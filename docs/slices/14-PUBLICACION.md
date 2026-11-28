# Slice 14 — Publicación

## Decisiones confirmadas

- Solo el organizador propietario con rol vigente publica un torneo finalizado o retira su publicación. Consulta anónima mediante enlace; publicar no implica desplegar la API en Internet.
- Página pública: nombre del torneo, juego, fechas, nombres visibles de jugadores, clasificación, cruces, resultados y byes.
- Cada jugador autoriza explícitamente compartir su propia presentación (nombre y lista literal), privada por defecto. Puede retirar autorización; nunca concede acceso a su biblioteca ni a presentaciones anteriores. Reemplazar una presentación borra el permiso anterior y requiere nueva autorización.
- Reabrir retira automáticamente la publicación en la misma operación. Un nuevo cierre no republica: exige otra acción explícita del organizador.

## Implementación

`PublishedAt` es independiente de Status. `PublicationVersion` coordina publicación/retiro y reapertura con los tokens anteriores. La inscripción guarda el número de snapshot autorizado, versión de visibilidad y fecha del último cambio, con dueño derivado de sesión.

`IPublishedTournaments` obtiene una lectura consistente en transacción RepeatableRead. Solo consulta texto de snapshots autorizados; no lee listas privadas para preparar la respuesta. Application calcula la clasificación con reglas congeladas y asignaciones vigentes. DTO público sin email, cuenta, token de inscripción, referencia de biblioteca, historial de correcciones o motivos administrativos.

`/#torneo={id}` muestra una página independiente sin login ni consultas privadas. Todas las respuestas públicas usan no-store; recargar retira resultados si el torneo dejó de estar publicado. Retirar publicación o permiso evita nuevas consultas, pero no puede retirar copias que terceros ya hayan guardado.

## Contratos

- `GET /api/public/tournaments/{id}`: anónimo, 404 indistinguible para inexistente/no publicado; devuelve únicamente datos permitidos.
- `POST /api/tournaments/{id}/publication`: `published`, `expectedPublicationVersion`, `expectedLifecycleVersion`, todos obligatorios. Organizador propietario y CSRF.
- `POST /api/registrations/{id}/deck/visibility`: `visible`, `snapshotNumber`, `expectedVersion`, obligatorios. Sesión propia y CSRF; no acepta dueño elegido por cliente.
- DTO administrativo agrega `publishedAt`/`publicationVersion`; presentación propia agrega `deckIsPublic`/`visibilityVersion`.
- Conflictos devuelven 409 y requieren recarga; no hay reintentos silenciosos.

## Verificación

Verificación local completada el 28 de noviembre de 2026:

- Backend en WSL Ubuntu con PostgreSQL real: restore y 118/118 tests aprobados, incluyendo concurrencia publicación/reapertura y consentimiento/reemplazo en ambos órdenes de commit. No se ejecutó el backend nativo de Windows por el bloqueo de Smart App Control.
- Frontend: npm ci, 57/57 tests y build TypeScript/Vite aprobados.
- Auditorías NuGet (incluidas transitivas) y npm: sin vulnerabilidades reportadas.
- Verificación de capas aprobada; EF no detecta cambios de modelo pendientes. Migración AddTournamentPublication aplicada correctamente, con torneos y presentaciones existentes privados por defecto.
- Verificación manual en navegador local: publicación de torneo finalizado, tabla y resultados públicos, ausencia inicial de listas, autorización de la presentación propia y revocación con desaparición de la lista pública. La reapertura y retirada automática se verificaron mediante tests HTTP y PostgreSQL.
- CI del PR pendiente. No se verificó rollback de la migración. Sin nuevas dependencias ni cambios de fórmulas históricas.

No despliegue a Internet: siguen pendientes recuperación/verificación de email y revisión operativa de HTTPS/cookies. No incluye directorio público/búsqueda, taxonomía/metajuego, playoffs, Google ni Swagger UI.
