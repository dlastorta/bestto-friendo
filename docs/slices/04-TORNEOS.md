# Slice 04 — Crear torneos en borrador

Estado: implementado y verificado localmente, pendiente de revisión e integración, 2026-11-25.

## Alcance

Crear, listar y consultar torneos en borrador asociados a un juego existente desde API y React. Mantener catálogo, identidad y biblioteca sin modificar sus reglas.

- Datos mínimos: nombre del torneo, juego, creador y estado `Draft`.
- Nombre entre 2 y 120 caracteres, recortado en extremos; no se exige unicidad.
- Juego obligatorio y existente. El cliente no determina el estado inicial ni el creador.
- Formatos avanzados se mantienen fuera de alcance: este slice vincula al juego, sin inventar un catálogo de formatos.
- No hay edición, eliminación, publicación, inicio, participantes, mazos presentados, resultados ni configuración de puntos/desempates en este slice.

## Permisos confirmados

Decisión del usuario (2026-11-25): solo usuarios con rol de organizador; borradores privados del creador.

- Rol `TournamentOrganizer`, independiente de `CatalogAdmin`. No se concede al registrarse y administrar el catálogo no concede organización.
- Crear/listar/consultar requiere sesión con el rol; solo se consultan borradores del perfil creador. Un organizador no accede a los de otro mediante IDs o filtros.
- La cookie revalida ambos roles contra el estado actual de la cuenta en cada petición. Revocar el rol impide crear y consultar torneos con sesiones existentes; no elimina los borradores. Reasignarlo recupera el acceso propio.
- `/api/me` expone `canOrganizeTournaments`; la interfaz usa ese permiso y desmonta el estado privado al cerrar/cambiar sesión o recibir `401`/`403`. El servidor siempre autoriza de nuevo.

### Asignación administrativa

Aplicar las migraciones; identificar el UUID de una cuenta existente y ejecutar con acceso administrativo a la base:

```sh
psql "$DATABASE_URL" -v account_id='UUID de la cuenta' -f scripts/grant-tournament-organizer.sql
```

El script es idempotente, asigna únicamente el rol de organizador y no crea cuentas ni contraseñas. No existe endpoint de autoasignación. Para revocar, el operador elimina la asociación de esa cuenta con `TournamentOrganizer` en `AspNetUserRoles`; nunca elimina el rol compartido.

## API y persistencia

| Método y ruta | Contrato |
|---|---|
| `POST /api/tournaments` | JSON `{ "name": "Copa local", "gameId": "UUID" }`; `201`, Location y detalle. Requiere CSRF. |
| `GET /api/tournaments?gameId={id}` | Lista propia, filtro de juego opcional, orden nombre/ID. |
| `GET /api/tournaments/{id}` | Detalle propio: `id`, `name`, `gameId`, `status` (`Draft`). |

Errores: `400` para metadatos inválidos, juego inexistente o CSRF; `401` anónimo; `403` sin rol; `404` para ajeno/inexistente. Respuestas de la feature con `Cache-Control: no-store`. Organizador y estado se determinan en el servidor; no se aceptan del cliente.

Migración `AddTournamentDrafts`: tabla `tournaments` con FK restrictivas a juego y perfil, índice organizador/juego y rol sembrado sin asignaciones. EF y Identity permanecen exclusivamente en Infrastructure. El estado solo contempla `Draft`; no se implementan transiciones futuras.

## Verificación prevista

Reglas puras, handlers con dependencias controladas, HTTP con sesión y CSRF, aislamiento entre cuentas, migración EF y PostgreSQL real, React con cleanup y limpieza del estado privado al cerrar sesión. Builds, auditorías NuGet/npm y límites entre capas.

## Evidencia local (2026-11-25)

- `dotnet restore TournamentManager.slnx` y `dotnet build TournamentManager.slnx --no-restore`: correctos, sin warnings ni errores.
- `RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx`: **49/49**, con PostgreSQL 16 real en base aislada `slice04_tests`, WSL Ubuntu 24.04 / .NET 10.
- Migraciones desde base vacía y `has-pending-model-changes`: correctos, sin cambios pendientes.
- HTTP aislado y PostgreSQL: rechazo anónimo y sin rol, independencia de `CatalogAdmin`, CSRF, juego válido, estado/creador derivados en servidor, nombres repetidos permitidos, aislamiento entre organizadores y revocación con cookie existente.
- `npm ci`, `npm test -- --run`: **19/19**; `npm run build`: correcto. La regresión de desmontaje detectó claves React duplicadas entre features privadas; se corrigieron con claves por feature/jugador.
- Auditoría NuGet con transitivas y npm: sin vulnerabilidades conocidas. Arquitectura y dependencias entre capas: correctas.
- Navegador con API/DB reales: login de organizador, creación de borrador, filtro por juego, consulta, recarga que conserva el torneo y logout que oculta torneos y biblioteca. El script administrativo asignó el rol correctamente a una cuenta local de prueba.
- Backend nativo Windows no ejecutado por el bloqueo conocido de Smart App Control; verificado en WSL. CI y revisión del PR pendientes al documentar esta evidencia. Continúan las limitaciones de despliegue público del Slice 02.

Para reproducir: configurar `ConnectionStrings__TournamentDb` a una base PostgreSQL descartable, ejecutar `dotnet tool restore`, `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api` y los tests con `RUN_POSTGRES_INTEGRATION=true`. Sin esa variable, las pruebas PostgreSQL no ejercitan la base aunque el runner las contabilice.
