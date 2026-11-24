-- Uso administrativo explícito: psql ... -v account_id='UUID de una cuenta existente' -f scripts/grant-catalog-admin.sql
-- No ejecutar desde la API. Los permisos se revalidan en cada petición autenticada.
\set ON_ERROR_STOP on
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
VALUES (:'account_id'::uuid, '663147a8-7ec9-44dd-955c-ce06653cdf45'::uuid)
ON CONFLICT DO NOTHING;
