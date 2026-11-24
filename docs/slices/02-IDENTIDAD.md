# Slice 02 — Identidad mínima de jugador

## Alcance

Cuenta por email/contraseña, sesión por cookie, perfil propio y autorización de creación de juegos para `CatalogAdmin`. Decisiones confirmadas por el usuario el 2026-11-24. No se implementan mazos, invitados, torneos ni login con Google.

El criterio previo del roadmap sobre editar mazos propios dependía del Slice 03. En este slice se verifica aislamiento entre perfiles; la propiedad de mazos se probará al implementar la biblioteca.

## Contratos

| Método y ruta | Resultado |
|---|---|
| `GET /api/auth/csrf` | `200 { token }` y cookie antiforgery; no cachear |
| `POST /api/auth/register` | `{ email, password, displayName }`; `201`, `400` o `409`; no inicia sesión ni otorga roles |
| `POST /api/auth/login` | `{ email, password }`; `204` con cookie HttpOnly, o `401` genérico |
| `POST /api/auth/logout` | `204`; requiere sesión y CSRF |
| `GET /api/me` | `200 { id, displayName, canManageCatalog }`, `401` o `404`; solo perfil propio |
| `PUT /api/me` | `{ displayName }`; `200`, `400`, `401` o `404`; no acepta dueño/rol como autoridad |
| `POST /api/games` | Contrato de Slice 01; ahora exige sesión (`401`), rol (`403`) y CSRF (`400`) |

Todos los POST/PUT requieren la cabecera `X-CSRF-TOKEN` obtenida de `/api/auth/csrf` junto con sus cookies. Obtener un token nuevo después de login/logout porque está ligado a la identidad. El frontend lo obtiene antes de cada escritura. Login y registro tienen límite de 20 peticiones/minuto por IP y responden `429` al excederlo.

## Seguridad y límites

- ASP.NET Core Identity administra hash/salt de contraseñas, email único normalizado y bloqueo tras cinco contraseñas fallidas durante quince minutos. Contraseñas de 12 a 128 caracteres; sin imponer composición artificial.
- Cuenta Identity y perfil se crean en una transacción; la unicidad de email y de cuenta/perfil también se impone en PostgreSQL.
- Cookie de sesión HttpOnly, SameSite=Lax, duración máxima de ocho horas sin renovación deslizante. Secure obligatorio fuera de Development. CSRF es obligatorio incluso en login y registro.
- En cada petición autenticada se comprueba que la cuenta exista, no esté bloqueada y se vuelve a cargar su rol. No se confía en roles viejos de la cookie para autorizar escrituras.
- Logout elimina la cookie del navegador. No hay todavía gestión de sesiones por dispositivo ni revocación individual de una cookie copiada previamente; tampoco cambio/recuperación de contraseña, verificación de email ni MFA.
- No publicar esta versión en Internet. Antes se deben cerrar los flujos pendientes de cuenta, configurar HTTPS, persistencia/protección de Data Protection keys, proxy confiable y límites de abuso adecuados al despliegue.

## Administrador del catálogo

Registrar primero una cuenta desde la UI. Un operador de base de datos verifica la cuenta y consulta su ID:

```sql
SELECT "Id", "Email" FROM "AspNetUsers" ORDER BY "Email";
```

Asignar el rol a ese ID expresamente, desde una conexión administrativa a la base correcta:

```sh
psql "$DATABASE_URL" -v account_id='UUID-DE-LA-CUENTA' -f scripts/grant-catalog-admin.sql
```

El script no crea usuarios ni contraseñas. Un ID inexistente falla por FK. Para retirar el rol:

```sql
DELETE FROM "AspNetUserRoles"
WHERE "UserId" = 'UUID-DE-LA-CUENTA'::uuid
  AND "RoleId" = '663147a8-7ec9-44dd-955c-ce06653cdf45'::uuid;
```

Los permisos se revalidan en la siguiente petición. Recargar la página actualiza también la UI. No existe endpoint público de asignación de roles ni administrador automático por email.

## Verificación reproducible

```sh
dotnet restore TournamentManager.slnx
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend/TournamentManager.Infrastructure --startup-project backend/TournamentManager.Api
RUN_POSTGRES_INTEGRATION=true dotnet test TournamentManager.slnx
python3 scripts/verify_structure.py
dotnet list TournamentManager.slnx package --vulnerable --include-transitive
cd frontend
npm ci
npm test -- --run
npm run build
npm audit
```

Configurar `ConnectionStrings__TournamentDb` con una base aislada antes de migrar/probar. Sin `RUN_POSTGRES_INTEGRATION=true`, los tests PostgreSQL existentes retornan temprano: un total verde no acredita integración real.

Verificación manual: registrar dos cuentas, iniciar sesión en navegadores separados, cambiar un nombre y verificar que el otro perfil no cambie; comprobar que un jugador no puede crear juegos; asignar `CatalogAdmin`, recargar, crear un juego; retirar el rol y comprobar `403`; cerrar sesión y comprobar `401` al consultar `/api/me`.

## Mejora futura

Login con Google, solicitado y registrado en `docs/ROADMAP.md`. Requiere decidir vinculación/desvinculación segura y recuperación antes de implementarlo; no fusionar por email sin prueba de propiedad.

## Resultados locales — 2026-11-24

- Backend: Ubuntu 24.04/WSL2, SDK .NET 10.0.115, PostgreSQL 16.15, base aislada `slice02_tests`. Restore/build correctos; 32/32 tests con `RUN_POSTGRES_INTEGRATION=true`, incluyendo cuentas, lockout, permiso de catálogo y persistencia real. Migraciones aplicadas; EF no detecta cambios de modelo pendientes.
- Frontend: 7/7 tests y build TypeScript/Vite correctos en Windows. Auditorías npm y NuGet (incluidas transitivas) sin vulnerabilidades reportadas.
- Límites de arquitectura comprobados con `scripts/verify_structure.py`.
- Navegador contra API en WSL: registro/login, edición de perfil, persistencia al recargar, logout y ausencia del formulario de creación para el jugador normal verificados. Aislamiento entre dos cuentas y autorización administrativa cubiertos por tests HTTP/integración.
- Windows nativo mantiene el bloqueo de Smart App Control sobre la DLL de dominio; no se modificó esa protección. El CI remoto con PostgreSQL 17 y la revisión del cambio siguen pendientes.
