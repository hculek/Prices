--
DO $$
DECLARE
    r RECORD;
    your_db_username text := 'prices_app';  -- << change here
	your_db_password text := '#prices_app'; -- << change here
BEGIN

EXECUTE format('CREATE USER %I WITH ENCRYPTED PASSWORD %L;', your_db_username, your_db_password);
EXECUTE format('GRANT ALL PRIVILEGES ON DATABASE prices_db TO %I;', your_db_username);

    FOR r IN
        SELECT schema_name
        FROM information_schema.schemata
        WHERE schema_name NOT IN ('pg_catalog', 'information_schema')
          AND schema_name NOT LIKE 'pg_toast%'
    LOOP
        EXECUTE format('GRANT ALL PRIVILEGES ON SCHEMA %I TO %I;', r.schema_name, your_db_username);
        EXECUTE format('GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA %I TO %I;', r.schema_name, your_db_username);
        EXECUTE format('GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA %I TO %I;', r.schema_name, your_db_username);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL PRIVILEGES ON TABLES TO %I;', r.schema_name, your_db_username);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL PRIVILEGES ON SEQUENCES TO %I;', r.schema_name, your_db_username);
    END LOOP;
END
$$;
--
