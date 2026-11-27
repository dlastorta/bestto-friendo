# AGENTS.md — Tournament Manager

Este archivo contiene las instrucciones de trabajo para asistentes y colaboradores en este repositorio. Aplican en todo el árbol salvo que un `AGENTS.md` más específico establezca algo diferente.

## Fuente de verdad

1. `docs/TOURNAMENT_RULES.md` es la fuente para reglas **confirmadas** del dominio. Distinguir las propuestas y los pendientes; no convertirlos silenciosamente en decisiones.
2. `docs/ROADMAP.md` fija el orden y alcance de los vertical slices.
3. `docs/ARCHITECTURE.md` describe las convenciones técnicas vigentes.
4. Ante discrepancias, detener cambios de reglas y explicitar la contradicción antes de implementar.

## Slice actual: 13 — Cierre, reapertura y regeneración auditada

**Permitido:** cerrar torneos completos, reabrir con motivo exclusivamente para corregir resultados y volver a cerrar; regenerar explícitamente solo la última ronda antes del primer cierre, conservando versiones anuladas y resultados según docs/slices/13-CIERRE.md. Mantener y generar rondas suizas según docs/slices/12-SUIZO.md; asignación manual de byes según docs/slices/11-BYES.md; tabla por puntos para organizador propietario con rol vigente y jugadores inscriptos; puestos compartidos (1, 1, 3) si persiste igualdad tras los criterios congelados. Aplicar fórmulas versión 1 de docs/slices/10-DESEMPATES.md solo a nuevos inicios; versión 0 conserva puntos. Reconstruir desde resultados vigentes y StartedRules, sin totales persistidos ni listas de mazos. Ver `docs/slices/09-TABLA.md`. Mantener slices anteriores; el organizador propietario crea partidas por ronda en torneos en curso y carga/corrige resultados. Dos jugadores distintos inscriptos, una partida por jugador y ronda. Victoria/derrota o empate; forfeit con ganador y puntos normales. Correcciones con motivo e historial de actor/fecha. Puntos derivados exclusivamente de `StartedRules`. Ver `docs/slices/08-PARTIDAS.md`.

**No permitido en Slice 13:** regenerar rondas anteriores a la última o cualquier ronda después del primer cierre, sobrescribir/borrar historial, cancelación/edición independiente de byes, publicación/eliminación de torneos, cambio de reglas históricas, edición de contendientes o borrado de partidas/resultados, conciliación de invitados, Elo, legalidad/importación de cartas. Google y Swagger UI quedan para después. Nunca exponer listas de mazos desde partidas.

**Seguridad temporal:** crear juegos requiere `CatalogAdmin`; administrar torneos requiere `TournamentOrganizer`. Inscripción por enlace y presentación propia requieren sesión, sin rol administrativo. Perfil, biblioteca, inscripciones y snapshots se determinan por la cuenta autenticada, sin privilegios sobre listas ajenas. Roles revalidados por petición; escrituras con CSRF. **No publicar la API en Internet** sin completar recuperación/verificación de email y revisar configuración HTTPS, claves de cookies y operación de cuentas.

## Enfoque arquitectónico

- Monolito modular con Vertical Slice Architecture; organizar cada caso de uso por feature **respetando límites de dependencia entre proyectos**.
- Separar siempre reglas de dominio y casos de uso de HTTP y persistencia; abstraer los puertos reales que se necesiten.
- Preferir implementación simple y legible. No introducir MediatR, AutoMapper, repositorio genérico, microservicios ni interfaces especulativas; **sí** exigir abstracciones en el límite hacia infraestructura cuando exista una dependencia real.
- Backend en .NET 10 LTS / ASP.NET Core; frontend React + TypeScript + Vite; EF Core con PostgreSQL.
- Mantener el comportamiento reproducible y testeable.

## Límites de capas (obligatorio, decisión explícita)

**Regla principal: no usar EF Core ni `DbContext` en dominio o aplicación.** Los vertical slices agrupan funcionalidades, **no** justifican acoplar la lógica de negocio a tecnologías concretas.

- **Domain (`TournamentManager.Domain`)**: entidades, value objects, invariantes y reglas puras; solo C#/.NET básico. No referenciar `Application`, `Infrastructure`, `Api`, EF Core ni anotar entidades con atributos de EF.
- **Application (`TournamentManager.Application`)**: casos de uso, orquestación, DTOs de aplicación e interfaces/puertos para dependencias externas. Puede referenciar `Domain`; **no** puede referenciar `Infrastructure`, `Api`, `Microsoft.EntityFrameworkCore` ni Npgsql. No exponer `DbContext`, `DbSet`, `IQueryable` o `EntityEntry` en contratos.
- **Infrastructure (`TournamentManager.Infrastructure`)**: implementaciones de puertos, `DbContext`, mapeos Fluent API, migraciones, consultas EF y transacciones. Referencia `Application` y `Domain`; nunca al revés.
- **Api (`TournamentManager.Api`)**: adaptador HTTP y punto de composición de DI. Un endpoint invoca casos de uso o puertos expuestos por `Application`, **nunca** ejecuta consultas EF directamente. La **única** referencia aceptada a `Infrastructure` desde API es el punto de composición (`Program.cs` y configuración equivalente); `Microsoft.EntityFrameworkCore.Design` en API es una excepción de tooling para `dotnet-ef` y **no** habilita usar EF en endpoints.
- **Frontend**: consume contratos HTTP; no copia reglas de dominio como segunda fuente de verdad.

Dependencias permitidas: `Api -> Application`, `Api -> Infrastructure` (composition root), `Infrastructure -> Application/Domain`, `Application -> Domain`; `Domain -> ninguna`. Prohibidas las inversas.

**Persistencia:** EF Core ofrece capacidades similares a Repository (`DbSet`) y Unit of Work (`DbContext`/`SaveChanges`), pero eso no vuelve a esos tipos parte de nuestra interfaz de negocio. Definir **interfaces específicas por caso de uso** en Application solo cuando se necesiten, implementadas en Infrastructure. No imponer `IRepository<T>` genérico; no agregar `IUnitOfWork` preventivamente. Si un caso de uso requiere transacciones coordinadas, introducir un puerto acotado y realizar el commit en infraestructura.

**Pruebas:** reglas de dominio/aplicación probadas sin base de datos ni mocks de EF; integración de EF en infraestructura. Rechazar PRs que incorporen `using Microsoft.EntityFrameworkCore` en Domain/Application o features HTTP.

## Flujo obligatorio para cada slice

1. Confirmar alcance y criterios de aceptación en `docs/ROADMAP.md`.
2. Implementar únicamente lo necesario para entregar ese comportamiento de punta a punta.
3. Incorporar validaciones y pruebas unitarias/integración apropiadas.
4. Ejecutar build, tests y verificación manual cuando el entorno lo permita. No afirmar que algo se probó si no se pudo ejecutar.
5. Actualizar README, arquitectura y reglas cuando cambien decisiones.
6. Documentar bloqueos o verificaciones pendientes con instrucciones reproducibles.

## Invariantes que deben preservar los slices futuros

- Puntaje de victoria/empate/derrota/bye configurable, congelado al inicio en `StartedRules`. Los futuros cálculos usan esa copia, nunca configuración viva. Cambios futuros de fórmulas deberán preservar la semántica histórica mediante versionado explícito.
- Criterios de desempate ordenados y aplicados secuencialmente.
- Máximo un bye por participante y torneo.
- Biblioteca personal de mazos separada del `DeckSubmission` / snapshot histórico inmutable en el torneo.
- Propiedad/autorización de la biblioteca; nunca editar mazos de otro dueño.
- Publicación del torneo independiente de su finalización; listas privadas no expuestas.
- Estadísticas basadas en snapshots/resultados históricos; byes excluidos de win rate de enfrentamientos.

## Calidad y seguridad

- **Dependencias seguras (bloqueante):** no aceptar dependencias directas ni transitivas NuGet o npm con vulnerabilidades conocidas de severidad **alta o crítica** (`NU1903` / `NU1904` para NuGet). Corregir o actualizar la dependencia afectada antes de integrar cambios; nunca silenciar alertas sin una excepción documentada, justificada y aprobada.
- Ejecutar auditoría de dependencias en CI durante `dotnet restore` con `NuGetAuditMode=all`. Los avisos `NU1903` y `NU1904` deben hacer fallar el build. Revisar también las alertas moderadas y bajas para planificar su corrección.
- Si una vulnerabilidad no tiene corrección disponible, no introducir la dependencia ni desplegar hasta evaluar explícitamente el riesgo y una alternativa segura.

- Tests de comportamiento y no de estructura interna.
- Los tests unitarios deben ser independientes de servicios externos. Los tests de integración deben controlar explícitamente sus dependencias; los mocks y reemplazos deben ser determinísticos y registrarse después de los servicios de producción.
- Los tests frontend deben desmontar los componentes y limpiar el DOM entre ejecuciones mediante cleanup en la configuración global de tests.
- No commitear secretos, tokens, `.env` reales ni credenciales de producción.
- Configuración de desarrollo claramente diferenciada de producción.
- Usar migraciones EF Core versionadas para modificaciones de esquema; nunca hacer `EnsureCreated` como sustituto.
- Registrar cambios de alcance conscientemente; ninguna decisión pendiente se da por resuelta por accidente.
- Si no están instalados SDKs, Docker o paquetes, reportar con precisión qué verificaciones no se pudieron ejecutar.

## Definition of Done — Slice 13

- Partidas manuales API/React solo para el organizador propietario, en torneo iniciado, con jugadores inscriptos distintos y sin duplicación por ronda.
- Resultado y correcciones con historial inmutable, actor de sesión, fecha, tipo y motivo; versiones esperadas impiden sobrescrituras.
- Tabla reconstruida desde reglas históricas y resultados vigentes, incluyendo ceros y puestos compartidos. Acceso solo al organizador propietario con rol vigente o inscriptos; sin acumulados mutables ni listas privadas.
- Migración versionada, autorización/CSRF, revocación de rol, pruebas puras y concurrencia real PostgreSQL.
- Conservar las garantías anteriores siguientes.

### Garantías de inicio e inscripciones

- Inicio exclusivo del organizador propietario, con confirmación en React, reglas explícitas, al menos dos jugadores y todas las presentaciones completas.
- Persistir fecha de inicio y copia autónoma de puntos, política de bye y orden de desempates; impedir segundo inicio y modificaciones posteriores.
- Reemplazo exclusivo del jugador antes del inicio, con motivo, actor, fecha y todas las versiones anteriores; historial privado.
- Inicio atómico frente a cambios concurrentes de reglas, inscripciones y presentaciones; probar ambas órdenes de commit con PostgreSQL real.
- Migración que preserve snapshots existentes; conservar las garantías del Slice 06 siguientes.

- Inscripción por enlace y cuenta autenticada, sin mazo inicial obligatorio, desde API y React.
- Presentación posterior de mazo propio compatible y snapshot independiente; editar/archivar origen no altera su contenido.
- Organizador solo consulta estado de presentación; contenido y referencias del mazo visibles únicamente al jugador.
- Exigir `TournamentOrganizer` para administrar el torneo, separado de `CatalogAdmin`; no conceder roles por registro o cuerpo de la petición.
- Impedir acceso ajeno y revalidar revocaciones con cookies existentes; cerrar la vista privada al perder sesión o permiso.
- Separar perfil y credenciales; conservar todas las reglas del catálogo.
- Conservar autorización del catálogo; validar CSRF y no aceptar un dueño del perfil o mazo enviado por el cliente.
- Persistir inscripciones y snapshots mediante migraciones EF versionadas y escrituras transaccionales; prevenir duplicados concurrentes, sin EF en Domain, Application ni endpoints.
- Tests puros, HTTP aislados, aislamiento entre cuentas e integración PostgreSQL real habilitable en CI.
- Documentar contratos, asignación del organizador y limitaciones; conservar las reglas de catálogo, identidad y biblioteca.
- No declarar que tests/build/migración corrieron si no existe evidencia de ejecución.

- Desempates secuenciales exactos, forfeits excluidos y versión histórica preservada; tests de fórmulas, cadena, migración y UI. No cambiar semántica de versiones existentes.

- Byes manuales con política congelada versión 1 en nuevos inicios: organizador propietario, inscriptos impares, máximo uno por ronda/jugador y sin partida en esa ronda. Puntos históricos sin rival ficticio; excluidos de MWP/lista de rivales, incluidos en totales de Buchholz. Concurrencia bye/partida atómica y actor/fecha auditados. Ver docs/slices/11-BYES.md.

- Primera ronda aleatoria auditada; siguientes desde menor puntaje/resistencia, sin revanchas (incluido forfeit), bye al peor elegible que permita completar. Avance solo con rondas anteriores completas. Algoritmo congelado versión 1 en nuevos inicios, históricos manuales versión 0. Ronda atómica frente a asignaciones y correcciones concurrentes; no modificar cruces al corregir resultados. Ver docs/slices/12-SUIZO.md.

- Cierre solo con al menos una ronda completa, sin pendientes ni jugadores sin asignación; reapertura con motivo, exclusivamente para correcciones. Cada cierre conserva referencias exactas a resultados y byes.
- Regeneración explícita solo de la última ronda antes del primer cierre, con motivo y versiones esperadas; pares y bye reemplazados atómicamente, resultados anteriores preservados pero excluidos de la tabla.
