# Slice 05 — Configuración de reglas de torneo

Estado: implementado y verificado localmente; pendiente de revisión, 2026-11-25.

## Alcance

El organizador propietario configura puntos por victoria/empate/derrota/bye, habilitación de byes y una lista ordenada de desempates. Solo se edita en `Draft`. Se mantienen los permisos privados del Slice 04.

Los torneos nuevos y existentes quedan **sin configurar** (`rules: null`, `rulesVersion: 0`); no se impone una puntuación por defecto. No se implementan inicio, inscripciones, clasificación, resultados ni algoritmos de emparejamiento.

## Política técnica del MVP

- Puntajes explícitos y no negativos, hasta dos decimales. Límite técnico de 1000000 por resultado; se usan `decimal` y columnas `numeric(9,2)`, sin redondeo silencioso ni comparaciones obligatorias entre victoria/empate/derrota.
- `byesEnabled` habilita/deshabilita los byes. El puntaje de bye se conserva al deshabilitarlos, pero no implica concederlos. Máximo **uno por participante y torneo**, fijo conforme a la regla confirmada. No se asignan byes todavía.
- Criterios disponibles: `Buchholz`, `OMWPercentage` y `BuchholzCut`. Se guarda el orden elegido, sin duplicados ni códigos desconocidos. La lista puede estar vacía; en ese caso no hay criterios adicionales para resolver empates.
- Los nombres identifican criterios futuros; **no se implementan ni se dan por definidas sus fórmulas**, el número/tratamiento de descartes, mínimos de OMW%, rondas no jugadas ni el criterio final si persiste empate. Se resolverán antes de ejecutar cálculos en los slices correspondientes.
- Cada guardado exitoso incrementa `rulesVersion`; un cliente desactualizado recibe `409`. Cabecera y reglas se guardan en una única transacción de infraestructura. El dominio rechaza cambios fuera de `Draft`; no se crea todavía una operación de inicio del torneo.

## API

`GET /api/tournaments` y `GET /api/tournaments/{id}` incorporan `rulesVersion` y `rules` (nullable). La configuración expone `maximumByesPerParticipant: 1`, que no es editable.

`PUT /api/tournaments/{id}/rules` requiere cookie de `TournamentOrganizer`, propiedad y `X-CSRF-TOKEN`:

```json
{
  "winPoints": 3,
  "drawPoints": 1,
  "lossPoints": 0,
  "byesEnabled": true,
  "byePoints": 3,
  "tieBreakers": ["Buchholz", "OMWPercentage"],
  "expectedVersion": 0
}
```

El ejemplo no define valores por defecto. Todos los campos son obligatorios, incluida la lista aunque esté vacía. Devuelve el detalle actualizado. `400`: datos inválidos/faltantes o CSRF; `401`: anónimo; `403`: sin rol; `404`: ajeno/inexistente; `409`: versión desactualizada o estado distinto de borrador. Respuestas de la feature sin caché.

UI: configuración desde el detalle del torneo; puntajes inicialmente vacíos, política explícita y controles para agregar/quitar/subir/bajar desempates. Un error conserva el formulario. «Recargar reglas (descarta cambios)» recupera la versión actual. `401`/`403` retiran la vista privada y revalidan el perfil.

## Persistencia y verificaciones

Migración `AddTournamentRules`: `RulesVersion` en `tournaments` y tabla opcional `tournament_rules`; relación 1:1 con PK/FK `TournamentId`. Orden guardado en `text[]`, mapeado a campo privado; EF no sale de Infrastructure.

- Backend WSL / .NET 10 con `RUN_POSTGRES_INTEGRATION=true`, PostgreSQL 16 real: **55/55**. Incluye concurrencia entre contextos, decimales, orden persistido, privacidad, CSRF y rechazo de cambios fuera de borrador.
- `npm ci`, tests **24/24** y build frontend correctos; npm sin vulnerabilidades.
- Restore y build .NET correctos. Auditoría NuGet directa/transitiva sin vulnerabilidades conocidas; modelo EF sin cambios pendientes y verificación de arquitectura correcta.
- Migración aplicada sobre base nueva y sobre la base local del Slice 04. Verificación manual en navegador: borrador existente sin configuración, guardado de puntos y política de bye, reordenamiento de desempates, persistencia al recargar y retiro de la vista privada al cerrar sesión.
- Windows nativo sigue afectado por Smart App Control; backend verificado en WSL. Se mantienen las limitaciones de despliegue público del Slice 02.

Para repetir: configurar `ConnectionStrings__TournamentDb` a una base descartable; ejecutar `dotnet tool restore`, `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api`, y `RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx`. Sin la variable, los tests PostgreSQL no ejercitan la base aunque el runner los contabilice.
