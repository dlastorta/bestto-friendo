# Slice 06 — Inscripciones y snapshot de mazos

Estado: implementado y verificado localmente, pendiente de revisión. Base: Slice 05 integrado en `main`, 2026-11-25.

## Alcance confirmado

- Inscribir jugadores y seleccionar un mazo propio del juego del torneo.
- Conservar una copia autónoma del contenido presentado, además de identificar mazo y revisión de origen.
- Mantener literalmente la lista de cartas, sin parseo ni controles de cartas o cantidades.
- Editar o archivar el mazo en la biblioteca no modifica la copia presentada.
- Mantener separación Domain/Application/Infrastructure/API, CSRF y autorización desde la sesión.
- Privacidad confirmada: solo el jugador puede consultar su lista presentada. El organizador únicamente ve si presentó un mazo; sus respuestas HTTP no incluyen lista, nombre, facciones ni referencias al mazo de origen.
- No implementar inicio del torneo, resultados, clasificación ni publicación. El reemplazo auditado de presentaciones se resolverá con el Slice 07.

## Decisiones confirmadas

1. Inscripción mediante enlace compartido por el organizador. Requiere cuenta autenticada, pero no rol de organizador. El enlace permite consultar únicamente nombre, juego y estado del torneo, sin abrir sus endpoints administrativos.
2. Inscripción sin mazo permitida; presentación obligatoria antes del inicio. El caso de uso de inicio y su validación se implementan en Slice 07.
3. Solo el participante ve el contenido presentado, incluso frente al organizador. Un organizador que también participa solo ve su propia presentación.

## Implementación

- Inscripción vinculada al torneo y al perfil autenticado, con unicidad por torneo/jugador.
- Snapshot separado de `DeckRevision`: IDs de origen, revisión, juego, nombre, texto literal, facciones y momento de presentación. Consultar el snapshot sin reconstruir su contenido desde la biblioteca.
- Validar propiedad y compatibilidad del juego antes de copiar. Las referencias de origen sirven para trazabilidad, no para actualizar el snapshot.
- Cada escritura guarda sus cambios en una transacción: inscripción y versión del torneo, o snapshot y versión del torneo. Índice único torneo/jugador y token de concurrencia `ParticipationVersion`; una carrera devuelve `409` y permite recargar/reintentar. Repetir una inscripción ya guardada devuelve la existente.
- Migración versionada y puertos específicos en Application; EF exclusivamente en Infrastructure.
- El enlace se habilita explícitamente, usa un UUID aleatorio distinto del ID de torneo y se conserva al solicitarlo nuevamente. Se comparte como `/#inscripcion=TOKEN`; el fragmento no se envía como Referer. No concede acceso a listas, participantes ni reglas privadas. No incluye caducidad, rotación ni cancelación de inscripciones en este slice.
- La presentación exige mazo propio, activo, del mismo juego y versión seleccionada vigente. Se copia la revisión leída; cambios posteriores no se reflejan. La biblioteca sigue aceptando listas vacías o texto arbitrario.
- Migración `20261125193211_AddTournamentRegistrations`: inscripciones, snapshots con referencia restrictiva a la revisión de origen y columnas de enlace/concurrencia del torneo. No reconstruir snapshots desde esa referencia.

## Contratos HTTP

Todos requieren sesión y devuelven `Cache-Control: no-store`. Escrituras requieren `X-CSRF-TOKEN`.

| Endpoint | Permiso y resultado |
|---|---|
| `POST /api/tournaments/{id}/registration-link` | Organizador propietario; habilita/recupera `{ token }` solo en borrador. |
| `GET /api/tournaments/{id}/participants` | Organizador propietario; solo `playerId`, `displayName`, `hasPresentedDeck`. |
| `GET /api/registrations/invite/{token}` | Cuenta autenticada con enlace; `tournamentId`, `name`, `gameId`, `status`. |
| `POST /api/registrations/invite/{token}` | Inscribe el perfil de la sesión sin mazo; no acepta ID de jugador del cliente. |
| `GET /api/registrations` | Inscripciones propias con nombre/juego y estado de presentación. |
| `GET /api/registrations/{tournamentId}` | Presentación propia, o `deck: null` mientras está pendiente. |
| `POST /api/registrations/{tournamentId}/deck` | `{ deckId, expectedDeckVersion }`; guarda primera copia privada. |

`400`: datos inválidos/faltantes o CSRF; `401`: sin sesión; `403`: sin rol administrativo donde se requiere; `404`: enlace inválido, recurso ajeno/inexistente o ausencia de inscripción propia; `409`: concurrencia, mazo archivado/desactualizado, presentación ya existente o torneo fuera de borrador. No existe endpoint para consultar el snapshot por ID de otro jugador.

React permite obtener enlace y consultar estado de inscriptos desde el detalle administrativo. El jugador abre el enlace, ingresa, consulta la invitación y confirma inscripción; en «Mis inscripciones» recarga, abre el torneo y presenta un mazo. El snapshot se muestra como texto escapado. Al cerrar sesión se desmontan ambas vistas privadas.

## Verificaciones ejecutadas

- Pruebas puras de copia independiente y conservación literal; editar/archivar origen no altera presentación.
- Pruebas HTTP de sesión, permisos, CSRF y aislamiento entre cuentas/torneos.
- Verificar que las respuestas al organizador no contienen el snapshot ni datos del mazo y que no puede consultar la presentación privada de otro jugador.
- PostgreSQL real: migración, persistencia del snapshot y duplicación concurrente.
- React: flujo de inscripción, selección compatible y limpieza de vistas privadas al salir.
- Restore, build, tests, auditorías NuGet/npm y límites de arquitectura; backend en WSL por el bloqueo conocido de DLLs en Windows.

Resultados locales: **59/59 tests backend** con `RUN_POSTGRES_INTEGRATION=true` y PostgreSQL 16 real en WSL; **28/28 frontend**, `npm ci` y ambos builds correctos. Auditorías NuGet directa/transitiva y npm sin vulnerabilidades conocidas. Arquitectura correcta y modelo EF sin cambios pendientes. Migración probada en base nueva y sobre la base local anterior.

Prueba manual en navegador con dos cuentas: generar enlace, ingresar como participante, inscribirse sin mazo, crear y presentar mazo después, ver contenido como jugador, cerrar sesión y consultar como organizador únicamente «Mazo presentado». La vista privada desaparece al cerrar sesión.

Limitaciones: backend nativo Windows bloqueado por Smart App Control; ejecutado en WSL. Sin inicio, reemplazo auditado de presentación, cancelación ni rotación del enlace. No se declara validado un inicio sin mazo: todavía no existe esa operación. Mantener las restricciones de despliegue público del Slice 02.
