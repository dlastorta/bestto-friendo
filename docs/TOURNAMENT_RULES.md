# Reglas de dominio — Tournament Manager

Estado: reglas acordadas y propuestas explícitamente distinguidas. Actualizadas el 2026-11-26.

## 1. Principios acordados

1. Un torneo está asociado a un juego; los juegos tienen afiliaciones o facciones configurables.
2. Los puntos de victoria, empate, derrota y bye son configurables por torneo.
3. Los criterios de desempate son una **lista ordenada** configurable. Ejemplo: Buchholz → OMW% → Buchholz con descarte → criterio adicional. Cada criterio solo rompe empates persistentes del anterior.
4. La configuración de puntuación y desempate se congela cuando el torneo inicia.
5. Un participante no puede recibir más de **un bye** en un torneo.
6. Un bye no equivale a jugar contra un oponente real; las políticas específicas para cada desempate deben definirse y probarse.
7. Un jugador tiene una **biblioteca personal de mazos reutilizables** y presenta uno antes del inicio del torneo; puede inscribirse sin mazo inicialmente.
8. Al presentar el mazo, se guarda un **snapshot histórico e independiente**. El registro del torneo no es un enlace vivo al contenido actual de la biblioteca.
9. El ranking Elo global queda fuera del MVP.

## 2. Identidad y propiedad

- **UserAccount**: identidad autenticada que puede gestionar la biblioteca. No es necesariamente lo mismo que el competidor histórico.
- **Player**: identidad de competidor; puede intervenir en varios torneos.
- **Deck**: recurso de la biblioteca personal, propiedad de un jugador, asociado a un juego, con contenido y metadatos editables.
- **DeckRevision** (conceptual): versión persistente de los cambios significativos de un mazo personal.
- **TournamentParticipant**: participación de un jugador en un torneo.
- **DeckSubmission / TournamentDeckSnapshot**: copia histórica presentada para esa participación.

**Restricción:** la biblioteca personal requiere una política de autenticación/autorización antes de permitir que varios usuarios administren sus propios mazos. Nunca exponer edición de mazos ajenos mediante IDs adivinables.

### Decisiones del Slice 02 (2026-11-24)

- Registro e inicio de sesión por email y contraseña, con sesión mediante cookie.
- Solo un administrador del catálogo (`CatalogAdmin`) puede crear juegos; registrarse no concede ese permiso. Consultar el catálogo sigue siendo público.
- Cada cuenta registrada tiene su propio perfil de jugador. Consultarlo o modificarlo usa la identidad de la sesión, nunca un identificador de dueño provisto por el cliente.
- Política técnica inicial del perfil: nombre visible de 2 a 80 caracteres, sin exigir unicidad; el perfil tiene un identificador distinto de la cuenta.
- Los invitados y su futura conciliación con cuentas siguen pendientes; no se les asignan credenciales ficticias.
- La propiedad de mazos se implementará y verificará en Slice 03. El inicio de sesión con Google queda en el backlog futuro, sin decidir aún cómo vincular cuentas.

### Decisiones del Slice 04 (2026-11-25)

- Solo cuentas con rol `TournamentOrganizer` pueden crear y consultar torneos borrador; son privados del perfil creador. El rol no se concede al registrarse ni por administrar el catálogo.
- El servidor deriva el organizador de la sesión y establece `Draft` al crear. Revocar el rol bloquea acceso con sesiones existentes sin borrar los torneos.
- Cada torneo requiere un juego existente. Nombre de 2 a 120 caracteres, sin unicidad. El Slice 04 no implementa formatos avanzados, edición ni transiciones de estado.
- No se agregan aún puntos, desempates, participantes ni snapshots; estas reglas permanecen en sus slices previstos.

## 3. Juegos, facciones y metadatos

- **Game** contiene las afiliaciones/facciones válidas (ejemplo: clanes en L5R y colores en Magic).
- Cada juego/formato puede definir una cardinalidad de afiliaciones y reglas de combinación. No asumir que todas las facciones son mutuamente excluyentes.
- Los **arquetipos** y **estrategias** deben pertenecer a una taxonomía de juego o formato, no a un enum global rígido.
- Una etiqueta de estrategia puede surgir de la selección del jugador, la revisión del organizador o una clasificación posterior; registrar su origen si se publica como estadística.

### Política técnica provisional del Slice 01 (no decisión permanente de formatos)

Para el catálogo inicial, cada juego declara `minimumFactions` y `maximumFactions` y una lista de facciones únicas. La pertenencia se valida con IDs del propio juego; `min=1` y `max=1` para juegos de una sola afiliación, y valores mayores para combinaciones.

La distinción de principal/secundaria, afiliaciones condicionales y reglas especiales de un juego sigue **pendiente**. No se pretende inferirlas por el simple mínimo/máximo. La edición de un catálogo ya utilizado por torneos también requiere una regla histórica futura.

## 4. Biblioteca personal de mazos

- Un jugador puede tener muchos mazos dentro de un juego.
- Un mazo personal puede editarse, renombrarse o archivarse sin alterar inscripciones históricas.
- Cada edición conserva una nueva revisión lógica; nunca debe reescribir versiones anteriores ni snapshots históricos.
- **Confirmado el 2026-11-25:** lista en texto libre, destinada únicamente a mostrarse. Formato sugerido `N x Nombre de la carta` (N cantidad; x separador), sin parseo ni controles de formato, cartas, cantidades o legalidad. El texto se conserva literalmente; puede estar vacío o no seguir esa sugerencia. Importación y validación por cartas quedan fuera del MVP.
- Slice 03 incluye nombre, juego y facciones como metadatos; etiquetas/arquetipos quedan pendientes. Las facciones respetan pertenencia y cardinalidad del catálogo.
- Solo el propietario autenticado accede a la biblioteca, incluso si otra cuenta es administradora del catálogo. El juego queda fijo al crear; archivar conserva el historial y deja el mazo en modo consulta.

## 5. Presentación del mazo en un torneo

**Regla confirmada:** presentar un mazo de la biblioteca para una inscripción genera una copia histórica de su contenido y metadatos relevantes.

**Privacidad confirmada para Slice 06 (2026-11-25):** solo el jugador puede ver su lista presentada. El organizador únicamente ve si presentó un mazo, sin acceso a su contenido. La autorización se aplica también en API; ocultar el contenido en la interfaz no es suficiente.

**Inscripción confirmada para Slice 06 (2026-11-25):** acceso mediante enlace compartido por el organizador y cuenta autenticada. Se permite inscribirse sin mazo, pero debe presentarse antes del inicio del torneo. El inicio y la comprobación de este requisito se implementan en Slice 07. La primera presentación copia un mazo propio activo del juego compatible; no modifica la privacidad de la biblioteca.

**Reglas propuestas para el MVP:**

- Una inscripción utiliza **un solo mazo**; variantes multideck o cambios entre rondas requieren un slice posterior.
- La copia almacena al menos: identificadores del torneo/participante, mazo original y revisión original (para trazabilidad), nombre, juego/formato, afiliaciones, texto de lista, etiquetas/arquetipo declarados, momento de presentación y hash opcional.
- Modificar un mazo personal **no** cambia la presentación.
- Confirmado en Slice 07: hasta que empiece el torneo, solo el jugador puede reemplazar **explícitamente** su presentación con motivo obligatorio, actor, fecha y versiones anteriores preservadas. El organizador no puede sustituir ni consultar listas ajenas.
- Al iniciar el torneo, se bloquean las presentaciones; excepciones administrativas futuras deberán ser explícitas y auditadas.
- El sistema debe impedir la presentación de un mazo perteneciente a otro juego. Las restricciones de formato más sofisticadas llegarán después.
- Archivar o borrar de la biblioteca no borra automáticamente los datos que respaldan resultados históricos; definir por separado políticas de retención, privacidad y eliminación de datos personales.

### Ejemplo de historia inmutable

1. Un jugador tiene en su biblioteca `León Militar` revisión 3.
2. Se inscribe en `Copa Rokugan` y se almacena `DeckSubmission #1` con el contenido de revisión 3.
3. Actualiza `León Militar` a revisión 4 en su biblioteca. La presentación del torneo continúa siendo la de revisión 3.
4. Si todavía está permitido, selecciona expresamente "actualizar presentación" y se crea una nueva revisión de presentación; nunca se actualiza de forma silenciosa.
5. Iniciado el torneo, la presentación queda bloqueada.

## 6. Resultados, byes y desempates

### Configuración del Slice 05

- El organizador propietario configura las reglas solo en borrador. Los torneos comienzan sin configuración, sin puntajes por defecto.
- Política técnica inicial: puntos entre 0 y 1000000, con hasta dos decimales; victoria, empate, derrota y bye se declaran por separado. No se impone una relación entre esos valores.
- Byes habilitados/deshabilitados con puntaje propio; máximo uno por participante fijo, sin mecanismo de asignación todavía.
- Se conserva una secuencia sin duplicados de Buchholz, OMW% y/o Buchholz con descarte. Puede estar vacía. Esta configuración no implementa fórmulas ni resuelve las variantes pendientes.
- Un guardado desactualizado se rechaza; no se sobrescriben silenciosamente cambios de otra petición.

### Inicio y reglas históricas — Slice 07 (confirmado el 2026-11-26)

- Solo el organizador propietario inicia el torneo. Exige reglas configuradas, al menos dos jugadores inscriptos y presentación de mazo de todos ellos.
- Al iniciar se conserva una copia independiente e inmutable de los puntos por resultado, política de bye y orden de desempates, junto con versión de origen y fecha de inicio. Cambiar reglas posteriormente no debe afectar el torneo ni sus resultados históricos.
- Los cálculos futuros deben usar esa copia histórica. Si cambian las fórmulas o su interpretación, deberán conservar su semántica mediante versionado explícito; las fórmulas siguen pendientes, no se implementan en este slice.
- El estado pasa de `Draft` a `InProgress`. No permite nuevas inscripciones, presentaciones, reemplazos ni cambios de reglas; tampoco un segundo inicio ni volver a borrador. La biblioteca personal puede seguir editándose sin alterar snapshots.
- Inicio y cambios concurrentes se excluyen de forma atómica. Un conflicto exige recargar y revisar antes de reintentar.
- El inicio no publica el torneo ni sus listas. Todo el historial de presentaciones sigue siendo privado del jugador.

### Partidas y resultados — Slice 08

- El organizador propietario crea partidas y carga/corrige resultados en torneos en curso. Los jugadores no reportan resultados en este slice. Estas vistas administrativas siguen privadas.
- Cada partida tiene número de ronda positivo y dos jugadores distintos inscriptos; un jugador participa como máximo en una partida de esa ronda. La numeración es manual, sin apertura/cierre de rondas ni comprobación de que la ronda anterior terminó. La prevención de revancha entre rondas pertenece al algoritmo suizo del Slice 12.
- Resultados iniciales: victoria del primer jugador, victoria del segundo o empate. Tipo `Played` o `Forfeit`; forfeit exige ganador y usa puntos normales de victoria/derrota. No implica retirar al jugador del torneo. No hay bye, doble derrota, marcador de juegos internos ni anulación todavía.
- Primera carga y correcciones guardan actor y fecha. Corregir exige motivo de 1 a 500 caracteres y conserva todas las revisiones. Un conflicto de versión exige recargar; no hay sobrescritura silenciosa.
- Los puntos se derivan de la revisión vigente y de `StartedRules`, sin totales mutables. La futura tabla del Slice 09 reconstruirá acumulados desde esas fuentes y reflejará las correcciones. No se calcula clasificación en Slice 08.
- Las partidas no revelan listas ni referencias de mazos. No se editan contendientes ni se borran partidas/resultados; cualquier futura anulación deberá diseñarse con auditoría.

### Reglas para los resultados futuros

- Los resultados tienen un tipo específico: partida jugada, bye, forfeit/abandono o cualquier otra clase que definamos.
- El bye da los puntos configurados; su efecto en cada fórmula de resistencia debe definirse por algoritmo, sin fabricar por defecto un rival con cero puntos.
- Un jugador no puede recibir un segundo bye, incluso si es el peor clasificado. Si la asignación no es posible con las restricciones, el motor debe reportarlo.
- El orden de los desempates es parte de la configuración congelada del torneo.
- Los puntos y las estadísticas se derivan de resultados verificables; no son contadores que se puedan modificar manualmente sin auditoría.

**Pendiente:** prioridad automática de candidatos para Slice 12. Las fórmulas, forfeits, pendientes y empates finales quedaron definidos en Slice 10.

## 7. Publicación y estadísticas

- Finalizar un torneo no lo publica automáticamente: la publicación es una acción explícita.
- La política de visibilidad debe distinguir al menos: metadatos del mazo, lista de cartas, resultados y campos personales.
- La publicación de una lista no es automática por el hecho de haberla enviado.
- Se pueden calcular popularidades por inscripciones (representación) y rendimientos por partidas efectivamente disputadas; las victorias por bye no cuentan como victorias contra mazos o rivales en los cálculos de enfrentamientos.
- Las estadísticas deben ser históricas y reproducibles desde snapshots, resultados y clasificaciones registradas; no depender del contenido vivo de la biblioteca personal.
- No mezclar estadísticas de distintos juegos o formatos por accidente.

## 8. Invariantes a probar

- Un jugador no puede enviar un mazo de otro juego al torneo.
- Un usuario no puede editar mazos de otro propietario.
- Editar o archivar un mazo de la biblioteca no altera presentaciones históricas.
- Una inscripción no puede comenzar el torneo sin cumplir la política de mazo requerida por ese torneo.
- Las reglas de puntuación y desempate permanecen bloqueadas tras el inicio.
- Un participante nunca recibe dos byes.
- La tabla aplica los desempates en la secuencia configurada.
- Las estadísticas de win rate por mazo no tratan los byes como enfrentamientos.
- El torneo puede finalizarse sin hacerse público, y una lista privada no debe mostrarse por error en la página publicada.

## Tabla básica — Slice 09 (confirmado)

- Visible al organizador propietario con rol vigente y a jugadores inscriptos, nunca pública. Si el organizador pierde su rol pero está inscripto, conserva únicamente el acceso de participante a la tabla.
- Puntos de mayor a menor desde resultados vigentes y reglas históricas; pendientes no suman. Todos los inscriptos aparecen, incluso con cero puntos.
- Igual puntaje implica igual puesto, con numeración 1, 1, 3. Orden secundario por ID solo estabiliza la presentación, no rompe empates deportivos.
- Las correcciones se reflejan al consultar nuevamente, sin sumar revisiones anteriores. No aplica desempates, byes ni estadísticas de win rate.

## Desempates — Slice 10 (confirmado)

Buchholz suma puntos actuales de rivales de partidas jugadas; Buchholz con descarte quita una contribución mínima. OMW% promedia la proporción de victorias de cada rival (empate=0,5), con mínimo exacto 1/3 por rival, sin depender de puntos configurables. Forfeits y pendientes no cuentan como enfrentamientos ni en MWP, aunque los puntos de forfeit siguen en los totales de rivales usados por Buchholz. Repeticiones de rival cuentan por encuentro. Sin rivales computables, valor cero. Si persiste igualdad tras toda la cadena, compartir puesto.

Torneos ya iniciados conservan versión 0 (solo puntos); nuevos inicios capturan versión 1. Toda evolución debe preservar semántica histórica. Comparaciones exactas sin desempatar por redondeo. Ver casos, contrato y transición en [Slice 10](slices/10-DESEMPATES.md). El tratamiento confirmado de byes se detalla a continuación.

## Byes — Slice 11 (confirmado)

Asignación manual del organizador propietario en torneo en curso con byes habilitados; solo inscriptos sin partida en esa ronda ni bye previo. Máximo uno por ronda y por jugador en el torneo, exclusivamente con cantidad impar de inscriptos. Otorga puntos congelados sin rival ficticio ni contribución a MWP o lista de rivales de Buchholz/OMW%; sus puntos sí integran el total del jugador usado por sus rivales en Buchholz. Actor y fecha inmutables, sin edición/cancelación. Nuevos inicios congelan ByePolicyVersion=1; torneos ya iniciados conservan versión 0 sin asignación. Elección automática pendiente de Slice 12. Ver [contratos](slices/11-BYES.md).
