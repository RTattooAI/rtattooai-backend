# RTattooAI Backend — Reporte de Entrega

Documento de traspaso del estado actual del backend (ASP.NET Core) para continuar
el trabajo en otra sesión. Verificado contra el código real al momento de escribir
este reporte: build limpio, **18/18 pruebas unitarias en verde**, sin commits
pendientes de revisión (todo el trabajo vive sin commitear en `feature/backend-scaffold`,
rama actual: `main` en el árbol de trabajo local — confirmar antes de continuar).

---

## 1. Qué se construyó

Arquitectura por capas: `RTattooAI.Domain` / `RTattooAI.Application` /
`RTattooAI.Infrastructure` / `RTattooAI.Api`. .NET 10, EF Core 10, SQL Server,
Azure Blob Storage (Azurite en local), JWT, HttpClient tipado al servicio IA (FastAPI).

| Módulo / Endpoint | Estado | Notas |
|---|---|---|
| `POST /api/auth/login` | ✅ Funcional | JWT con claims `sub`, `NameIdentifier`, `Name`, `Email`, `Role` |
| `POST /api/analyses` (upload + IA + persistencia) | ✅ Funcional | Probado contra servicio IA real (`rtattooai-serving`) |
| `GET /api/analyses/{id}` | ✅ Funcional | Scopeado al usuario autenticado; enriquece `referenceImageUrl` vía SAS |
| `GET /api/analyses` (historial) | ✅ Funcional | Sin paginación ni filtros; calcula "mejor coincidencia" |
| `POST /api/catalog` (enrolar tatuaje) | ✅ Funcional | Find-or-create de `BandaCriminal` por nombre |
| `GET /api/catalog` | ✅ Funcional | Sin paginación |
| `IaServiceClient` (`/identify`, `/enroll`) | ✅ Funcional | Probado en vivo contra el contenedor FastAPI real |
| `AzureBlobStorage` (upload + SAS read URL) | ✅ Funcional | Ver limitación en §5 sobre Managed Identity |
| Seed inicial (rol + usuario analista) | ✅ Funcional | Solo corre en `Development`, vía `Seed:AnalystEmail/Password` |
| Migración EF Core inicial (`InitialCreate`) | ✅ Funcional | Esquema alineado al diseño DBML del proyecto (ver §2) |
| Pruebas unitarias (`AuthService`, `AnalysisService`, `CatalogService`) | ✅ Funcional | 18 tests, 0 fallos |
| Stub/fallback del servicio IA (Fase 8) | ⏸️ Pendiente | **Diferido explícitamente por el usuario**, no iniciado |
| Paginación/filtros de historial y catálogo | ❌ No iniciado | Ver §4 |
| Validación de duplicados al enrolar tatuaje | ❌ No iniciado | Se calcula el hash pero no se consulta antes de insertar |
| Soporte WEBP | ❌ No iniciado | Ver discrepancia de fuentes en §5 |
| RBAC (roles aplicados a autorización) | ❌ No iniciado | El modelo ya tiene `Rol`/`RolId`, no se usa en `[Authorize]` |
| Audit log / trazabilidad forense | ❌ No iniciado | Solo hay `ILogger` de errores, no persistente |
| Reporte PDF | ❌ No iniciado | Ningún paquete de PDF referenciado |
| Gestión de usuarios (CRUD) | ❌ No iniciado | Solo existe login; `IUsuarioRepository` solo lee |
| Dashboard de métricas / export CSV | ❌ No iniciado | — |

---

## 2. Contrato real implementado

Todas las rutas salvo `/api/auth/login` requieren `Authorization: Bearer <jwt>`
(filtro global `AuthorizeFilter` en `Program.cs`). JSON en camelCase (default de
ASP.NET Core / System.Text.Json).

### `POST /api/auth/login`
```json
// Request
{ "email": "...", "password": "..." }
// Response 200
{ "token": "eyJ...", "expiresAt": "2026-06-24T15:00:00Z",
  "user": { "id": "guid", "name": "Analista 1", "role": "analyst" } }
```
**401** si las credenciales no son válidas. Coincide con el contrato.

### `POST /api/analyses`
`multipart/form-data` con `image` (jpg/png, máx. 10MB). Responde **201** con el
objeto Analysis completo (`status`: `processing|completed|no_detection|failed`).
**400** con `{ "error": "..." }` si la imagen no es válida. Coincide con el contrato.

### `GET /api/analyses` (historial) y `GET /api/analyses/{id}`
```json
// GET /api/analyses → 200
[{ "id": "guid", "thumbnailUrl": null, "status": "completed",
   "createdAt": "...", "topBand": "Los Pulpos", "topSimilarity": 0.87 }]
```
`GetById` devuelve **404** si el id no existe **o pertenece a otro usuario**
(no se distingue, para no filtrar existencia de registros ajenos — decisión
deliberada, no está en el contrato original).

### `POST /api/catalog` y `GET /api/catalog`
`multipart/form-data` con `image` + `band`. Responde **201**:
```json
{ "catalogId": "tat_00890", "band": "Los Pulpos",
  "imageUrl": "https://.../catalogo/{id}/original.jpg",
  "source": "carga_manual", "addedBy": "guid", "addedAt": "...", "validated": false }
```
Coincide exactamente con `API_CONTRACT.md` §B.

### Disclaimer (constante, igual al contrato)
`"Resultado preliminar de apoyo. No constituye identificación definitiva y requiere validación de un analista."`

### Desviaciones respecto a `API_CONTRACT.md`

1. **Modelo de datos (§D del contrato)**: el contrato describe entidades planas en
   inglés (`User`, `Analysis`, `Detection`, `Match`, `CatalogItem`). El proyecto
   migró a un esquema EF Core alineado al **DBML real del proyecto**, en español
   (`Usuario`, `Rol`, `Consulta`, `Deteccion`, `Coincidencia`, `TatuajeCatalogo`,
   `BandaCriminal`) — decisión arquitectónica explícita tomada antes de esta sesión.
   El contrato JSON (§B/§C, lo que ve el frontend) se mantiene 100% compatible;
   solo cambió el modelo interno.
2. **`threshold`/`margin`** que devuelve `/identify` (§A) no se capturan ni se
   exponen en ningún lado — consistente con el Esquema Analysis (§C), que no los
   incluye, pero hoy no hay forma de ajustar el umbral desde el backend.
3. **`top_k` fijo en 5**, no parametrizable desde `POST /api/analyses` (el
   contrato solo define el default en el servicio IA, no exige que sea configurable
   desde el backend).
4. **Historial scopeado por usuario**: el contrato dice "Historial del usuario"
   sin definir si debe existir vista multi-usuario/supervisor; se interpretó como
   estrictamente per-owner.

---

## 3. Cómo correrlo en local

Requisitos: .NET 10 SDK, Docker Desktop.

```bash
# 1. Levantar SQL Server + Azurite
docker compose -f docker-compose.local.yml up -d

# 2. Configurar secretos de usuario (NUNCA en appsettings.json committeado)
cd src/RTattooAI.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=RTattooAI;User Id=sa;Password=<ver MSSQL_SA_PASSWORD en docker-compose.local.yml>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "<cadena aleatoria larga, solo para local>"
dotnet user-secrets set "Blob:ConnectionString" "UseDevelopmentStorage=true"   # cadena fija pública de Azurite
dotnet user-secrets set "Seed:AnalystEmail" "<email arbitrario de prueba>"
dotnet user-secrets set "Seed:AnalystPassword" "<password arbitrario de prueba>"
```

`IaService:BaseUrl` ya tiene default `http://localhost:8000` en `appsettings.json`
(no es secreto). Equivalentes como variables de entorno (doble guion bajo):
`ConnectionStrings__Default`, `Jwt__Key`, `Blob__ConnectionString`,
`IaService__BaseUrl`, `Seed__AnalystEmail`, `Seed__AnalystPassword`.

```bash
# 3. Levantar el servicio IA real (repo/contenedor separado, no está en
#    docker-compose.local.yml). Debe escuchar en :8000 y exponer /health, /identify, /enroll.

# 4. Correr la API (en Development aplica migraciones y siembra el usuario solo)
dotnet run --project src/RTattooAI.Api

# 5. Swagger UI disponible en /swagger (Development únicamente)

# 6. Pruebas unitarias
dotnet test
```

Sin el servicio IA corriendo, `POST /api/analyses` y `POST /api/catalog`
fallarán (no hay fallback — ver §1, Fase 8 diferida).

---

## 4. Cobertura de user stories

### Backlog original (US-01..US-07) — todas con backend funcional
US-01 (login), US-02 (carga imagen), US-03 (detección+comparación vía IA),
US-04 (visualización con disclaimer), US-05 (historial), US-07 (enrolar catálogo)
→ implementadas y probadas en vivo. US-06 (reporte PDF) quedó fuera del sprint
("COULD"), no iniciada.

### Backlog nuevo (HU-01..HU-14) — mapeo de cobertura

| HU | Estado | Gap principal |
|---|---|---|
| HU-01 carga de imagen | 🟡 Parcial | No acepta WEBP (solo JPG/PNG); preview es responsabilidad de frontend |
| HU-02 recorte/ajuste de imagen | N/A backend | 100% frontend |
| HU-03 registrar tatuaje en catálogo | 🟡 Parcial | Sin validación de duplicados (hash calculado pero no consultado); sin campo categoría/descripción expuesto |
| HU-04 detección YOLO | ✅ Completa | SLA `<5s` no se mide activamente, solo observado |
| HU-05 comparación + top-5 | 🟡 Parcial | `threshold`/`margin` del servicio IA se ignoran; no hay umbral configurable |
| HU-06 disclaimer + semáforo | ✅ Backend completo | Semáforo de color es derivable en frontend desde `confidence`/`similarity` ya devueltos |
| HU-07 reporte PDF | ❌ No iniciada | Sin librería de PDF en el proyecto |
| HU-08 historial con filtros/auditoría | 🟡 Parcial | Sin paginación/filtros, sin vista de supervisor, sin registro de quién consultó |
| HU-09 gestión de usuarios | ❌ No iniciada | Solo login; sin CRUD |
| HU-10 dashboard de métricas | ❌ No iniciada | — |
| HU-11/HU-12 dataset sintético/etiquetado | N/A backend | Pipeline ML, otro repo/componente |
| HU-13 cifrado + logs de acceso | 🟡 Parcial | HTTPS forzado; cifrado en reposo es default de Azure Storage (no verificado en despliegue real); sin logs de acceso a imágenes |
| HU-14 audit log forense | ❌ No iniciada | — |

**Sugerencia de prioridad** (de lo que falta, dentro de las "Alta" del backlog
nuevo): HU-03 (gap barato de cerrar) → HU-14 (audit log, cuanto más se tarde
más historial queda sin auditar) → HU-07 (PDF) → HU-05 (umbral configurable,
requiere coordinar con el repo del servicio IA) → HU-13 (logs de acceso).
HU-08 y HU-09 comparten la necesidad de RBAC mínimo — conviene resolverlo una sola vez.

---

## 5. Bugs conocidos, deuda técnica y supuestos

**Deuda técnica:**
- No hay middleware global de manejo de excepciones; solo `catch (InvalidImageException)`
  puntual en `AnalysesController`/`CatalogController`. Cualquier otra excepción no
  controlada (ej. servicio IA caído, SQL caído) devuelve un 500 genérico sin
  formato JSON consistente.
- Sin reintentos/circuit breaker hacia el servicio IA: si `rtattooai-serving` no
  responde, `POST /api/analyses` y `POST /api/catalog` explotan sin mensaje
  amigable para el usuario final.
- `GetOrCreateByNombreAsync` (find-or-create de `BandaCriminal`) no maneja
  condición de carrera: dos requests concurrentes creando la misma banda nueva
  pueden violar el índice único de `Nombre` con una excepción SQL no capturada.
- Sin paginación en ningún listado (`GET /api/analyses`, `GET /api/catalog`) —
  crecerá sin límite con uso real.
- `AzureBlobStorage.GetReadUrl` depende de `BlobClient.CanGenerateSasUri`, que
  requiere credenciales de cuenta (account key). Con Managed Identity pura en
  producción `CanGenerateSasUri` sería `false` y el código cae a devolver la
  URL **sin firmar** (`blobClient.Uri`), lo cual no sirve si el contenedor es
  privado. Pendiente migrar a "User Delegation SAS" antes de usar Managed Identity.
- Sin RBAC ni audit log (ver §4).
- Sin CORS configurado en `Program.cs` — si el frontend se despliega en otro
  dominio, todas las llamadas desde navegador fallarán hasta agregar
  `AddCors`/`UseCors`.

**Supuestos:**
- El servicio IA real expone `/health`, `/identify`, `/enroll` tal como documenta
  el contrato — confirmado por pruebas manuales en vivo, pero el backend no
  valida su disponibilidad al arrancar (no hay healthcheck activo).
- Imágenes siempre en JPG/PNG. La lista de HU nueva (HU-01) pide también WEBP,
  lo que **contradice** `API_CONTRACT.md` §A (que dice "jpg/png" en todos lados)
  — discrepancia entre las dos fuentes, sin resolver. Decidir antes de tocar `ImageValidator`.
- `top_k=5` fijo es aceptable para la demo actual.
- Un solo rol (`analyst`) sembrado; el modelo soporta más roles pero ninguna
  lógica los usa todavía.

**Bugs conocidos:** ninguno abierto — todas las pruebas manuales en vivo
(200/201/400/401/404, contra SQL Server, Azurite y el servicio IA real) pasaron
en el último ciclo de verificación de cada fase.

---

## 6. Qué falta para despliegue en Azure

Hoy todo corre 100% local (contenedores Docker de SQL Server + Azurite, servicio
IA manual). Para Azure falta:

1. **Azure SQL Database** (o Managed Instance) real + migrar `ConnectionStrings:Default`
   vía App Service Configuration o Key Vault (nunca en el repo).
2. **Azure Storage Account** real + migrar `Blob:ConnectionString`. Idealmente
   mover a Managed Identity (`DefaultAzureCredential`) en vez de connection
   string con account key — requiere el cambio de código de SAS descrito en §5
   (User Delegation SAS), no es solo configuración.
3. **Desplegar el servicio IA** (`rtattooai-serving`) en su propio contenedor/App
   Service/Container Apps. El contrato exige que **no esté expuesto a internet
   directamente** — falta definir VNET/Private Endpoint o regla de firewall que
   solo permita tráfico desde el backend.
4. **HTTPS real**: `UseHttpsRedirection()` solo fuerza el esquema; el certificado
   real depende de App Service/Front Door/Application Gateway.
5. **Variables de entorno / Key Vault** para producción: `ConnectionStrings__Default`,
   `Jwt__Key`, `Blob__ConnectionString`, `IaService__BaseUrl`, y decidir si el
   seed automático (`Seed__AnalystEmail/Password`) debe desactivarse en producción
   (hoy solo corre si `IsDevelopment()`, así que en producción no se aplican
   migraciones ni se siembra nada — falta definir el mecanismo de despliegue de
   esquema, ej. paso de release con `dotnet ef database update`).
6. **CORS**: agregar política explícita antes de que el frontend apunte a un
   dominio distinto al del backend.
7. **Observabilidad**: sin Application Insights ni logging centralizado configurado.
8. **HU-13/HU-14**: si la trazabilidad forense (logs de acceso, audit log) es
   requisito desde el día 1 en producción, conviene resolverlas antes o durante
   el despliegue, no después.
