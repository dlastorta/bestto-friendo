# Slice 13 — Cierre, reapertura y regeneración auditada

## Decisiones confirmadas

- Organizador propietario: cierre manual tras al menos una ronda; todas las rondas con todos los inscriptos asignados y todos los resultados cargados. Finalizar no publica.
- Reapertura explícita con motivo para corregir resultados después del cierre. Esta modalidad permite corregir resultados y finalizar nuevamente, pero nunca crear/regenerar rondas, partidas ni byes.
- En torneo en curso (antes de finalizar), correcciones seguidas de una acción explícita permiten regenerar únicamente la última ronda existente, sin agregar otra. Recalcular desde rondas anteriores; reemplazar pares y bye, anulando los resultados de la versión anterior pero conservando todo su historial.
- Correcciones de cualquier ronda permitidas; no se regeneran rondas automáticamente. Confirmación del usuario: «exacto».
- Playoffs futuros: antes de comenzar el Top 8, corregir rondas previas y regenerar cuadro con historial. Fuera del MVP; caso con playoffs iniciados pendiente.

## Implementación y verificación

Implementado. Conservar reglas y algoritmos congelados, permisos, CSRF y privacidad; EF solo Infrastructure. Cierre/reapertura/regeneración deben excluir atómicamente escrituras deportivas concurrentes. La tabla utiliza exclusivamente asignaciones vigentes, nunca anuladas; el historial sigue accesible al organizador.

## Contratos HTTP

Todos requieren organizador propietario con rol vigente; POST exige CSRF, respuestas privadas sin caché.

- `POST /api/tournaments/{id}/finish`: `expectedLifecycleVersion`, `expectedMatchesVersion`, `expectedResultsVersion`.
- `POST /api/tournaments/{id}/reopen`: `expectedLifecycleVersion`, `reason` obligatorio (1–500 caracteres).
- `GET /api/tournaments/{id}/lifecycle`: eventos con actor/fecha/motivo y referencias a resultados/bye de cada cierre.
- `POST /api/tournaments/{id}/rounds/regenerate`: `round`, las tres versiones esperadas y `reason` obligatorio. Solo última ronda, torneo en curso antes del primer cierre.
- `GET /api/tournaments/{id}/rounds/history`: asignaciones anuladas, revisiones de resultados y actor/fecha/motivo del reemplazo.

Conflictos de estado/versión devuelven 409; nunca reintentar sobrescribiendo silenciosamente. Reglas congeladas y privacidad de listas intactas. Los torneos con algoritmo histórico 0 conservan emparejamientos manuales y no pueden regenerar.

## Evidencia local — 2026-11-27

- WSL Ubuntu 24.04, .NET 10, PostgreSQL real: restore y tests con `RUN_POSTGRES_INTEGRATION=true`; 111/111 tests backend. Incluye cierre/regeneración concurrentes en ambos órdenes de commit.
- Migración `20261127171358_AddTournamentLifecycleAndRoundRevisions` aplicada sobre base local con datos anteriores; `has-pending-model-changes` sin diferencias.
- Frontend: `npm ci`, 53/53 tests y build TypeScript/Vite; auditoría npm sin vulnerabilidades.
- NuGet: auditoría de dependencias directas/transitivas sin vulnerabilidades reportadas. Verificación de límites de capas exitosa.
- Navegador contra API/React locales: reemplazo de ronda 2 (versión 1 conservada, nueva versión 2), resultado pendiente, carga, cierre, reapertura restringida y nuevo cierre; historial visible con tres eventos.
- Backend ejecutado en Linux/WSL por el bloqueo de DLL de Windows. CI remoto pendiente al abrir el PR. Rollback de la migración con historial nuevo se bloquea para evitar pérdida; no se ejecutó rollback sobre la base local.

Para reproducir backend, configurar `ConnectionStrings__TournamentDb` hacia una base PostgreSQL de pruebas, habilitar `RUN_POSTGRES_INTEGRATION=true`, ejecutar `dotnet restore TournamentManager.slnx`, `dotnet tool restore`, `dotnet ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api` y `dotnet test TournamentManager.slnx`. En `frontend`, ejecutar `npm ci`, `npm test -- --run`, `npm run build` y `npm audit --audit-level=high`.
