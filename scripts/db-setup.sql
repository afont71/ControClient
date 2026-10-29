-- Base de datos y roles de ControClient
-- Ejecutar como superusuario:
--   & 'C:\Program Files\PostgreSQL\18\bin\psql.exe' -U postgres -h 127.0.0.1 -f scripts/db-setup.sql
-- Cambia las dos contraseñas antes de ejecutarlo y no las guardes en el repositorio.

CREATE ROLE contoclient_owner LOGIN PASSWORD 'CAMBIAR_OWNER';
CREATE ROLE contoclient_app   LOGIN PASSWORD 'CAMBIAR_APP';

CREATE DATABASE contoclient
  OWNER contoclient_owner
  ENCODING 'UTF8'
  TEMPLATE template0;

\connect contoclient

-- El esquema deja de ser público: solo el dueño crea objetos.
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE, CREATE ON SCHEMA public TO contoclient_owner;
GRANT USAGE ON SCHEMA public TO contoclient_app;
GRANT CONNECT ON DATABASE contoclient TO contoclient_app;

-- La aplicación solo puede leer y escribir datos, nunca cambiar el esquema.
ALTER DEFAULT PRIVILEGES FOR ROLE contoclient_owner IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO contoclient_app;
ALTER DEFAULT PRIVILEGES FOR ROLE contoclient_owner IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO contoclient_app;

-- Comprobación: el dueño debe poder crear tablas y la app no.
-- (Se ejecuta después de las migraciones, no aquí.)
