# Tournament Manager

Organizador de torneos multijuego, desarrollado por **Vertical Slices** en un monorepo con separación estricta entre **Domain, Application, Infrastructure y API**. El roadmap aprobado contempla biblioteca personal de mazos, snapshot al inscribirse, suizo, desempates ordenados, publicación y metajuego, pero se implementa una funcionalidad por vez.

## Estado — Slice 06: Inscripciones y snapshot de mazos

Implementado en código:

- Backend .NET 10 / ASP.NET Core Minimal API; catálogo para **crear, listar y consultar juegos** con facciones/clanes/colores y cardinalidad mínima/máxima.
- Dominio puro que valida nombres, facciones únicas y pertenencia a un juego. Nada de EF en Domain ni Application.
- Adaptador de persistencia EF Core / PostgreSQL y migración `AddGamesCatalog` en Infrastructure.
- UI React + TypeScript + Vite con formulario y listado que persisten en la API.
- Tests de reglas, endpoints desacoplados y prueba integrada contra PostgreSQL, habilitada en CI.
- Bootstrap anterior: health checks, OpenAPI, Docker Compose, checks arquitectónicos y GitHub Actions.

El Slice 02 agrega registro por email/contraseña, sesión por cookie HttpOnly y perfil propio. Crear juegos requiere el rol `CatalogAdmin`; registrarse no concede ese rol. Las lecturas del catálogo siguen siendo públicas. Ver [contratos, asignación del administrador y límites de seguridad](docs/slices/02-IDENTIDAD.md).

**No desplegar esta versión públicamente:** faltan verificación de email, recuperación de cuenta y preparación operativa de seguridad. El login con Google está registrado como mejora futura.

El Slice 03 agrega biblioteca privada: crear, consultar, editar y archivar mazos por juego. Cada edición conserva una revisión; la lista es texto libre para mostrar, sin controles de cartas ni cantidades (`N x Nombre de la carta` es solo una sugerencia). Solo el dueño puede acceder, incluidos frente a administradores del catálogo. Aplicar la migración `AddDeckLibrary` con el comando habitual de abajo. Ver [contratos, concurrencia y verificaciones](docs/slices/03-BIBLIOTECA.md).

El Slice 04 permite crear/listar/consultar torneos borrador de un juego válido. Requiere el rol **`TournamentOrganizer`**, separado de `CatalogAdmin` y sin asignación automática al registrarse. Los borradores son privados del creador; no incluyen aún inscripciones ni puntuación. Aplicar `AddTournamentDrafts` y asignar el rol con `scripts/grant-tournament-organizer.sql`. Ver [contratos, permisos y verificaciones](docs/slices/04-TORNEOS.md). Swagger UI queda pendiente para el final; OpenAPI JSON está disponible en desarrollo.

El Slice 05 agrega configuración de puntos por resultado, política de bye y orden de desempates desde el detalle del torneo, solo para el organizador propietario y en borrador. Los torneos quedan inicialmente sin configurar y cada guardado controla la versión para evitar sobrescrituras. Aplicar `AddTournamentRules`. No calcula clasificaciones, fórmulas ni asigna byes todavía. Ver [contratos y verificación](docs/slices/05-REGLAS.md).

El Slice 06 permite compartir un enlace de inscripción, anotarse con una cuenta sin mazo y presentarlo después. Se conserva una copia privada independiente de la biblioteca; solo el jugador ve su lista y el organizador únicamente el estado de presentación. El mazo será obligatorio para iniciar en Slice 07. Aplicar `AddTournamentRegistrations`. Ver [contratos, privacidad y verificaciones](docs/slices/06-INSCRIPCIONES.md).

Documentos: [`AGENTS.md`](AGENTS.md), [`docs/ROADMAP.md`](docs/ROADMAP.md), [`docs/TOURNAMENT_RULES.md`](docs/TOURNAMENT_RULES.md), [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`docs/slices/01-CATALOGO.md`](docs/slices/01-CATALOGO.md).

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 22.12+](https://nodejs.org/) con npm
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) o Engine + Compose

## Cómo ejecutar localmente

**Desde la carpeta raíz**, en tres terminales:

```sh
# Terminal 1: PostgreSQL

docker compose up -d postgres
```

```sh
# Terminal 2: API (aplicar las migraciones antes de ejecutar)

dotnet restore TournamentManager.slnx
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api
dotnet run --project backend/TournamentManager.Api
```

```sh
# Terminal 3: Frontend

cd frontend
npm install
npm run dev
```

Abrir [http://localhost:5173](http://localhost:5173); la API escucha en `http://localhost:5080`. El proxy de Vite reenvía `/api` sin configuración CORS adicional.

**La base no se crea automáticamente al arrancar**: aplicar migraciones explícitamente. `AddGamesCatalog` crea juegos/facciones y `AddPlayerIdentity` agrega cuentas, perfiles y el rol de administrador, sin sembrar credenciales.

## Ejemplo de API

```sh
curl http://localhost:5080/api/games
curl http://localhost:5080/api/health/live
curl http://localhost:5080/api/health/ready
```

| Ruta | Acción | Resultado |
|---|---|---|
| `POST /api/games` | Crea juego con facciones (administrador + CSRF) | `201` + `Location`; `400` inválido; `401` sin sesión; `403` sin permiso; `409` duplicado |
| `GET /api/games` | Lista juegos + facciones | `200` |
| `GET /api/games/{id}` | Consulta juego | `200` o `404` |
| `GET /api/health/live` | Estado API | `200` |
| `GET /api/health/ready` | Estado PostgreSQL | `200` o `503` |
| `GET /openapi/v1.json` | OpenAPI (Development) | `200` |

El identificador de cada facción es un GUID propio del juego. La selección de facciones concretas al crear mazos llegará en el Slice 03. Las reglas avanzadas de afiliación por formato siguen pendientes.

## Arquitectura

- **Domain:** agregados y reglas puras; no sabe qué es EF Core, ASP.NET ni PostgreSQL.
- **Application:** casos de uso y puertos como `IGameCatalog` con operaciones acotadas; no expone `DbContext`, `IQueryable` ni `DbSet`.
- **Infrastructure:** EF, mapeos Fluent API, migraciones e implementaciones de puertos. `SaveChangesAsync` se ejecuta aquí.
- **Api:** endpoints HTTP delgados; se conecta a Infrastructure **solo** en la raíz de composición (`Program.cs`).

No hay repositorio genérico ni Unit of Work artificial. Los tests arquitectónicos y el script de verificación comprueban estos límites.

## Pruebas

```sh
python scripts/verify_structure.py

dotnet test TournamentManager.slnx

cd frontend
npm install
npm test
npm run build
```

La prueba de integración real de PostgreSQL se ejecuta en CI con `RUN_POSTGRES_INTEGRATION=true`, PostgreSQL aislado y migraciones aplicadas. Para replicarla localmente con la base creada, usar:

```sh
# PowerShell
$env:RUN_POSTGRES_INTEGRATION="true"
dotnet test TournamentManager.slnx

# Linux/macOS (alternativa)
RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx
```

Si se configura otra base, definir `ConnectionStrings__TournamentDb` para la aplicación. No usar credenciales de Compose fuera de desarrollo.

En este equipo, Windows Smart App Control bloquea la DLL de dominio en los tests nativos. El backend se verifica con Ubuntu 24.04 en WSL y PostgreSQL aislado; el frontend se verifica con Node en Windows. No es necesario desactivar Smart App Control.

## Siguiente slice

**03 — Biblioteca personal de mazos:** asociar mazos al dueño autenticado y probar que otra cuenta no puede editarlos. No se adelantó código de mazos en Slice 02.

### Política de seguridad de dependencias

La auditoría NuGet está habilitada para paquetes directos y transitivos. Los avisos de severidad alta (`NU1903`) y crítica (`NU1904`) se tratan como errores en `restore` y CI. La API fija `Microsoft.OpenApi` 2.7.5 para evitar la versión transitoria vulnerable.

Si ya descargaste una versión anterior del ZIP, podés aplicar el cambio en tu copia local actualizando `Microsoft.OpenApi`, la herramienta `dotnet-ef` y las propiedades de `Directory.Build.props` como se documenta en el commit de seguridad. No es necesario volver a aplicar las migraciones por estos cambios.
