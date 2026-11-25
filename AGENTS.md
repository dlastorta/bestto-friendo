# AGENTS.md — Tournament Manager

Este archivo contiene las instrucciones de trabajo para asistentes y colaboradores en este repositorio. Aplican en todo el árbol salvo que un `AGENTS.md` más específico establezca algo diferente.

## Fuente de verdad

1. `docs/TOURNAMENT_RULES.md` es la fuente para reglas **confirmadas** del dominio. Distinguir las propuestas y los pendientes; no convertirlos silenciosamente en decisiones.
2. `docs/ROADMAP.md` fija el orden y alcance de los vertical slices.
3. `docs/ARCHITECTURE.md` describe las convenciones técnicas vigentes.
4. Ante discrepancias, detener cambios de reglas y explicitar la contradicción antes de implementar.

## Slice actual: 03 — Biblioteca personal de mazos

**Permitido:** mantener catálogo e identidad; crear/listar/consultar/editar/archivar mazos propios por juego y conservar revisiones. Lista literal sin validaciones de cartas, cantidades ni formato; `N x Nombre de la carta` es una sugerencia. Ver `docs/slices/03-BIBLIOTECA.md`. Persistencia y EF solo en Infrastructure.

**No permitido en Slice 03:** torneos y sus snapshots, formatos avanzados, resultados, algoritmos suizos, Elo, edición/eliminación de juegos, legalidad/importación de cartas ni conciliación de invitados. Login con Google es mejora futura.

**Seguridad temporal:** crear juegos requiere `CatalogAdmin`; perfil y biblioteca se determinan por la cuenta autenticada, sin privilegios administrativos sobre mazos ajenos. Escrituras con CSRF. **No publicar la API en Internet** sin completar recuperación/verificación de email y revisar configuración HTTPS, claves de cookies y operación de cuentas.

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

## Invariantes para slices futuros (no implementar ahora)

- Puntaje de victoria/empate/derrota/bye configurable, congelado al inicio.
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

## Definition of Done — Slice 03

- Crear/listar/consultar/editar/archivar mazos propios desde API y React, con historial de revisiones inmutables.
- Conservar texto de lista literal; validar juego y facciones sin parsear cartas.
- Impedir lecturas/escrituras ajenas y pérdida de cambios concurrentes; cerrar la vista privada al terminar la sesión.
- Separar perfil y credenciales; conservar todas las reglas del catálogo.
- Conservar autorización del catálogo; validar CSRF y no aceptar un dueño del perfil o mazo enviado por el cliente.
- Persistir mazos y revisiones mediante migraciones EF versionadas, sin EF en Domain, Application ni endpoints.
- Tests puros, HTTP aislados, aislamiento entre cuentas e integración PostgreSQL real habilitable en CI.
- Documentar contratos, conflictos de versión, archivo y limitaciones; conservar la documentación de cuentas y catálogo.
- No declarar que tests/build/migración corrieron si no existe evidencia de ejecución.
