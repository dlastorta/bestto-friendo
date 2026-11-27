# Slice 09 — Tabla básica

Implementado y verificado el 2026-11-27 en `codex/slice-09-standings`, desde main después del PR #9.

## Alcance confirmado

- Tabla privada para organizador propietario con rol vigente y jugadores inscriptos. Revocar el rol no elimina el acceso como participante si existe inscripción propia.
- Reconstrucción desde revisión vigente de cada resultado y `StartedRules`; sin totales mutables persistidos. Partidas pendientes no suman y jugadores sin resultados tienen cero puntos.
- Empates comparten puesto (1, 1, 3). El ID solo estabiliza el orden de presentación, sin desempatar deportivamente.
- Correcciones reflejadas al consultar nuevamente, sin duplicar aportes del historial. No incluye actualización en tiempo real.
- No expone contenido ni referencias de mazos. No concede a los jugadores acceso a las rutas administrativas de partidas.
- Desempates, byes, publicación y emparejamientos quedan fuera del alcance.

## Contrato y diseño

`GET /api/tournaments/{id}/standings` requiere sesión. Devuelve una lista de `playerId`, `displayName`, `position`, `points`, con `Cache-Control: no-store`. Respuestas: 401 sin sesión, 404 ajeno/inexistente, 409 antes del inicio. No escribe datos ni requiere migraciones.

Domain calcula puntos y puestos; Application autoriza antes de consultar participantes/resultados. Infrastructure reutiliza las consultas existentes y carga reglas históricas para el acceso del participante. React consume la tabla en Mis torneos y Mis inscripciones, con consulta explícita, errores y desmontaje al perder acceso. Al recargar retira los datos anteriores para no mostrar una tabla obsoleta como actual.

## Verificaciones ejecutadas

- Restore, compilación y 75/75 tests backend en WSL/.NET 10 con PostgreSQL 16 real (`RUN_POSTGRES_INTEGRATION=true`), sin omitidos.
- Pruebas de decimales, ceros, pendientes, empate, forfeit y correcciones sin doble conteo; HTTP de organizador/participante/ajeno/anónimo, reglas históricas y privacidad.
- 40/40 tests frontend y build correctos. Auditorías NuGet directas/transitivas y npm sin vulnerabilidades conocidas detectadas. Arquitectura aprobada.
- Navegador con API/PostgreSQL reales: organizador y participante consultan el mismo empate con puesto 1 y un punto cada uno; tabla retirada al cerrar sesión.
- Sin dependencias nuevas ni cambios de esquema. Se reutilizó la base de pruebas aislada y migrada del Slice 08.

Comandos: `dotnet restore TournamentManager.slnx`, `RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx --no-restore`, `dotnet list TournamentManager.slnx package --vulnerable --include-transitive`, `python3 scripts/verify_structure.py`; en frontend, `npm test -- --run`, `npm run build`, `npm audit --audit-level=high`.

Windows Smart App Control sigue bloqueando tests backend nativos; se ejecutaron en WSL. La auditoría npm requirió acceso de red fuera del sandbox tras fallar DNS. Revisión e integración del PR pendientes; sin merge automático.
