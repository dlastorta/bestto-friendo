# Reglas de dominio — Tournament Manager

Estado: reglas acordadas y propuestas explícitamente distinguidas. Actualizadas el 2026-11-22.

## 1. Principios acordados

1. Un torneo está asociado a un juego; los juegos tienen afiliaciones o facciones configurables.
2. Los puntos de victoria, empate, derrota y bye son configurables por torneo.
3. Los criterios de desempate son una **lista ordenada** configurable. Ejemplo: Buchholz → OMW% → Buchholz con descarte → criterio adicional. Cada criterio solo rompe empates persistentes del anterior.
4. La configuración de puntuación y desempate se congela cuando el torneo inicia.
5. Un participante no puede recibir más de **un bye** en un torneo.
6. Un bye no equivale a jugar contra un oponente real; las políticas específicas para cada desempate deben definirse y probarse.
7. Un jugador tiene una **biblioteca personal de mazos reutilizables** y selecciona uno al inscribirse.
8. Al inscribirse, se guarda un **snapshot histórico e independiente** del mazo presentado. El registro del torneo no es un enlace vivo al contenido actual de la biblioteca.
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

**Regla confirmada:** al inscribirse, seleccionar un mazo de la biblioteca genera una copia histórica de su contenido y metadatos relevantes.

**Reglas propuestas para el MVP:**

- Una inscripción utiliza **un solo mazo**; variantes multideck o cambios entre rondas requieren un slice posterior.
- La copia almacena al menos: identificadores del torneo/participante, mazo original y revisión original (para trazabilidad), nombre, juego/formato, afiliaciones, texto de lista, etiquetas/arquetipo declarados, momento de presentación y hash opcional.
- Modificar un mazo personal **no** cambia la presentación.
- Hasta que empiece el torneo, un jugador/organizador autorizado puede reemplazar **explícitamente** la presentación: se registra una nueva revisión de la presentación y queda trazabilidad de la anterior.
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

- Los resultados tienen un tipo específico: partida jugada, bye, forfeit/abandono o cualquier otra clase que definamos.
- El bye da los puntos configurados; su efecto en cada fórmula de resistencia debe definirse por algoritmo, sin fabricar por defecto un rival con cero puntos.
- Un jugador no puede recibir un segundo bye, incluso si es el peor clasificado. Si la asignación no es posible con las restricciones, el motor debe reportarlo.
- El orden de los desempates es parte de la configuración congelada del torneo.
- Los puntos y las estadísticas se derivan de resultados verificables; no son contadores que se puedan modificar manualmente sin auditoría.

**Pendiente:** tratamiento exacto de rondas no jugadas, cálculo y mínimos de OMW%, variantes de Buchholz, prioridad precisa de candidatos a bye y resolución final si todos los criterios empatan.

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
