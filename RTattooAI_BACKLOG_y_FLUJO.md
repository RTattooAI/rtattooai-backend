# RTattooAI — Product Backlog, Flujo y Plan de Sprint (TP2)

**Proyecto:** Sistema de reconocimiento de tatuajes basado en visión por computadora para la identificación de bandas organizadas en el Perú.
**Naturaleza del sistema:** Herramienta web de **apoyo** al análisis preliminar de la PNP. **No** es un sistema de identificación definitiva; todo resultado requiere validación de un analista humano.

---

## 1. Priorización del backlog (MoSCoW)

| Prioridad | ID | User Story | Entrega |
|---|---|---|---|
| MUST | US-01 | Autenticación con JWT | Día 1 |
| MUST | US-02 | Carga de imagen de tatuaje | Día 1–2 |
| MUST | US-03 | Detección + comparación con catálogo | Día 2 |
| MUST | US-04 | Visualización de resultados con aviso de apoyo | Día 2–3 |
| SHOULD | US-05 | Historial de análisis (trazabilidad) | Día 3 |
| SHOULD | US-07 | Enrolar tatuaje al catálogo (endpoint sí, UI mínima) | Día 2–3 |
| COULD | US-06 | Exportar reporte PDF | Si sobra tiempo |
| WON'T (este sprint) | — | RBAC multi-rol, administración del catálogo por UI | TP3 |

Cubre el **Entregable 4 del Charter** ("Prototipo Web Funcional Integrado"): integración total (Backend + Frontend + Módulo IA), carga de imágenes, consulta del catálogo, visualización de resultados y pruebas funcionales.

---

## 2. User Stories y criterios de aceptación

### US-01 — Autenticación
Como analista de la PNP, quiero iniciar sesión con credenciales autorizadas, para que solo personal habilitado acceda al sistema.
- Dado un usuario válido, cuando envío credenciales correctas, entonces recibo un token JWT y accedo al panel.
- Dado un usuario inválido, cuando envío credenciales incorrectas, entonces el acceso se rechaza sin revelar qué campo falló.
- Dado un token expirado, cuando hago una petición, entonces el sistema responde 401 y redirige al login.

### US-02 — Carga de imagen
Como analista, quiero cargar una fotografía de un tatuaje, para iniciar su análisis.
- Dado un archivo JPG/PNG ≤ 10 MB, cuando lo cargo, entonces se almacena en Blob y se crea un registro de análisis en estado "processing".
- Dado un formato o tamaño inválido, cuando intento cargar, entonces se muestra un error claro y no se crea el registro.

### US-03 — Detección y comparación
Como analista, quiero que el sistema detecte el tatuaje y lo compare con el catálogo, para obtener coincidencias preliminares en segundos.
- Dado un análisis en proceso, cuando el servicio IA responde, entonces se guardan las regiones detectadas y las N coincidencias con puntaje de similitud y banda asociada.
- Dado que no se detecta ningún tatuaje, cuando finaliza el proceso, entonces el estado es "no_detection" y se informa al usuario sin error.
- Dado un fallo del servicio IA, cuando ocurre, entonces el estado es "failed" y el análisis queda registrado para reintento.

### US-04 — Visualización de resultados
Como analista, quiero ver la imagen con las regiones detectadas y la lista de coincidencias ordenadas por similitud, para apoyar mi evaluación.
- Dado un análisis completado, cuando abro el resultado, entonces veo la imagen con cajas, las coincidencias rankeadas con su porcentaje de similitud y banda, y un **aviso permanente** de que el resultado es de apoyo y requiere validación humana.

### US-05 — Historial
Como analista, quiero consultar mis análisis previos, para dar trazabilidad a mi trabajo.
- Dado que tengo análisis previos, cuando abro el historial, entonces veo fecha, miniatura, estado y la mejor coincidencia, y puedo abrir cada resultado.

### US-06 — Reporte exportable (opcional)
Como analista, quiero exportar el resultado como PDF, para adjuntarlo a un expediente.
- Dado un análisis completado, cuando exporto, entonces obtengo un PDF con imagen, regiones, coincidencias y el aviso de apoyo.

### US-07 — Enrolar tatuaje al catálogo
Como personal autorizado, quiero agregar un tatuaje al catálogo indicando la banda asociada, para que el sistema lo considere en búsquedas futuras y el catálogo crezca con el uso.
- Dado una imagen y una banda, cuando enrolo, entonces el servicio calcula el embedding, lo agrega al índice persistente y se guardan los metadatos (banda, autor, fecha, fuente) en SQL con `validated = false`.
- Dado un tatuaje recién enrolado, cuando hago un nuevo análisis similar, entonces aparece entre las coincidencias.
- La procedencia y el autor quedan registrados para auditoría.

---

## 3. Flujo end-to-end

1. Analista inicia sesión → recibe JWT.
2. Carga imagen → frontend la envía al backend.
3. Backend guarda la imagen en Azure Blob y crea el análisis (estado "processing").
4. Backend llama `POST /analyze` del servicio FastAPI.
5. Servicio IA: YOLO (detección) → recorte → CNN (embedding) → similitud coseno contra el catálogo → top-K coincidencias por región.
6. Backend persiste detecciones y coincidencias en SQL Server; responde al frontend.
7. Frontend muestra imagen con cajas, lista de coincidencias rankeadas y el aviso de apoyo.
8. El análisis queda en el historial (trazabilidad/auditoría).

---

## 4. Plan de 5 días

- **Día 1 — Contrato + cimientos.** Congelar el contrato de la API. FastAPI con modelos reales + catálogo pequeño en memoria. Scaffold backend (capas, EF Core, migración inicial, JWT). Scaffold frontend (rutas, login, interceptor). Frontend contra mock que cumple el contrato.
- **Día 2 — Núcleo end-to-end.** Backend: upload + análisis + persistencia conectados al servicio IA. Frontend: upload y resultados con datos reales. Meta: flujo completo en local con una imagen.
- **Día 3 — Robustez + demo.** Ramas negativas (US-02/US-03), historial, aviso en UI, pruebas funcionales, fix de bugs críticos. Congelar alcance al mediodía; pulir demo en la tarde.
- **Día 4 — Despliegue de infra.** Resource Group → Azure SQL → Blob → Container Apps (inferencia) → App Service (backend) → Static Web Apps (frontend).
- **Día 5 — Integración cloud + validación.** Conectar todo, CORS, variables de entorno, prueba end-to-end en producción y plan B.

---

## 5. Catálogo (confirmado)

Existe un catálogo base. El plan de validación es que la PNP provea uno y/o que el catálogo crezca conforme se suban tatuajes marcados como pertenecientes a una banda.

Diseño adoptado:
- **Embeddings + ids:** índice persistente en el servicio de inferencia, guardado en Azure Blob. Se carga al arrancar, se agrega al enrolar (`/enroll`) y se vuelve a guardar.
- **Metadatos:** en SQL Server (banda, procedencia, autor, fecha, validación), para trazabilidad y auditoría.
- **Crecimiento en demo:** el flujo de enrolamiento (US-07) hace demostrable el aumento del catálogo, no solo prometido.

Consideraciones para el marco de herramienta de apoyo:
- Cada entrada registra **quién** la etiquetó, **cuándo** y la **fuente**.
- El flag `validated` deja abierta una compuerta de revisión humana antes de que una entrada cuente como confirmada. En la demo la búsqueda incluye todas; la compuerta formal es refinamiento de TP3. Esto mitiga el riesgo de etiquetas erróneas o sesgadas en el catálogo.
- Escala de producción (narrativa de tesis, no se construye ahora): Azure AI Search vectorial o pgvector.
