# Slice 08 — Partidas manuales y resultados

Implementado el 2026-11-26 en `codex/slice-08-manual-matches`, desde `main` actualizado tras integrar el PR #8 (Slice 07).

## Alcance y decisiones

- Solo el organizador propietario puede crear, consultar y cargar/corregir partidas. Requiere torneo en curso y conserva la privacidad de listas y snapshots.
- Partidas con número de ronda positivo, dos jugadores distintos inscriptos y máximo una partida por jugador en esa ronda. La carga es manual: no abre/cierra rondas ni impide cargar una posterior antes de completar la anterior. No hay motor suizo ni prevención de revancha entre rondas todavía.
- Resultado: `PlayerOneWin`, `PlayerTwoWin` o `Draw`; tipo `Played` o `Forfeit`. Forfeit exige ganador y da puntos normales de victoria/derrota; no retira al participante del torneo.
- Cada carga conserva actor, fecha y revisión. Corregir requiere motivo de 1 a 500 caracteres y versión esperada. No modifica ni borra revisiones anteriores.
- Puntos derivados de `StartedRules` y del resultado vigente; no se aceptan puntos del cliente ni se almacenan acumulados mutables. Cambiar la configuración fuente no afecta los puntos históricos.
- El criterio del roadmap sobre recalcular clasificaciones se entrega en dos pasos: Slice 08 recalcula puntos por partida al corregir; Slice 09 construirá la tabla desde resultados vigentes. No se adelantó clasificación ni desempates.
- No incluye byes (Slice 11), doble derrota, marcador interno, anulación, edición de contendientes, eliminación, publicación ni reportes de jugadores. Cualquier futura anulación o cambio de semántica debe conservar auditoría e interpretación histórica.

## Contratos

Prefijo `/api/tournaments/{id}/matches`. Sesión, `TournamentOrganizer` y propiedad del torneo en todas las rutas; CSRF en escrituras. Actor siempre derivado de la sesión.

| Método / ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /` | — | Lista ordenada por ronda, fecha e ID, con resultado vigente y puntos |
| `GET /{matchId}` | — | Detalle con historial descendente |
| `POST /` | `round`, `playerOneId`, `playerTwoId` | `201`, detalle y Location |
| `POST /{matchId}/result` | `outcome`, `kind`, `reason`, `expectedVersion` | `200`, resultado vigente e historial |

`expectedVersion=0` para primera carga; después usar `version` del detalle. `reason` es nota opcional inicial (por defecto `Carga inicial`) y obligatorio al corregir. Cada revisión incluye `number`, `outcome`, `kind`, `reason`, `actorPlayerId`, `recordedAt`, `playerOnePoints` y `playerTwoPoints`. Los puntos se corresponden con los jugadores 1 y 2 de la partida.

Errores: `400` datos inválidos/CSRF; `401` sin sesión; `403` sin rol; `404` recurso ajeno/inexistente o partida de otro torneo; `409` torneo no iniciado, jugador ocupado en ronda o versión desactualizada. Respuestas privadas sin caché. No se devuelven contenido ni referencias de mazos.

## Persistencia y concurrencia

Migración `20261126213250_AddManualMatches`: agrega `MatchesVersion` al torneo y tablas de partidas/revisiones, sin alterar snapshots ni reglas existentes. EF y transacciones exclusivamente en Infrastructure.

La creación lee torneo antes de comprobar ocupación y modifica su token `MatchesVersion`; partida y token se confirman juntos. Dos creaciones concurrentes no pueden confirmar sobre la misma versión. La operación perdedora devuelve 409 y no deja una partida parcial. Este mecanismo se debe reutilizar en futuras creaciones automáticas.

Cada corrección incrementa `TournamentMatch.Version` y agrega una revisión en el mismo commit. Tanto conflictos de token como duplicación de `(MatchId, Number)` se traducen a 409. No hay reintento automático sobre datos que el usuario no revisó.

El downgrade rechaza ejecutarse si existen partidas para no perder historial. Tomar backup antes de migrar bases con datos y usar migraciones correctivas hacia adelante después de usar la funcionalidad.

## Verificaciones

- Backend .NET 10 en Ubuntu 24.04 / WSL, con PostgreSQL 16 real y `RUN_POSTGRES_INTEGRATION=true`: restore/build y **73/73 tests**, sin omitidos.
- Pruebas puras de estado, jugadores/ronda, duplicados, empate/forfeit, versiones, motivo y conservación de historial/puntos.
- HTTP real: permisos, CSRF, accesos ajenos, revocación del rol con cookie existente, reglas históricas independientes y ausencia de listas privadas.
- PostgreSQL: creaciones y correcciones concurrentes con un solo commit exitoso y sin escritura parcial.
- Frontend: `npm ci`, **37/37 tests**, build y auditoría npm. NuGet directo/transitivo y npm sin vulnerabilidades conocidas detectadas.
- Arquitectura aprobada y modelo EF sin cambios pendientes. Migración aplicada en base nueva y en base local con torneo ya iniciado.
- Navegador: crear partida, cargar victoria (3/0), corregir a empate (1/1), conservar ambas versiones tras consultar nuevamente, rechazar duplicación en ronda y desmontar la vista privada al cerrar sesión.

Comandos reproducibles, con base de pruebas creada y conexión configurada:

```sh
dotnet restore TournamentManager.slnx
dotnet build TournamentManager.slnx --no-restore
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api
RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx --no-restore
dotnet list TournamentManager.slnx package --vulnerable --include-transitive
python3 scripts/verify_structure.py
cd frontend
npm ci
npm test -- --run
npm run build
npm audit --audit-level=high
```

Windows Smart App Control sigue bloqueando los tests backend nativos; se verificaron en WSL sin desactivar protecciones. Para reinstalar npm se detuvo el Vite local que mantenía un archivo abierto. No hay dependencias nuevas. La revisión e integración del PR quedan pendientes; no se realiza merge automático.
