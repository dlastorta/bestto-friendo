# Arquitectura — Tournament Manager

Estado: Slice 02 (identidad mínima y autorización). La separación estricta de proyectos se mantiene desde el bootstrap.

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

## Fuera de alcance de este slice

Biblioteca de mazos, invitados, torneos, formatos avanzados, partidas, puntaje, desempates, byes, estadísticas, publicación y Elo. Ver `docs/ROADMAP.md`. La edición de juegos existentes requiere definir una política de impacto sobre referencias históricas y queda postergada.
