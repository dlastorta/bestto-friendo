# Slice 12 — Emparejamientos suizos

Estado: implementado en `codex/slice-12-swiss`, desde main con PR #12 integrado. Verificado localmente; pendiente de revisión.

## Reglas confirmadas

- Solo el organizador propietario con rol vigente genera rondas de un torneo en curso. No publicar ni finalizar torneos en este slice.
- Primera ronda al azar: sorteo de todos los inscriptos, sin orden previo. Si son impares, el primero del sorteo recibe el bye; es una elección uniforme. El orden completo se conserva para auditoría.
- Desde la segunda ronda se usa la tabla vigente: puntos y luego la cadena de resistencia congelada (Buchholz, OMW% y/o descarte, según configuración). Recorrer desde el peor clasificado hacia arriba, buscando primero al vecino disponible. Una igualdad deportiva total mantiene puesto compartido; el ID solo estabiliza el orden operativo después de la primera ronda.
- No repetir rivales, incluidos enfrentamientos con forfeit. Si una elección deja jugadores sin rival, explorar alternativas. No se promete un óptimo global de distancia: se busca la primera solución completa siguiendo el orden desde abajo.
- Con impares, priorizar al peor clasificado sin bye previo. Si no permite emparejar al resto, probar el siguiente elegible, según la política aprobada. Nunca segundo bye ni rival ficticio. Byes deshabilitados o candidatos agotados producen error sin cambios.
- No comenzar una ronda sin completar todas las anteriores: todos los inscriptos asignados una vez, partidas con resultado y/o bye. No saltar números. La generación crea exclusivamente la siguiente ronda completa, sin completar ni sobrescribir una ronda manual parcial.
- Los controles de avance y no revancha también se aplican a las partidas manuales de nuevos torneos; el avance se valida al asignar un bye manual. La elección manual del bye sigue siendo la operación del Slice 11.
- Las correcciones posteriores conservan los cruces ya guardados; solo influyen en generaciones futuras y tabla.
- Nuevos inicios capturan PairingAlgorithmVersion=1. Los torneos previamente iniciados reciben versión 0 y conservan modalidad manual anterior (incluida su semántica de rondas). No se reescriben resultados ni byes históricos.

## Contrato HTTP y React

GET `/api/tournaments/{id}/rounds`: `{ nextRound, enabled, rounds }`. Lista únicamente rondas generadas; la próxima numeración considera también partidas y byes manuales. Cada ronda contiene número, versión de algoritmo, actor, fecha, orden de participantes, pares con ID de partida y bye opcional. Sin listas ni referencias de mazos.

POST a la misma ruta: `{ expectedRound }`. Devuelve 200 con la ronda completa guardada. El servidor obtiene participantes, tabla, sorteo, actor y fecha; no acepta cruces ni puntos enviados por cliente. 401 sin sesión, 403 sin rol, 404 ajeno/inexistente, 400 entrada inválida y 409 por estado, históricos, pendientes, numeración desactualizada, imposibilidad o conflicto concurrente. Lecturas privadas no-store; escrituras con CSRF. Roles revalidados en cada petición.

React permite consultar y confirmar generación, muestra pares, bye, actor/fecha y orden del sorteo inicial. No replica el algoritmo. Ante conflicto no presenta una ronda ficticia; permite recargar. Ante pérdida de sesión/rol se desmonta la vista privada. Resultados y tabla se consultan desde las vistas existentes.

## Persistencia y concurrencia

SwissRound y RoundProgress implementan reglas puras en Domain. Application orquesta puertos específicos; ISwissRounds confirma toda la generación. Infrastructure agrega cabecera de ronda, partidas, bye opcional y cambios de versión del torneo en una sola transacción SaveChanges. No hay commits parciales.

MatchesVersion protege asignaciones; ResultsVersion cambia con cada carga/corrección. Ambos son tokens de concurrencia del torneo: una generación calculada con resultados viejos no puede confirmar después de una corrección concurrente, ni viceversa. Dos generaciones o generación/partida/bye también se excluyen. Un conflicto exige recargar; no se reintenta silenciosamente con otro sorteo.

La ronda conserva actor, fecha, versión de algoritmo, orden completo y versiones fuente. Migración AddSwissRounds agrega swiss_rounds, ResultsVersion y PairingAlgorithmVersion con default 0 para históricos. Down bloquea eliminación si hay rondas o torneos iniciados con la política nueva. EF permanece exclusivamente en Infrastructure.

La búsqueda usa alternativas con memoria de estados fallidos y cancelación. Límites técnicos iniciales: 512 participantes y 100000 estados explorados por petición. Superarlos devuelve error explícito sin asignaciones, distinguiéndolo de imposibilidad demostrada. No son reglas deportivas ni se flexibiliza la prohibición de revancha al alcanzarlos.

## Pruebas y verificaciones

- Dominio: sorteo provisto reproducible, cobertura completa par/impar, orden desde abajo, bye elegible, búsqueda con retroceso, rivales agotados/forfeits, byes agotados, resultados pendientes, rondas incompletas/saltadas, corrección sin cambiar cruces e históricos manuales.
- PostgreSQL: HTTP de autorización, propiedad, CSRF, revocación, privacidad, persistencia del sorteo y ronda; ocho casos con ambas órdenes de commit para generación simultánea, partida manual, bye manual y corrección de resultado. Verifica rollback sin filas parciales.
- Casos anteriores de rondas manuales y revanchas se mantienen con fixtures explícitos de versión histórica 0, sin modificar sus expectativas deportivas.
- React: confirmación, CSRF, pares/bye/sorteo, error por pendientes, históricos y pérdida de acceso.

Resultados locales (2026-11-27): backend WSL con PostgreSQL real **105/105**, cero fallos/omitidos; frontend **50/50**, npm ci y build TypeScript/Vite correctos. NuGet directo/transitivo y npm audit sin vulnerabilidades conocidas. Migración aplicada, EF sin cambios pendientes de modelo, arquitectura y git diff --check correctos. Verificación en navegador: sesión de organizador en base de pruebas, consulta de sorteo auditado, generación de ronda 2 con pares y bye, rechazo de ronda 3 con resultado pendiente. Primera generación aleatoria cubierta además por tests HTTP y puros. CI remoto pendiente al abrir el PR.

Reproducir: `dotnet restore TournamentManager.slnx`, `dotnet tool restore`, configurar ConnectionStrings__TournamentDb para PostgreSQL de pruebas, aplicar `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api`, ejecutar `dotnet test TournamentManager.slnx` con RUN_POSTGRES_INTEGRATION=true. En frontend: `npm ci`, `npm test -- --run`, `npm run build`, `npm audit --audit-level=high`. NuGet: `dotnet list TournamentManager.slnx package --vulnerable --include-transitive`.
