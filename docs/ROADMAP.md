# Roadmap — Tournament Manager

Estado: Slices 01–07 integrados; Slice 08 implementado y verificado, pendiente de revisión. Actualizado el 2026-11-26.

## Objetivo

Aplicación web para organizar torneos de distintos juegos, comenzando por el sistema suizo; registrar resultados y mazos utilizados; publicar torneos finalizados; y analizar metajuegos. Backend en ASP.NET Core, frontend React + TypeScript, PostgreSQL con EF Core. Monorepo y arquitectura modular monolítica por vertical slices.

## Principios de planificación

- Cada slice entrega una capacidad de usuario verificable de punta a punta (UI/API/persistencia/tests cuando aplique).
- Solo se construyen capacidades que el slice actual necesita; no implementar anticipadamente el ranking Elo, importadores, validación de cartas ni otros formatos de torneo.
- Los cortes entre slices son orientativos: pueden dividirse si resultan grandes, sin perder la capacidad funcional como unidad de entrega.
- Mantener un conjunto pequeño de pruebas de reglas de negocio y pruebas de integración por slice.
- No confundir un mazo reutilizable de la biblioteca con la inscripción histórica de ese mazo en un torneo.
- La implementación por vertical slices **no** permite acoplamiento entre capas: `Domain` y `Application` jamás deben usar EF Core, DbContext o Npgsql; adaptadores de persistencia exclusivamente en `Infrastructure`.

## Roadmap — MVP

| Slice | Entrega observable | Criterios mínimos de aceptación |
|---|---|---|
| 00. Bootstrap | Monorepo, API, React, PostgreSQL, migraciones y health check. | La solución corre localmente; pipeline de tests básico. |
| 01. Catálogo de juegos | Crear/listar juegos y definir sus afiliaciones/facciones y reglas de selección (una o varias). | No se puede asignar una facción de otro juego. |
| 02. Identidad mínima de jugador | Cuenta por email/contraseña, sesión por cookie y perfil propio. | Registro/login/logout, aislamiento de perfiles y creación de juegos solo para administradores del catálogo. La propiedad de mazos se verificará en Slice 03; invitados y conciliación siguen pendientes. |
| 03. Biblioteca personal de mazos | Crear, listar, consultar, editar y archivar mazos por juego. | Un jugador mantiene varios mazos; editar uno genera una nueva revisión lógica y no altera versiones utilizadas en torneos. |
| 04. Crear torneos | Crear/listar/consultar torneos borrador asociados a juego. | Juego válido; rol `TournamentOrganizer` obligatorio y borradores privados del creador. Formatos avanzados postergados. |
| 05. Reglas de torneo | Configurar puntos por resultado, política de bye y secuencia ordenada de criterios de desempate. | Orden de desempate persistido; configuración editable solo en borrador. |
| 06. Inscripciones y deck snapshot | Inscribir jugadores y seleccionar un mazo propio del juego compatible; almacenar copia del mazo presentado. | La inscripción referencia al mazo de origen **y** conserva un snapshot autónomo; modificaciones posteriores en biblioteca no lo actualizan. |
| 07. Comenzar torneo | Validar arranque y conservar reglas históricas independientes; bloquear inscripciones y presentaciones. | Organizador propietario, reglas configuradas, mínimo dos jugadores y todos los mazos presentados. Solo el jugador puede sustituir su presentación antes del inicio, con motivo e historial privado. |
| 08. Partidas manuales y resultados | Crear enfrentamientos manuales, cargar/corregir resultados y registrar el motivo (jugado, forfeit, etc.). | Puntos de cada partida derivados de reglas históricas y recalculados al corregir, con historial. La clasificación agregada del Slice 09 debe reconstruirse desde resultados vigentes. |
| 09. Tabla básica | Mostrar clasificación por puntos. | Se reconstruye desde resultados y reglas; no usa un total mutable como fuente de verdad. |
| 10. Cadena de desempates | Configurar y calcular secuencialmente Buchholz, OMW%, Buchholz con descarte, según definiciones concretas. | Si persiste igualdad se evalúa el criterio siguiente, sin reordenar arbitrariamente. Implementar políticas de rondas no jugadas antes de aceptar resultados oficiales. |
| 11. Byes | Soportar asignación y resultados de bye según política. | Máximo un bye por jugador en el torneo; bye distinguible de partidas jugadas y forfeit. |
| 12. Emparejamientos suizos | Generar rondas y prevenir rivales repetidos, respetando restricciones de bye y reglas definidas. | Emparejamientos válidos o error explicativo cuando las restricciones impiden generarlos. |
| 13. Finalizar torneo | Cierre, clasificación final y conservación histórica. | Solo se finaliza bajo condiciones validadas; queda historial de correcciones. |
| 14. Publicación | Página pública de resultados y metadatos permitidos. | Finalizar y publicar son acciones separadas; configuración de visibilidad de las listas. |
| 15. Taxonomía y metajuego básico | Arquetipos/estrategias por juego o formato, clasificación manual y primeros gráficos. | Filtrar por juego, formato y torneo; popularidad por inscripciones y win rate por partidas efectivamente jugadas. |

### Entregas intermedias sugeridas

- **M1 (00–03):** usuario con biblioteca personal reutilizable.
- **M2 (04–07):** torneo configurado con jugadores inscriptos y mazos históricamente preservados.
- **M3 (08–12):** torneo suizo operable y tabla con desempates.
- **M4 (13–15):** resultados publicados y estadísticas de mazos.

## Fuera del MVP

- Ranking Elo global.
- Biblioteca avanzada de cartas, validación de legalidad e importadores específicos de juegos.
- Torneos por equipos, eliminatorias y múltiples fases.
- Múltiples mazos por jugador en un mismo torneo o cambio entre rondas (si un reglamento lo requiere, será feature separado).
- Cálculo automático de estrategia/arquetipo a partir de cartas.
- Integración automática con páginas de terceros para publicación.

## Mejora futura — Inicio de sesión con Google

Solicitada el 2026-11-24. Incorporar Google como proveedor de inicio de sesión conservando el perfil y la propiedad de la biblioteca. No incluido en Slice 02. Antes de implementarlo, definir vinculación/desvinculación de cuentas, verificación de email y recuperación; no fusionar cuentas automáticamente solo por coincidencia de email.

## Herramienta pendiente — Swagger UI

Acordado el 2026-11-25: agregar Swagger UI al final, sin adelantarlo al Slice 04. El documento OpenAPI JSON sigue disponible solo en desarrollo en `/openapi/v1.json`.

## Backlog futuro — Finanzas y tercer tiempo (fuera del MVP)

Estas capacidades no tienen slice asignado ni modifican el orden, alcance o criterios de los slices 00–15. No implementar hasta planificar y aprobar su alcance.

- **Gestión económica de torneos:** presupuesto por categoría, estimación de inscripciones pagas, precio sugerido con reserva configurable y ajuste manual, pagos y exenciones, gastos reales con pagador y comprobantes opcionales, ingresos/egresos/resultado, moneda y redondeo por evento.
- **Tercer tiempo:** evento opcional asociado al torneo, invitados incluso no competidores, respuestas Sí/No/NS/NC, confirmación de asistencia, compras y reparto equitativo, saldos, transferencias sugeridas y liquidaciones. Inscripción y asistencia independientes del torneo.
- **Tercer tiempo — Reparto avanzado:** extensión del reparto básico para definir participantes específicos por gasto, excluir personas del reparto general o particular y calcular saldos individuales respetando las compras realizadas, incluso por quienes no asisten o no participan del reparto.

Los módulos financieros son independientes de clasificación, emparejamiento y puntuación. Deben preservar auditoría, privacidad, exactitud monetaria y evitar doble contabilización. Ver [reglas y propuestas financieras](TOURNAMENT_RULES.md).

## Definition of Done por slice

1. Caso de uso y criterios de aceptación documentados.
2. Contrato de API y permisos definidos.
3. Validaciones y reglas de dominio probadas.
4. Persistencia y migraciones controladas cuando corresponda.
5. UI utilizable si el caso de uso es visible al organizador/jugador.
6. Tests automáticos ejecutándose y demostración manual reproducible.
   - Los límites de dependencia entre Domain, Application, Infrastructure y API pasan los checks de arquitectura.
7. Documentación de reglas y decisiones actualizada.

## Lista de cartas confirmada

- Formato de listas confirmado: texto libre para mostrar, con `N x Nombre de la carta` como sugerencia, sin validación de cartas ni cantidades.

## Decisiones aún pendientes

- Facciones principales/secundarias, combinación de colores y sus restricciones por juego/formato.
- Qué campos de listas y estrategias son públicos, privados o visibles solo tras el cierre.
- Cálculos exactos de Buchholz/OMW% y rondas no jugadas, límites y criterio final de desempate.
- Regla suiza precisa de asignación de bye cuando múltiples jugadores son candidatos.
- Gestión de usuarios invitados y conciliación futura con una cuenta.

## Implementación actual

- **Slice 01:** catálogo y reglas conservados. Ver [`slices/01-CATALOGO.md`](slices/01-CATALOGO.md).
- **Slice 02:** identidad mínima, perfil propio y autorización del catálogo. Ver [`slices/02-IDENTIDAD.md`](slices/02-IDENTIDAD.md). No incluye mazos ni invitados.
- **Slice 03:** biblioteca privada por juego, edición con revisiones y archivo. Lista de cartas literal, sin controles. Ver [`slices/03-BIBLIOTECA.md`](slices/03-BIBLIOTECA.md). No incluye snapshots de torneo.
- **Slice 04:** borradores privados para organizadores, con creación/listado/consulta por juego. Ver [`slices/04-TORNEOS.md`](slices/04-TORNEOS.md).
- **Slice 05:** configuración explícita de puntos, byes y orden de desempates solo en borrador; todavía sin cálculos. Ver [`slices/05-REGLAS.md`](slices/05-REGLAS.md).
- **Slice 06:** inscripción por enlace sin mazo inicial, presentación posterior de snapshot privado y estado visible al organizador. Ver [`slices/06-INSCRIPCIONES.md`](slices/06-INSCRIPCIONES.md).
- **Slice 07:** inicio, reglas históricas autónomas y sustitución auditada privada antes del inicio. Ver [`slices/07-INICIO.md`](slices/07-INICIO.md).
- **Slice 08:** partidas manuales por ronda, resultados y correcciones auditadas con puntos históricos. Ver [`slices/08-PARTIDAS.md`](slices/08-PARTIDAS.md).
