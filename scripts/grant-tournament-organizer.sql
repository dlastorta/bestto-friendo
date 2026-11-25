-- Asignación administrativa a una cuenta existente:
-- psql ... -v account_id='UUID de la cuenta' -f scripts/grant-tournament-organizer.sql
-- No ejecutar desde la API. El rol se revalida en cada petición autenticada.
\set ON_ERROR_STOP on
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
VALUES (:'account_id'::uuid, '781621bb-bde8-442c-b945-d6767f49271a'::uuid)
ON CONFLICT DO NOTHING;
