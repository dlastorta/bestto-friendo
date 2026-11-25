# Slice 03 — Biblioteca personal de mazos

Estado: implementado y verificado localmente, pendiente de revisión e integración, 2026-11-25.

## Comportamiento confirmado

- Un jugador autenticado puede crear, listar, consultar, editar y archivar sus propios mazos por juego.
- Puede mantener varios mazos del mismo juego. No se impone unicidad del nombre.
- La propiedad se determina desde la sesión y el perfil de jugador; nunca desde un dueño enviado en el cuerpo de la petición.
- Otras cuentas, incluidos administradores del catálogo, no pueden leer ni modificar la biblioteca ajena.
- Las facciones deben pertenecer al juego y respetar la cardinalidad del catálogo; reutilizar las reglas de `Game`.
- Cada edición conserva una nueva revisión lógica sin modificar revisiones anteriores. Archivar no elimina el mazo ni su historial.
- La biblioteca personal y las revisiones no son inscripciones ni snapshots de torneo. Estos últimos se implementarán en Slice 06.

## Lista de cartas confirmada

Texto libre conservado literalmente para mostrarlo después. El formato sugerido es `N x Nombre de la carta`, donde N es la cantidad y x el separador. No se parsea, normaliza ni valida el texto, las cantidades, los nombres o la legalidad. Se permiten listas vacías y líneas sin ese formato. Las validaciones de nombre del mazo, juego y facciones son independientes de la lista.

## Contratos y decisiones técnicas

Todas las rutas requieren sesión y devuelven `Cache-Control: no-store`. Las escrituras también requieren `X-CSRF-TOKEN` obtenido en `/api/auth/csrf`.

| Método y ruta | Comportamiento |
|---|---|
| `GET /api/decks?gameId={id}&archived=false` | Resúmenes propios; ambos filtros opcionales, activos por defecto. |
| `POST /api/decks` | Crea con `gameId`, `name`, `listText`, `factionIds`; responde `201` con detalle y Location. |
| `GET /api/decks/{id}` | Detalle propio con versión y revisión actual. |
| `PUT /api/decks/{id}` | Recibe `name`, `listText`, `factionIds`, `expectedVersion`; agrega una revisión. |
| `GET /api/decks/{id}/revisions` | Revisiones propias, de más reciente a más antigua. |
| `POST /api/decks/{id}/archive` | Recibe `expectedVersion`; archiva sin borrar revisiones. |

- Nombre de mazo entre 2 y 120 caracteres, sin unicidad; juego fijo después de crear. Para otro juego se crea otro mazo.
- Los archivados conservan consulta e historial, pero no se editan. No se incluye restauración ni eliminación.
- `version` cambia al editar o archivar; el número de revisión solo al crear/editar. El token de concurrencia y la transacción de EF impiden perder cambios entre peticiones simultáneas.
- `400` para metadatos inválidos o CSRF; `401` sin sesión; `404` para inexistentes y ajenos; `409` para edición de archivados o versión desactualizada. Ningún permiso de catálogo habilita acceso a mazos ajenos.
- UI: filtros de juego/archivo, editor, historial y recarga explícita. Un conflicto conserva el borrador; recargar el mazo lo descarta. Al salir/cambiar de cuenta se desmonta la biblioteca; una respuesta `401` también borra la vista privada y vuelve al login.
- Migración `AddDeckLibrary`: tablas `decks` y `deck_revisions`, relaciones restrictivas con juego/perfil; facciones como IDs de PostgreSQL (`uuid[]`) validados por Domain.

## Verificación prevista

- Dominio: pertenencia, cardinalidad, duplicados e inmutabilidad de revisiones.
- Aplicación/HTTP: operaciones propias, `401` anónimo, rechazo del acceso ajeno, CSRF y ausencia de privilegios especiales para `CatalogAdmin` sobre mazos.
- PostgreSQL: migraciones versionadas, persistencia de revisiones y control de ediciones concurrentes.
- React: creación, consulta, edición, archivo y recuperación ante errores de sesión.
- Build, tests, auditorías NuGet/npm y comprobación de capas; backend en WSL por el bloqueo de Smart App Control en Windows.

## Fuera de alcance

Torneos, inscripciones, snapshots de presentación, formatos avanzados, legalidad/importación de cartas, invitados, Google, estadísticas, publicación y eliminación física de mazos.

## Evidencia local (2026-11-25)

- `dotnet restore TournamentManager.slnx` y compilación en WSL Ubuntu 24.04 / .NET 10: correctos.
- `RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx`: **41/41**, incluyendo PostgreSQL 16 real en base aislada `slice03_tests` (socket local, puerto 5433).
- Migraciones aplicadas desde base vacía; `has-pending-model-changes`: sin cambios pendientes.
- `npm ci`, `npm test -- --run`: **14/14**; `npm run build`: correcto. Fue necesario detener el Vite anterior, que retenía el módulo nativo durante `npm ci`.
- Auditoría NuGet con transitivas: sin paquetes vulnerables; npm: **0 vulnerabilidades**. CI usa lockfile con `npm ci` y bloquea auditorías npm altas/críticas.
- `python3 scripts/verify_structure.py`: correcto; tests de dependencias compiladas correctos.
- Navegador con API/DB reales: login, creación con líneas sin formato, segunda revisión, archivo, consulta de la primera revisión intacta y logout que retira la biblioteca de la pantalla.
- Backend nativo Windows no ejecutado por el bloqueo conocido de Smart App Control; se usó WSL. CI y revisión del PR pendientes. Se conservan las limitaciones de despliegue público del Slice 02.

Para repetir contra una base PostgreSQL local descartable: configurar `ConnectionStrings__TournamentDb`, ejecutar `dotnet tool restore`, `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api` y luego los tests con `RUN_POSTGRES_INTEGRATION=true`. Sin esa variable, las pruebas PostgreSQL no ejercitan la base aunque el runner las contabilice.
