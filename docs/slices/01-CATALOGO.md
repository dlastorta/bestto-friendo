# Slice 01 — Catálogo de juegos y facciones

## Historia

Como organizador quiero crear y consultar juegos y sus facciones (clanes/colores) con una regla de selección mínima/máxima, para que los mazos y torneos futuros utilicen las facciones correctas.

## Alcance implementado

- Crear juego con nombre, facciones y cardinalidad mínima/máxima.
- Listar y consultar juegos con los identificadores de sus facciones.
- Persistir ambos elementos en PostgreSQL mediante EF Core confinado a Infrastructure.
- Aplicar las invariantes en Domain, incluyendo la pertenencia al juego de las facciones elegidas.
- Mostrar catálogo y formulario en React con errores y feedback.
- Responder `400` por reglas inválidas, `409` por nombre repetido y `404` por ID inexistente.
- Pruebas unitarias de Domain; de endpoints con repositorio falso; de PostgreSQL real en CI.

## Reglas locales del MVP

- Juego: nombre obligatorio de 2 a 120 caracteres, único ignorando mayúsculas y minúsculas.
- Facciones: entre 1 y 50, nombres de 1 a 80 caracteres, únicos por juego ignorando mayúsculas y minúsculas/espacios externos.
- Cardinalidad: `1 <= minimumFactions <= maximumFactions <= cantidad de facciones`.
- Selección: solo IDs del propio juego; no duplicados; dentro de su cardinalidad.
- Para un juego con una sola facción elegible, `min=1`, `max=1`. Para combinaciones como colores de Magic, `min=1`, `max=5` si se registran los cinco colores.

Estas reglas representan una política simple de cardinalidad; no modelan facción primaria/secundaria, legalidad, formato ni reglas específicas de combinación. Quedan pendientes para otro slice. La validación de selección es pública en el agregado `Game`; su integración con mazos ocurrirá en el Slice 03, no antes.

## API

`POST /api/games`

```json
{
  "name": "Legend of the Five Rings",
  "factions": ["León", "Grulla", "Dragón", "Escorpión", "Fénix", "Cangrejo", "Unicornio"],
  "minimumFactions": 1,
  "maximumFactions": 1
}
```

Responde `201 Created` con la representación completa y una cabecera `Location: /api/games/{id}`.

`GET /api/games` devuelve el catálogo ordenado alfabéticamente.

`GET /api/games/{id}` devuelve un juego o `404`.

## Fuera de alcance

- Editar/eliminar juegos o facciones existentes.
- Autenticación/autorización (Slice 02). **No exponer la API públicamente antes de implementar permisos de escritura.**
- Registro de jugadores, mazos, torneos, resultados o estadísticas.
- Selección concreta de facciones por un mazo (ocurrirá con la biblioteca).

## Verificación

1. Ejecutar migraciones: `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api`.
2. Levantar Postgres, API y frontend como indica README.
3. Crear «L5R» desde React, observar sus siete facciones, recargar y verificar persistencia.
4. Intentar repetir el nombre o una facción, y verificar errores sin crear registros.
5. Ejecutar `dotnet test TournamentManager.slnx`, `npm test`, `npm run build`, `python scripts/verify_structure.py`.
6. En CI, la base PostgreSQL aislada ejecuta una prueba HTTP de persistencia real con `RUN_POSTGRES_INTEGRATION=true`.

**Estado de verificaciones en este entorno:** validación estática ejecutada; faltan `dotnet build/test`, `npm install/test/build`, ejecución de Docker y migraciones por falta de SDK/servicio/paquetes en el runtime actual.
