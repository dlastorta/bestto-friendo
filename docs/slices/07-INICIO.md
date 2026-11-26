# Slice 07 — Comenzar torneo

Implementado y verificado el 2026-11-26 en `codex/slice-07-start-tournament`. El trabajo comenzó sobre el Slice 06 y se rebasó sobre `main` después de integrar el PR #7, sin diferencias de código en la base. El PR del Slice 07 apunta directamente a `main`.

## Comportamiento confirmado

- El organizador propietario inicia desde API y React con confirmación explícita: reglas configuradas, mínimo dos jugadores y todos los mazos presentados.
- Se persisten estado `InProgress`, fecha y copia independiente de las reglas: puntos, política de bye, máximo por participante y orden de desempates, con versión de origen. Los futuros cálculos deben usar `StartedRules`, nunca configuración viva. Las futuras fórmulas necesitarán preservar su semántica histórica cuando evolucionen.
- El inicio bloquea nuevas inscripciones, presentaciones, reemplazos y edición de reglas. No hay segundo inicio ni vuelta a borrador. No publica el torneo.
- Antes del inicio, solo el jugador puede reemplazar su presentación por un mazo propio activo y compatible. Exige motivo de 1 a 500 caracteres y conserva actor, fecha, contenido y todas las versiones anteriores. El texto de cartas sigue siendo literal, sin controles.
- Solo el jugador ve la presentación y su historial. El organizador únicamente ve si presentó mazo. Editar o archivar la biblioteca no modifica snapshots.
- No incluye rondas, partidas, resultados, clasificación, publicación, asignación de byes ni algoritmos suizos.

## Contratos HTTP

Todas las rutas exigen sesión; escrituras con CSRF. `{id}` es el ID del torneo, incluso bajo `/registrations`.

| Ruta | Permiso y cuerpo | Respuesta |
|---|---|---|
| `POST /api/tournaments/{id}/start` | Organizador propietario; `expectedRulesVersion`, `expectedParticipationVersion` | `200` detalle con `status`, `startedAt`, `startedRules`; `400` condiciones incompletas; `409` versión/estado incompatible |
| `POST /api/registrations/{id}/deck/replace` | Jugador inscripto; `deckId`, `expectedDeckVersion`, `expectedPresentationVersion`, `reason` | `200` presentación vigente; `400` datos inválidos; `409` versión/estado incompatible |
| `GET /api/registrations/{id}/deck/history` | Solo el jugador de la sesión | `200` versiones de más reciente a más antigua |

Sin sesión: `401`; sin rol para inicio: `403`; ajeno/inexistente: `404`. Ningún cuerpo acepta un dueño o actor. DTO de presentación incorpora `number`, `actorPlayerId` y `reason`. Listado/detalle propios incorporan estado del torneo. El historial no concede acceso a la biblioteca de otro jugador.

## Persistencia y concurrencia

Domain conserva las invariantes; Application autoriza y usa puertos específicos existentes. Infrastructure confirma las escrituras mediante una transacción de `SaveChangesAsync`. `Status`, `RulesVersion` y `ParticipationVersion` detectan carreras entre inicio, reglas, inscripción y presentación. Un commit invalida la operación desactualizada; el usuario debe recargar y revisar antes de reintentar.

Migración `20261126112443_AddTournamentStartAndSubmissionHistory`:

- Agrega fecha y tabla `tournament_started_rules` con copia por valor, sin dependencia del contenido vivo de reglas.
- Cambia snapshots a clave compuesta inscripción/número y conserva las presentaciones existentes como número 1, actor obtenido de su inscripción y motivo `Presentación inicial`; conserva contenido y fecha originales.
- El downgrade rechaza ejecutarse si hay torneos iniciados o reemplazos, para evitar pérdida histórica. Usar backup probado antes de migrar entornos con datos y una migración correctiva hacia adelante si ya se utilizó la funcionalidad.

Aplicar con el comando habitual:

```sh
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api
```

## Verificaciones ejecutadas

- Backend en Ubuntu 24.04 / WSL, .NET 10, PostgreSQL 16 aislado: restore, build y **68/68 tests**, sin omitidos, con `RUN_POSTGRES_INTEGRATION=true`.
- Incluye HTTP de permisos/CSRF/privacidad, inicio inválido, versiones obsoletas, reemplazo e historial, bloqueo posterior y copia de reglas independiente aunque cambie la fila original.
- Concurrencia PostgreSQL: inicio frente a reglas, inscripción y reemplazo, en ambos órdenes de commit, sin persistencia parcial.
- Migraciones aplicadas a base nueva y base local existente; la presentación previa conservó texto y fecha. `has-pending-model-changes` sin cambios pendientes.
- Frontend: `npm ci`, **33/33 tests** y build. Regresión para desmontar confirmación de inicio y bloquear controles tras la transición.
- Auditorías NuGet directas/transitivas y npm: sin vulnerabilidades conocidas detectadas. Arquitectura: `scripts/verify_structure.py` aprobado.
- Navegador contra API y PostgreSQL reales: reemplazo e historial privado, rechazo de inicio con un jugador, rechazo con mazo faltante, inicio con dos presentaciones, estado persistido y bloqueo de controles posteriores.

Comandos reproducibles (con base de pruebas creada y migrada, configurando `ConnectionStrings__TournamentDb`):

```sh
dotnet restore TournamentManager.slnx
dotnet build TournamentManager.slnx --no-restore
RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx --no-restore
dotnet list TournamentManager.slnx package --vulnerable --include-transitive
python3 scripts/verify_structure.py
cd frontend
npm ci
npm test -- --run
npm run build
npm audit --audit-level=high
```

Windows Smart App Control impide ejecutar los tests backend nativos de este equipo; se verificaron en WSL sin desactivar esa protección. El frontend se verificó en Windows. Sigue pendiente la revisión e integración del PR del Slice 07; no se realizó merge automático. Se mantienen los límites de seguridad de cuentas del Slice 02 y Google/Swagger UI fuera de este alcance.
