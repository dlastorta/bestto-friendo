# Slice 11 — Byes

Estado: implementado y verificado automáticamente en `codex/slice-11-byes`, desde main tras integrar PR #11.

## Reglas confirmadas

- Asignación manual exclusiva del organizador propietario con rol vigente, en torneo en curso con byes habilitados en StartedRules.
- Solo un inscripto sin partida en esa ronda y sin bye previo; máximo un bye por ronda y solo con cantidad impar de inscriptos. No hay elección automática (Slice 12).
- El bye otorga inmediatamente los puntos congelados. No tiene rival ficticio, no participa en MWP ni agrega rivales a Buchholz/OMW%. Sus puntos sí integran el total que los rivales usan en Buchholz.
- Asignación inmutable con actor y fecha. No se implementa cancelación, sustitución ni edición; requieren una política auditada futura.
- Nuevos inicios capturan ByePolicyVersion=1. Migración conserva versión 0 en torneos ya iniciados, sin habilitarles asignación ni cambiar sus resultados. TieBreakerFormulaVersion mantiene su semántica existente.

## Contrato

GET/POST `/api/tournaments/{id}/byes`: sesión, rol TournamentOrganizer vigente y propiedad; POST requiere CSRF. Cuerpo `{ round, playerId }`; el servidor determina actor, fecha y puntos. DTO `{ id, round, playerId, points, actorPlayerId, assignedAt }`, sin referencias ni listas de mazos. GET lista por ronda; POST devuelve 200 con la asignación. 401 sin sesión, 403 sin rol, 404 ajeno/inexistente, 400 regla inválida, 409 ocupación/concurrencia o política histórica incompatible.

La tabla autorizada existente incluye los puntos del bye. La administración de asignaciones sigue privada del organizador; los jugadores no obtienen acceso administrativo.

## Persistencia y concurrencia

TournamentBye es independiente de TournamentMatch; ITournamentByes es un puerto específico. Ambos tipos de creación modifican MatchesVersion del torneo y confirman cabecera e inserción en una transacción. Así, lecturas simultáneas no pueden confirmar dos ocupaciones, incluso entre bye y partida. Índices únicos por torneo/jugador y torneo/ronda refuerzan límites. Todo futuro generador debe consultar ambos tipos y respetar el token común.

Migración AddTournamentByes agrega tabla y versión congelada, sin recalcular históricos. Down rechaza borrar byes o políticas de torneos iniciados con versión 1. EF permanece exclusivamente en Infrastructure.

## Verificación

Pruebas puras de límites, ocupación mutua, puntos fraccionarios y desempates tras corregir resultados; HTTP de permisos, CSRF, rol revocado, política histórica y privacidad; PostgreSQL con ambas órdenes de commit para bye/partida, misma ronda y mismo jugador. React verifica envío con CSRF, auditoría, conflicto y pérdida de acceso.

Resultados locales (2026-11-27):

- .NET restore y test en WSL Ubuntu 24.04 / PostgreSQL 16 real: **87/87**, cero fallos u omitidos, RUN_POSTGRES_INTEGRATION=true.
- Migración aplicada en slice08_tests; EF has-pending-model-changes sin diferencias. Histórico previo recibe ByePolicyVersion=0.
- npm ci, tests **45/45** y build TypeScript/Vite correctos.
- NuGet directas/transitivas sin vulnerabilidades conocidas; npm audit: 0 vulnerabilidades.
- scripts/verify_structure.py: correcto; git diff --check limpio.
- Verificación visual pendiente: servidor API y Vite arrancaron, pero el navegador integrado agotó el tiempo de conexión a localhost:5174 y 127.0.0.1:5174. No se afirma recorrido manual exitoso.

Reproducción: `dotnet restore TournamentManager.slnx`, `dotnet tool restore`, configurar ConnectionStrings__TournamentDb para una base PostgreSQL local de pruebas, aplicar `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api`; con RUN_POSTGRES_INTEGRATION=true ejecutar `dotnet test TournamentManager.slnx`. En frontend: `npm ci`, `npm test -- --run`, `npm run build`, `npm audit --audit-level=high`. NuGet: `dotnet list TournamentManager.slnx package --vulnerable --include-transitive`.

Recorrido manual pendiente: iniciar un torneo nuevo con tres jugadores y byes habilitados; consultar byes, asignar a uno en ronda 1, comprobar puntos en la tabla y rechazo de una partida del mismo jugador en esa ronda y de un segundo bye. Comprobar que un torneo histórico rechaza asignación y conserva su tabla.
