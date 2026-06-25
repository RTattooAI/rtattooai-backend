# RTattooAI — Backend (ASP.NET Core)

## Contexto
Herramienta web de APOYO para la PNP que identifica tatuajes asociados a bandas
organizadas. NO es identificación definitiva: todo resultado es preliminar y
requiere validación humana. Refleja esto en mensajes y en el campo `disclaimer`.

## Arquitectura (por capas)
- RTattooAI.Api: Controllers, Program.cs, DI, JWT, CORS.
- RTattooAI.Application: servicios, DTOs, interfaces.
- RTattooAI.Domain: entidades (User, Analysis, Detection, Match, CatalogItem).
- RTattooAI.Infrastructure: EF Core (SQL Server), Blob Storage, HttpClient tipado al servicio IA.

El backend ORQUESTA: recibe imagen → guarda en Blob → POST /identify al servicio
FastAPI → persiste resultados (incluido `verdict`) → responde. No corre inferencia él mismo.

## Contrato
Implementa exactamente los endpoints de API_CONTRACT.md (secciones B, C, D):
login, POST/GET analyses y POST/GET catalog. El esquema Analysis, los estados
(processing|completed|no_detection|failed) y el `verdict` por detección son obligatorios.

## Convenciones
- C# nullable habilitado, async/await en todo I/O.
- Controllers delgados; lógica en Application. DTOs, no entidades, hacia afuera.
- HttpClient tipado para el servicio IA. URL del servicio por configuración.
- Sin secretos en código: connection strings, JWT secret, Blob y URL IA por
  variables de entorno / appsettings (sin valores reales en el repo).
- Pruebas unitarias para los servicios de Application.

## Reglas de trabajo
- Ramas feature/*, nunca commits directos a main.
- Sin atribución ni co-autoría de Claude en commits.

## Definición de "terminado"
Compila, corre local con SQL local o contenedor, cada endpoint probado, y cubre
el criterio de aceptación de su user story (ver BACKLOG).