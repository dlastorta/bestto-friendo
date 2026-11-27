# Slice 10 — Cadena de desempates

Implementado en `codex/slice-10-tiebreakers`, desde main tras PR #10.

## Reglas confirmadas

- Puntos primero; después cada criterio en el orden congelado, solo para igualdades persistentes. Si todo empata, puesto compartido (1, 1, 3). ID solo estabiliza presentación.
- Buchholz: suma de puntos actuales de rivales de enfrentamientos jugados. Esos puntos incluyen todos los resultados puntuables del rival, incluso sus forfeits.
- BuchholzCut: quitar exactamente una contribución mínima; cero con cero o un enfrentamiento computable.
- OMWPercentage: promedio de MWP de rivales; MWP=(victorias+0,5*empates)/partidas jugadas, con piso exacto de 1/3 por rival. Independiente del puntaje configurable.
- Forfeits y pendientes no forman enfrentamientos de desempate ni integran el numerador/denominador de MWP. Los forfeits conservan puntos en la tabla.
- Rival repetido aporta por cada enfrentamiento jugado. Sin rivales computables, todos los desempates dan cero. Solo revisión vigente de resultados; correcciones recalculan todo.
- Byes aún no existen; su tratamiento requiere definición antes del Slice 11. No hay asignación automática ni finalización/publicación.

## Preservación histórica

`FrozenTournamentRules.TieBreakerFormulaVersion`: 0 conserva tabla solo por puntos; 1 aplica las fórmulas anteriores. La migración `AddTieBreakerFormulaVersion` asigna 0 a filas existentes. Nuevos inicios capturan 1; torneos ya iniciados no se migran a nuevas fórmulas. Versiones desconocidas fallan explícitamente. Cambios futuros deben agregar versión, nunca alterar la semántica de una existente.

El downgrade bloquea pérdida de versiones distintas de cero. Usar backup y migración correctiva hacia adelante si ya se inició un torneo con versión 1.

## Contrato y arquitectura

Se conserva GET `/api/tournaments/{id}/standings`, permisos de organizador propietario/inscriptos y no-store. Cada fila agrega `formulaVersion` y `tieBreakers` en orden, con `code`, `value` (redondeado a seis decimales), `numerator` y `denominator` como strings exactos. OMW se transmite como razón 0..1, React lo muestra como porcentaje.

Domain calcula fracciones con BigInteger y compara por productos cruzados, sin redondear para posiciones. Application transforma DTOs; EF/migración solo en Infrastructure. React muestra columnas y valores exactos en título, sin recalcular clasificación. No expone listas de mazos.

## Verificación

- Backend: 78/78 tests, PostgreSQL 16 real en WSL, sin omitidos; restore/compilación correctos.
- Pruebas de cadena secuencial, piso OMW, descarte, rivales repetidos, forfeits, correcciones, equivalencia/diferencias exactas, contrato HTTP y versión histórica.
- Frontend: 41/41 tests y build; columnas en orden, porcentaje, fracción exacta y vista histórica.
- Auditorías NuGet directas/transitivas y npm sin vulnerabilidades conocidas; arquitectura aprobada.
- Migración aplicada sobre base de pruebas y base manual existente. Navegador: torneo histórico conserva puestos/puntos y muestra aviso de versión 0.
- La nueva versión completa está cubierta por pruebas de dominio, HTTP/PostgreSQL y componentes; no se creó un torneo nuevo completo en la prueba manual.

Reproducir: `dotnet restore TournamentManager.slnx`, `dotnet tool restore`, `dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api`, `RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx --no-restore`; frontend `npm test -- --run` y `npm run build`. Definir conexión a base aislada. Auditorías: `dotnet list TournamentManager.slnx package --vulnerable --include-transitive`, `npm audit --audit-level=high`.

Backend nativo Windows continúa bloqueado por Smart App Control; se verifica en WSL. Sin dependencias nuevas. Revisión e integración pendientes, sin merge automático.
