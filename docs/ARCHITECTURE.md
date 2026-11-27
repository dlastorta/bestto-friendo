# Arquitectura — Tournament Manager

Estado: Slice 09 (tabla básica). La separación estricta de proyectos se mantiene desde el bootstrap.

## Decisiones

- **Monorepo:** `backend/`, `frontend/`, `docs/`, `scripts/`.
- **Backend:** ASP.NET Core 10 Minimal API, con proyectos separados `Domain`, `Application`, `Infrastructure`, `Api` y `Tests` para forzar límites de dependencias.
- **Frontend:** React 19 + TypeScript + Vite 8, test de componentes con Vitest + Testing Library.
- **Base de datos:** PostgreSQL (desarrollo vía Compose) + EF Core 10, proveedor Npgsql 10. Implementación exclusivamente dentro de `TournamentManager.Infrastructure`.
- **Contratos:** API bajo `/api`; OpenAPI JSON en `/openapi/v1.json` en desarrollo.
- **Desarrollo local:** Vite en `http://localhost:5173` con proxy `/api` hacia API en `http://localhost:5080`. Se evita configuración CORS redundante en este flujo.
- **Verificación de salud:** `/api/health/live` no depende de DB y `/api/health/ready` consulta conectividad real a PostgreSQL.
- **Migraciones:** `InitialBaseline` no contiene tablas: es el punto de inicio de EF Core para esquemas que lleguen en slices futuros. Ejecutar `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api`. El startup API conserva una referencia `Microsoft.EntityFrameworkCore.Design` **solo** para la herramienta; el código de la API no usa EF.
- **Configuración:** conexión de desarrollo en `appsettings.Development.json`, reemplazable con `ConnectionStrings__TournamentDb` en otros entornos.

## Separación estricta de capas

```text
API (endpoints HTTP, composition root)
 |  \
 |   \---> Infrastructure (EF Core, PostgreSQL, migraciones, adaptadores)
 v                |                  |
Application <-----+                  |
 |                                   |
 v                                   v
Domain <-----------------------------+
```

- `Domain` no referencia ningún proyecto interno ni bibliotecas de persistencia. Sus invariantes no conocen EF Core, ASP.NET, ni PostgreSQL.
- `Application` referencia solamente `Domain`. Los casos de uso consumen **puertos** (interfaces) propios; no exponen tipos EF ni devuelven `IQueryable`.
- `Infrastructure` implementa esos puertos y referencia `Application`/`Domain`. Aquí viven `DbContext`, `DbSet`, configuraciones Fluent API, migraciones, consultas y commits.
- `Api` transforma HTTP en llamadas a Application; importa `Infrastructure` **únicamente** para registrar adaptadores en `Program.cs`/composition root. Ningún endpoint consulta EF directamente.
- La prueba de estructura verifica project references y prohíbe APIs de EF fuera de infraestructura y tooling. Los tests de arquitectura verifican referencias compiladas cuando se puede ejecutar .NET.

Esta separación convive con *Vertical Slice Architecture*: una feature puede abarcar un endpoint API, caso de uso Application, reglas Domain y adaptador Infrastructure, sin importar el código de una capa superior o de una implementación concreta.

**Repository y Unit of Work:** `DbSet` y `DbContext` ya realizan gran parte de esas tareas dentro de Infrastructure. No se aceptan como contratos en Application; tampoco se exige un `IRepository<T>` genérico. Se definen puertos específicos (ej. `IDeckReader`) al implementar el primer caso de uso que los necesite. Si luego una operación necesita una transacción de varias escrituras, se define una abstracción acotada para el commit y se implementa con EF dentro de infraestructura. `SaveChangesAsync` nunca en casos de uso.

**Ejemplo ya implementado en Slice 00:** `/api/health/ready` recibe `IDatabaseReadinessProbe` (puerto en `Application`). `PostgresReadinessProbe` en `Infrastructure` usa `TournamentDbContext` para consultar la conexión. Los endpoints no importan EF.

## Estilo por slices

Cuando existan features, agrupar cada caso de uso por capacidad de negocio **dentro de su proyecto/límite** (endpoint en API, handler en Application, reglas en Domain y adapter en Infrastructure). Extraer objetos y servicios compartidos solo cuando haya reutilización real o una regla transversal confirmada.

No existe `IRepository<T>`, bus de mensajes ni abstracción de scoring/pairing todavía. Agregarlos solo en el slice que los necesite; las interfaces de persistencia deben representar necesidades de Application, no EF Core.

## Pruebas

- La suite .NET utiliza `WebApplicationFactory` para validar la API en memoria; el test de liveness no requiere PostgreSQL. También prueba readiness con un puerto falso, sin EF.
- La suite React valida el render de la pantalla y el estado de la API simulando respuestas HTTP.
- Prueba manual de readiness: con Compose en ejecución devuelve 200; sin conexión devuelve 503.

## Implementación en Slice 01

- `Domain/Games`: `Game` es agregado de `GameFaction`. Expone `EnsureValidFactionSelection`, validación pura para el futuro uso de mazos. No conoce EF.
- `Application/Features/Games`: casos de uso Create/List/Get y puerto `IGameCatalog` con métodos puntuales, sin tipos EF ni `IQueryable`. El puerto es un contrato de aplicación, no un `IRepository<T>` genérico.
- `Infrastructure/Games/EfGameCatalog`: consultas y comandos con EF; captura violación de índice único de PostgreSQL para devolver conflicto de dominio. `TournamentDbContext` configura tablas, relaciones e índices con Fluent API.
- `Api/Features/Games`: endpoints HTTP sin referencia a Infrastructure. `Program.cs` compone handlers + adaptadores.
- `frontend/src/features/games`: formulario y catálogo consumiendo `/api/games`, sin replicar reglas de negocio como autoridad.

La migración `AddGamesCatalog` crea `games` y `game_factions`. El índice de nombre normalizado evita carreras de duplicación entre requests. El agregado valida cardinalidad y pertenencia de facciones, todavía no se asignan a mazos porque no existen hasta Slice 03.

**Evolución en Slice 02:** consultar juegos sigue siendo público; crearlos exige sesión, `CatalogAdmin` y CSRF. La versión sigue siendo local mientras se completan los flujos de cuenta y la configuración operativa de seguridad.

## Implementación en Slice 02

- `Domain/Players/Player`: perfil con identidad propia y cuenta asociada; valida el nombre visible sin conocer credenciales, HTTP o persistencia.
- `Application/Features/Identity`: contratos `IAccountAccess`, `IPlayerProfiles`, `ICurrentAccount`, registro y consulta/edición del perfil autenticado. No se recibe un ID de dueño desde el cuerpo HTTP.
- `Infrastructure/Identity`: ASP.NET Core Identity con `UserManager`/`SignInManager` para credenciales, normalización, hash y lockout. Cuenta y perfil se registran en una transacción. `TournamentDbContext` deriva de `IdentityDbContext`; toda la persistencia de Identity permanece aquí.
- La migración `AddPlayerIdentity` agrega tablas de Identity, perfiles e índice único de email. Siembra solo el rol `CatalogAdmin`, sin usuarios ni credenciales. El operador lo asigna mediante `scripts/grant-catalog-admin.sql`.
- `Api/Features/Identity`: cookies, CSRF y traducción de respuestas. `CurrentAccount` extrae la identidad de la sesión. La cookie se valida consultando existencia, bloqueo y permisos actuales de la cuenta en cada petición. `Program.cs` configura autenticación, política `ManageCatalog` y límite de intentos por IP.
- React presenta registro/login/logout/perfil y oculta la creación de juegos sin permiso. El backend vuelve a autorizar cada escritura; ocultar controles no sustituye autorización.
- No se almacenan credenciales ni tokens de sesión en localStorage. La cookie es HttpOnly y el token CSRF se obtiene antes de escribir, con `credentials: same-origin`.

Ver contratos, límites de sesión, asignación del administrador y pruebas en [`slices/02-IDENTIDAD.md`](slices/02-IDENTIDAD.md). Google es mejora futura; no se introduce OAuth ni vinculación de cuentas ahora.

## Implementación en Slice 03

- `Domain/Decks`: agregado `Deck` con revisiones inmutables de nombre, lista literal y facciones; reutiliza las invariantes de `Game`. Sin parsing de cartas.
- `Application/Features/Decks`: `DeckLibraryHandler` resuelve el propietario desde `ICurrentAccount` y `IPlayerProfiles`; `IDeckLibrary` consulta siempre por dueño. No hay bypass administrativo.
- `Infrastructure/Decks`: consultas y commits EF. `Version` es token de concurrencia; `SaveChangesAsync` confirma revisión y cabecera en la misma transacción. Un conflicto no modifica el historial.
- API autenticada bajo `/api/decks`, CSRF en escrituras, `404` indistinguible entre ajeno/inexistente y respuestas privadas sin caché.
- React desmonta el estado privado al cambiar sesión; permite crear/editar/archivar y consultar revisiones. Las listas se muestran como texto escapado, nunca HTML.

Ver [contratos y verificación](slices/03-BIBLIOTECA.md).

## Fuera de alcance de este slice

Invitados, formatos avanzados, clasificación/desempates, asignación de byes, estadísticas, publicación y Elo. Ver `docs/ROADMAP.md`. La edición de juegos existentes requiere definir una política de impacto sobre referencias históricas y queda postergada.

## Implementación en Slice 08

- `TournamentMatch` valida estado, jugadores distintos y ocupación de ronda. Sus resultados son revisiones inmutables (`MatchResultRevision`) con tipo, resultado, motivo, actor y fecha; `CurrentResult` selecciona la última. Los puntos se calculan con `FrozenTournamentRules`, nunca con la configuración fuente.
- `TournamentMatchesHandler` verifica sesión, rol y propiedad antes de consultar partidas; puertos existentes para torneo/inscripciones y `ITournamentMatches` específico para persistencia. Ninguna entrada permite elegir actor ni puntos.
- Infrastructure confirma creación y `Tournament.MatchesVersion` en una transacción. Ese token serializa creaciones del torneo y evita que lecturas simultáneas acepten el mismo jugador en una ronda, incluso si cambia de lado. Todo futuro flujo de creación (incluido suizo) debe respetar esta protección; no basta consultar antes de insertar.
- Corrección: `TournamentMatch.Version` y clave `(MatchId, Number)` protegen la concurrencia. Conflicto de actualización o revisión duplicada devuelve `409`, con rollback completo. EF y SQL permanecen en Infrastructure.
- Migración `AddManualMatches`: tablas `tournament_matches` / `match_result_revisions` y token `MatchesVersion`. No transforma reglas ni snapshots. El downgrade bloquea pérdida de partidas existentes.
- React incorpora partidas en detalle de torneo en curso, selección de jugadores, resultado, corrección e historial. Ante 401/403 desmonta la vista privada; ante 409 conserva el formulario para recargar explícitamente. Ver [contratos y verificación](slices/08-PARTIDAS.md).

## Implementación en Slice 07

- `Tournament.Start` valida reglas, mínimo de dos participantes y todas las presentaciones; captura `FrozenTournamentRules`, fecha y estado `InProgress`. Puntos, política de bye (incluido máximo) y secuencia de desempates se copian por valor en `tournament_started_rules`, con versión de origen. Las lecturas posteriores y futuros cálculos deben usar `StartedRules`.
- El handler autoriza al organizador propietario y obtiene conteos por puertos. Infrastructure confirma inicio y reglas históricas en un único `SaveChangesAsync`; `Status`, `RulesVersion` y `ParticipationVersion` son tokens de concurrencia. Una inscripción, reemplazo o modificación de reglas concurrente invalida la otra operación sin commits parciales.
- La inscripción conserva una colección de snapshots inmutables, identificados por inscripción y número; la presentación vigente es la última. Reemplazar agrega una fila con actor, fecha y motivo, nunca actualiza la anterior. Historial y contenido siguen siendo exclusivos del jugador.
- `AddTournamentStartAndSubmissionHistory` preserva las presentaciones previas como versión 1. Su downgrade rechaza perder reglas históricas o reemplazos; ver instrucciones operativas en [Slice 07](slices/07-INICIO.md).
- React confirma el inicio con las versiones observadas, muestra reglas históricas y bloquea edición después del inicio. El backend vuelve a validar cada operación. No se versionan fórmulas aún inexistentes: cuando se incorporen, su evolución deberá preservar la semántica de torneos históricos.

## Implementación en Slice 06

- `TournamentRegistration` permite inscripción sin presentación; `TournamentDeckSnapshot` copia revisión y texto literal con facciones en una colección defensiva. La presentación valida dueño, juego, archivo, versión y estado de torneo.
- `TournamentRegistrationsHandler` resuelve siempre el perfil desde sesión. Distingue DTO administrativo (jugador/estado) de presentación privada. El organizador no tiene un bypass al snapshot.
- `ITournamentRegistrations` ofrece consultas específicas y commit; Infrastructure implementa EF. Cada inscripción/presentación modifica `ParticipationVersion` del torneo y se confirma junto con el registro/snapshot en una transacción. Un índice único evita inscripciones duplicadas.
- El enlace aleatorio se habilita explícitamente y solo expone datos mínimos de invitación a una cuenta autenticada. No habilita endpoints administrativos. `RegistrationToken` tiene índice único y control de concurrencia.
- React separa controles administrativos y «Mis inscripciones». La sesión determina sus claves de montaje; todas las escrituras usan CSRF.
- Migración `AddTournamentRegistrations`, snapshots con FK restrictiva a revisión de origen. La lectura privada no reconstruye contenido desde el mazo vivo. Ver [contratos y pruebas](slices/06-INSCRIPCIONES.md).

## Implementación en Slice 05

- `Tournament` controla edición en borrador y versión; `TournamentRules` valida puntos y códigos/duplicados de una secuencia ordenada con copia defensiva.
- `TournamentDraftsHandler` conserva autorización/propiedad y recibe un comando explícito de configuración. Campos faltantes no se convierten silenciosamente en cero o false.
- El puerto existente agrega `SaveAsync`. El adaptador carga reglas y guarda cabecera/configuración en una transacción; `RulesVersion` es token de concurrencia. La creación simultánea de la primera configuración también se traduce a conflicto.
- Migración `AddTournamentRules`, relación opcional 1:1, decimales y array de códigos. No hay reglas por defecto ni EF en capas internas.
- React incorpora editor de reglas en el detalle, con cambios de orden, conflictos conservando formulario y recarga explícita. Los cálculos de clasificación no forman parte de esta feature.

## Implementación en Slice 04

- `Domain/Tournaments/Tournament`: creación de borradores con nombre y referencias válidas, sin HTTP, EF ni reglas anticipadas de torneo.
- `Application/Features/Tournaments`: `TournamentDraftsHandler` verifica rol, resuelve perfil y juego; `ITournamentDrafts` ofrece Add/ListOwned/FindOwned sin tipos de persistencia.
- `Infrastructure/Tournaments/EfTournamentDrafts`: filtros de propiedad en SQL y persistencia; migración `AddTournamentDrafts` con FK restrictivas y rol `TournamentOrganizer` sin cuentas/asignaciones.
- API bajo `/api/tournaments`: política de organizador, CSRF, `404` para ajenos y errores de validación. Ambos roles se reconstruyen al validar cada cookie; no se confía en roles antiguos del ticket.
- Perfil HTTP incorpora `canOrganizeTournaments`. React muestra creación/listado/detalle solo con ese permiso y elimina borradores visibles ante `401` o `403`, reconsultando la sesión. Los componentes privados tienen claves distintas por feature y jugador para desmontarlos correctamente.

Ver [contratos, asignación administrativa y verificación](slices/04-TORNEOS.md).

## Implementación en Slice 09

TournamentStandings calcula puntos y puestos compartidos en Domain. StandingsHandler autoriza organizador propietario con rol vigente o inscripción propia antes de consultar datos. Reutiliza puertos existentes y StartedRules; no persiste acumulados ni modifica el esquema. React muestra la respuesta sin calcular puntuación y retira la tabla antes de recargar. GET /api/tournaments/{id}/standings exige sesión y devuelve no-store. No concede acceso a partidas administrativas ni listas de otros jugadores.
