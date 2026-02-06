DO
$do$
BEGIN
   IF EXISTS (SELECT FROM pg_database WHERE datname = 'identity-db') THEN
      RAISE NOTICE 'Database already exists';  -- Optional
   ELSE
      PERFORM dblink_exec('dbname=' || current_database(), 'CREATE DATABASE "identity-db"');
   END IF;

   IF EXISTS (SELECT FROM pg_database WHERE datname = 'app-db') THEN
      RAISE NOTICE 'Database already exists';  -- Optional
   ELSE
      PERFORM dblink_exec('dbname=' || current_database(), 'CREATE DATABASE "app-db"');
   END IF;
   
   IF EXISTS (SELECT FROM pg_database WHERE datname = 'telemetry-db') THEN
      RAISE NOTICE 'Database already exists';  -- Optional
   ELSE
      PERFORM dblink_exec('dbname=' || current_database(), 'CREATE DATABASE "telemetry-db"');
   END IF;
END
$do$;
