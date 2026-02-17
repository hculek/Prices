--
-- PostgreSQL database dump
--

-- Dumped from database version 17.5
-- Dumped by pg_dump version 17.5

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: admin; Type: SCHEMA; Schema: -; Owner: hrvoje
--

CREATE SCHEMA admin;


ALTER SCHEMA admin OWNER TO hrvoje;

--
-- Name: crm; Type: SCHEMA; Schema: -; Owner: hrvoje
--

CREATE SCHEMA crm;


ALTER SCHEMA crm OWNER TO hrvoje;

--
-- Name: data_presentation; Type: SCHEMA; Schema: -; Owner: hrvoje
--

CREATE SCHEMA data_presentation;


ALTER SCHEMA data_presentation OWNER TO hrvoje;

--
-- Name: SCHEMA data_presentation; Type: COMMENT; Schema: -; Owner: hrvoje
--

COMMENT ON SCHEMA data_presentation IS 'Retailers Pricelists App';


--
-- Name: archive_pricelist(); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.archive_pricelist()
    LANGUAGE plpgsql
    AS $$
BEGIN
INSERT INTO data_presentation.imp_pricelist_arh
SELECT * FROM data_presentation.imp_pricelist;
TRUNCATE data_presentation.imp_pricelist;
END;
$$;


ALTER PROCEDURE data_presentation.archive_pricelist() OWNER TO hrvoje;

--
-- Name: batch_run_prices(); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.batch_run_prices()
    LANGUAGE plpgsql
    AS $$
BEGIN
	--archive current data
	CALL data_presentation.archive_pricelist();

	--import new data
	CALL data_presentation.csv_import_all();

	--generate events for detected changes
	CALL data_presentation.create_events();
END;
$$;


ALTER PROCEDURE data_presentation.batch_run_prices() OWNER TO hrvoje;

--
-- Name: create_events(); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.create_events()
    LANGUAGE plpgsql
    AS $$
DECLARE
unit RECORD;

BEGIN
--select archived data for comparison
FOR unit IN 
WITH price_archive AS (
SELECT
 	retailer_id,
	unit_id,
	product_name,
	product_brand,
	net_measure,
	retail_price,
	price_per_measure_unit,
	barcode,
	date_processed,
	checksum,
	row_number() OVER (partition by retailer_id, unit_id, barcode order by date_processed desc) as row_num
FROM data_presentation.imp_pricelist_arh WHERE barcode IS NOT NULL order by date_processed desc)
--select current data for comparison and join archived data
SELECT
	p.retailer_id retailer_id_new,
	p.unit_id unit_id_new,
	p.product_name product_name_new,
	p.product_brand product_brand_new,
	p.net_measure net_measure_new,
	p.retail_price retail_price_new,
	p.price_per_measure_unit price_per_measure_unit_new,
	p.barcode barcode_new,
	p.date_processed date_processed_new,
	p.checksum checksum_new,
 	a.retailer_id retailer_id_old,
	a.unit_id unit_id_old,
	a.product_name product_name_old,
	a.product_brand product_brand_old,
	a.net_measure net_measure_old,
	a.retail_price retail_price_old,
	a.price_per_measure_unit price_per_measure_unit_old,
	a.barcode barcode_old,
	a.date_processed date_processed_old,
	a.checksum checksum_old
FROM data_presentation.imp_pricelist p
LEFT JOIN price_archive a 
	ON a.retailer_id = p.retailer_id 
	AND a.unit_id = p.unit_id 
	AND a.barcode = p.barcode
	AND a.row_num = 1
WHERE a.barcode IS NOT NULL

--compare data, insert events for changed data
LOOP 
	IF COALESCE(unit.checksum_new, '0') <> COALESCE(unit.checksum_old, '0') THEN
		-- event 1 | price increase
		IF COALESCE(unit.retail_price_new, MONEY '0') > COALESCE(unit.retail_price_old, MONEY '0')
			THEN INSERT INTO data_presentation.prices_events 
			(event_id, event_date, barcode, product_name, product_brand, old_price, new_price, retailer_id, unit_id)
			VALUES
			(1, now(), unit.barcode_new, unit.product_name_new, unit.product_brand_new, unit.retail_price_old, unit.retail_price_new, unit.retailer_id_new, unit.unit_id_new);
			
		-- event 2 | price decrease
		ELSIF COALESCE(unit.retail_price_new, MONEY '0') < COALESCE(unit.retail_price_old, MONEY '0')
			THEN INSERT INTO data_presentation.prices_events 
			(event_id, event_date, barcode, product_name, product_brand, old_price, new_price, retailer_id, unit_id)
			VALUES
			(2, now(), unit.barcode_new, unit.product_name_new, unit.product_brand_new, unit.retail_price_old, unit.retail_price_new, unit.retailer_id_new, unit.unit_id_new);
			
		-- event 3 | new product
		ELSIF COALESCE(unit.checksum_old, '0') = '0'
			THEN INSERT INTO data_presentation.prices_events 
			(event_id, event_date, barcode, product_name, product_brand, old_price, new_price, retailer_id, unit_id)
			VALUES
			(3, now(), unit.barcode_new, unit.product_name_new, unit.product_brand_new, unit.retail_price_old, unit.retail_price_new, unit.retailer_id_new, unit.unit_id_new);
			
		-- event 4 | discontinued product
		ELSIF COALESCE(unit.checksum_new, '0') = '0'
			THEN INSERT INTO data_presentation.prices_events 
			(event_id, event_date, barcode, product_name, product_brand, old_price, new_price, retailer_id, unit_id)
			VALUES
			(3, now(), unit.barcode_new, unit.product_name_new, unit.product_brand_new, unit.retail_price_old, unit.retail_price_new, unit.retailer_id_new, unit.unit_id_new);
		END IF;
	END IF;
END LOOP;

END;
$$;


ALTER PROCEDURE data_presentation.create_events() OWNER TO hrvoje;

--
-- Name: csv_import_all(); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.csv_import_all()
    LANGUAGE plpgsql
    AS $$
DECLARE
unit RECORD;
BEGIN
--get all active retailers and business units data
FOR unit IN 
SELECT 
bd.retailer_name,
bd.retailer_id, 
ud.unit_id,
bd.csv_directory
FROM crm.retailer_basic_data bd
LEFT JOIN crm.retailer_business_unit_data ud on ud.retailer_id = bd.retailer_id and ud.is_active = true
WHERE bd.is_active = true

--import data from csv to table
LOOP
IF unit.retailer_name = 'KONZUM' THEN CALL data_presentation.csv_to_pricelist_konzum(unit.csv_directory, unit.retailer_id, unit.unit_id);
ELSIF unit.retailer_name = 'KTC' THEN CALL data_presentation.csv_to_pricelist_ktc(unit.csv_directory, unit.retailer_id, unit.unit_id);
ELSIF unit.retailer_name ='LIDL' THEN CALL data_presentation.csv_to_pricelist_lidl(unit.csv_directory, unit.retailer_id, unit.unit_id);
ELSIF unit.retailer_name = 'DM' THEN CALL data_presentation.csv_to_pricelist_dm(unit.csv_directory, unit.retailer_id, unit.unit_id);
ELSE 
	RAISE NOTICE 'No procedure for retailer: % with id: %', 
	unit.retailer_name, 
	unit.retailer_id;
END IF;

END LOOP;
END;
$$;


ALTER PROCEDURE data_presentation.csv_import_all() OWNER TO hrvoje;

--
-- Name: csv_to_pricelist_dm(text, integer, integer); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.csv_to_pricelist_dm(IN csv_directory text, IN retailer_id integer, IN unit_id integer)
    LANGUAGE plpgsql
    AS $$
DECLARE
	p_file_path TEXT;

BEGIN
	p_file_path := csv_directory || '\\' || retailer_id || '.csv';
	
-- temp table for import only
CREATE TEMP TABLE cjenik_dm(
naziv_proizvoda TEXT,
sifra_proizvoda TEXT,
marka_proizvoda TEXT,
barkod TEXT,
kategorija_proizvoda TEXT,
neto_kolicina TEXT,
jedinica_mjere TEXT,
cijena_za_jedinicu_mjere TEXT,
dostupno_samo_online TEXT,
maloprodajna_cijena TEXT,
mpc_akcija TEXT,
najniza_cijena_30_dana TEXT,
sidrena_cijena TEXT
);

--copy from csv to temp table
EXECUTE format(
		'COPY cjenik_dm FROM %L WITH (FORMAT csv, HEADER true, DELIMITER %L, ENCODING %L, QUOTE %L)',
		p_file_path, ';', 'UTF8', '"'
);

--from temp table to imp table
INSERT INTO data_presentation.imp_pricelist 
(retailer_id, unit_id, product_name, product_brand, product_sku,
net_measure, retail_price, price_per_measure_unit,
barcode, date_processed, checksum)
SELECT 
retailer_id, unit_id, UPPER(naziv_proizvoda), UPPER(marka_proizvoda), sifra_proizvoda,
neto_kolicina, 
CASE 
WHEN maloprodajna_cijena IS NOT NULL THEN REPLACE(maloprodajna_cijena, '.', '')::MONEY
ELSE REPLACE(mpc_akcija, '.', '')::MONEY
END AS mpc, 
REPLACE(cijena_za_jedinicu_mjere, '.', '')::MONEY,
barkod::varchar(13),
now(),
md5(naziv_proizvoda|| neto_kolicina || COALESCE(maloprodajna_cijena,'') || COALESCE(mpc_akcija,''))
FROM cjenik_dm WHERE (dostupno_samo_online is null or dostupno_samo_online <> 'Da');

DROP TABLE cjenik_dm;
END;
$$;


ALTER PROCEDURE data_presentation.csv_to_pricelist_dm(IN csv_directory text, IN retailer_id integer, IN unit_id integer) OWNER TO hrvoje;

--
-- Name: csv_to_pricelist_konzum(text, integer, integer); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.csv_to_pricelist_konzum(IN csv_directory text, IN retailer_id integer, IN unit_id integer)
    LANGUAGE plpgsql
    AS $$
DECLARE
	p_file_path TEXT;

BEGIN
	p_file_path := csv_directory || '\\' || retailer_id || '_' || unit_id || '.csv';
	
-- temp tablica za import, ne specificiramo shemu zato sto mora ici u temp shemu
CREATE TEMP TABLE cjenik_konzum(
naziv_proizvoda TEXT,
sifra_proizvoda TEXT,
marka_proizvoda TEXT,
neto_kolicina TEXT,
jedinica_mjere TEXT,
maloprodajna_cijena TEXT,
cijena_za_jedinicu_mjere TEXT,
mpc_akcija TEXT,
najniza_cijena_30_dana TEXT,
sidrena_cijena TEXT,
barkod TEXT,
kategorija_proizvoda TEXT);

--kopiranje csv u temp tablicu
EXECUTE format(
		'COPY cjenik_konzum FROM %L WITH (FORMAT csv, HEADER true, DELIMITER %L, ENCODING %L)',
		p_file_path, ',', 'UTF8'
);

--iz temp u imp konzum
INSERT INTO data_presentation.imp_pricelist 
(retailer_id, unit_id, product_name, product_brand, product_sku,
net_measure, retail_price, price_per_measure_unit, 
barcode, date_processed, checksum)
SELECT 
retailer_id, unit_id, UPPER(naziv_proizvoda), UPPER(marka_proizvoda), sifra_proizvoda,
neto_kolicina, 
CASE 
WHEN maloprodajna_cijena IS NOT NULL THEN REPLACE(REPLACE(maloprodajna_cijena, ',', ''), '.', ',')::MONEY
ELSE REPLACE(REPLACE(mpc_akcija, ',', ''), '.', ',')::MONEY
END AS mpc, 
REPLACE(REPLACE(cijena_za_jedinicu_mjere, ',', ''), '.', ',')::MONEY,
barkod::varchar(13),
now(),
md5(naziv_proizvoda|| neto_kolicina || COALESCE(maloprodajna_cijena,'') || COALESCE(mpc_akcija,''))
FROM cjenik_konzum;

DROP TABLE cjenik_konzum;
END;
$$;


ALTER PROCEDURE data_presentation.csv_to_pricelist_konzum(IN csv_directory text, IN retailer_id integer, IN unit_id integer) OWNER TO hrvoje;

--
-- Name: csv_to_pricelist_ktc(text, integer, integer); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.csv_to_pricelist_ktc(IN csv_directory text, IN retailer_id integer, IN unit_id integer)
    LANGUAGE plpgsql
    AS $$
DECLARE
	p_file_path TEXT;

BEGIN
	p_file_path := csv_directory || '\\' || retailer_id || '_' || unit_id || '.csv';
	
-- temp table for import only
CREATE TEMP TABLE cjenik_ktc(
naziv_proizvoda TEXT,
sifra_proizvoda TEXT,
marka_proizvoda TEXT,
neto_kolicina TEXT,
jedinica_mjere TEXT,
maloprodajna_cijena TEXT,
cijena_za_jedinicu_mjere TEXT,
barkod TEXT,
kategorija_proizvoda TEXT,
najniza_cijena_30_dana TEXT,
mpc_akcija TEXT
);

--copy from csv to temp table
EXECUTE format(
		'COPY cjenik_ktc FROM %L WITH (FORMAT csv, HEADER true, DELIMITER %L, ENCODING %L, QUOTE %L)',
		p_file_path, ';', 'WIN1252', /*fix for quotes that cause import errors*/ E'\b'
);

--from temp table to imp table
INSERT INTO data_presentation.imp_pricelist 
(retailer_id, unit_id, product_name, product_brand, product_sku,
net_measure, retail_price, price_per_measure_unit, 
barcode, date_processed, checksum)
SELECT 
retailer_id, unit_id, UPPER(naziv_proizvoda), UPPER(marka_proizvoda), sifra_proizvoda,
neto_kolicina||jedinica_mjere, 
CASE
WHEN maloprodajna_cijena IS NOT NULL THEN REPLACE(REPLACE(maloprodajna_cijena, ',', ''), '.', ',')::MONEY
ELSE REPLACE(REPLACE(mpc_akcija, ',', ''), '.', ',')::MONEY
END AS MPC,
REPLACE(REPLACE(cijena_za_jedinicu_mjere, ',', ''), '.', ',')::MONEY, 
REPLACE(barkod, '''', '')::varchar(13),
now(),
md5(naziv_proizvoda|| neto_kolicina || COALESCE(maloprodajna_cijena,''))
FROM cjenik_ktc;

DROP TABLE cjenik_ktc;
END;
$$;


ALTER PROCEDURE data_presentation.csv_to_pricelist_ktc(IN csv_directory text, IN retailer_id integer, IN unit_id integer) OWNER TO hrvoje;

--
-- Name: csv_to_pricelist_lidl(text, integer, integer); Type: PROCEDURE; Schema: data_presentation; Owner: hrvoje
--

CREATE PROCEDURE data_presentation.csv_to_pricelist_lidl(IN csv_directory text, IN retailer_id integer, IN unit_id integer)
    LANGUAGE plpgsql
    AS $$
DECLARE
	p_file_path TEXT;

BEGIN
	p_file_path := csv_directory || '\\' || retailer_id || '_' || unit_id || '.csv';
	
-- temp table for import only
CREATE TEMP TABLE cjenik_lidl(
naziv_proizvoda TEXT,
sifra_proizvoda TEXT,
neto_kolicina TEXT,
jedinica_mjere TEXT,
marka_proizvoda TEXT,
maloprodajna_cijena TEXT,
mpc_akcija TEXT,
najniza_cijena_30_dana TEXT,
cijena_za_jedinicu_mjere TEXT,
barkod TEXT,
kategorija_proizvoda TEXT,
sidrena_cijena TEXT
);

--copy from csv to temp table
EXECUTE format(
		'COPY cjenik_lidl FROM %L WITH (FORMAT csv, HEADER true, DELIMITER %L, ENCODING %L, QUOTE %L)',
		p_file_path, ',', 'WIN1250', '"'
);

--from temp table to imp table
INSERT INTO data_presentation.imp_pricelist 
(retailer_id, unit_id, product_name, product_brand, product_sku,
net_measure, retail_price, price_per_measure_unit,
barcode, date_processed, checksum)
SELECT 
retailer_id, unit_id, UPPER(naziv_proizvoda), UPPER(marka_proizvoda), sifra_proizvoda,
neto_kolicina, 
CASE 
WHEN maloprodajna_cijena IS NOT NULL THEN REPLACE(REPLACE(maloprodajna_cijena, ',', ''), '.', ',')::MONEY
ELSE REPLACE(REPLACE(mpc_akcija, ',', ''), '.', ',')::MONEY
END AS mpc, 
REPLACE(REPLACE(cijena_za_jedinicu_mjere, ',', ''), '.', ',')::MONEY,
barkod::varchar(13),
now(),
md5(naziv_proizvoda|| neto_kolicina || COALESCE(maloprodajna_cijena,'') || COALESCE(mpc_akcija,''))
FROM cjenik_lidl;

DROP TABLE cjenik_lidl;
END;
$$;


ALTER PROCEDURE data_presentation.csv_to_pricelist_lidl(IN csv_directory text, IN retailer_id integer, IN unit_id integer) OWNER TO hrvoje;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: logs_winservice; Type: TABLE; Schema: admin; Owner: hrvoje
--

CREATE TABLE admin.logs_winservice (
    machine_name character varying(128),
    log_date timestamp without time zone,
    log_level character varying(10),
    message character varying(512),
    logger character varying(128),
    callsite character varying(256),
    exception text
);
ALTER TABLE ONLY admin.logs_winservice ALTER COLUMN exception SET STORAGE PLAIN;


ALTER TABLE admin.logs_winservice OWNER TO hrvoje;

--
-- Name: app_follow_list; Type: TABLE; Schema: crm; Owner: hrvoje
--

CREATE TABLE crm.app_follow_list (
    user_id integer,
    barcode text,
    product_name character varying(512),
    net_measure character varying(16),
    date_followed date
);


ALTER TABLE crm.app_follow_list OWNER TO hrvoje;

--
-- Name: events_data; Type: TABLE; Schema: crm; Owner: hrvoje
--

CREATE TABLE crm.events_data (
    event_id integer,
    event_description character varying(56)
);


ALTER TABLE crm.events_data OWNER TO hrvoje;

--
-- Name: retailer_basic_data; Type: TABLE; Schema: crm; Owner: hrvoje
--

CREATE TABLE crm.retailer_basic_data (
    retailer_id integer NOT NULL,
    retailer_name character varying(48),
    csv_directory character varying(512) NOT NULL,
    is_active boolean DEFAULT true NOT NULL
);


ALTER TABLE crm.retailer_basic_data OWNER TO hrvoje;

--
-- Name: retailer_basic_data_retailer_id_seq; Type: SEQUENCE; Schema: crm; Owner: hrvoje
--

ALTER TABLE crm.retailer_basic_data ALTER COLUMN retailer_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME crm.retailer_basic_data_retailer_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: retailer_business_unit_data; Type: TABLE; Schema: crm; Owner: hrvoje
--

CREATE TABLE crm.retailer_business_unit_data (
    is_active boolean,
    retailer_id integer,
    unit_id integer NOT NULL,
    lookup character varying(512),
    filename character varying(512),
    street_name character varying(256) NOT NULL,
    street_number character varying(16) NOT NULL,
    zip_code character varying(5) NOT NULL,
    settlement_name character varying(64) NOT NULL
);


ALTER TABLE crm.retailer_business_unit_data OWNER TO hrvoje;

--
-- Name: retailer_business_unit_data_unit_id_seq; Type: SEQUENCE; Schema: crm; Owner: hrvoje
--

ALTER TABLE crm.retailer_business_unit_data ALTER COLUMN unit_id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME crm.retailer_business_unit_data_unit_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: prices_events; Type: TABLE; Schema: data_presentation; Owner: hrvoje
--

CREATE TABLE data_presentation.prices_events (
    event_id integer,
    event_date date,
    barcode character varying(13),
    product_name character varying(512),
    product_brand character varying(512),
    old_price money,
    new_price money,
    retailer_id integer,
    unit_id integer
);


ALTER TABLE data_presentation.prices_events OWNER TO hrvoje;

--
-- Name: discontinued_products; Type: VIEW; Schema: data_presentation; Owner: hrvoje
--

CREATE VIEW data_presentation.discontinued_products AS
 SELECT d.event_description,
    e.event_date,
    e.barcode,
    e.product_name,
    e.product_brand,
    e.old_price,
    e.new_price,
    rb.retailer_name,
    (((ru.street_name)::text || ' '::text) || (ru.street_number)::text) AS address,
    ru.settlement_name,
    ru.zip_code
   FROM (((data_presentation.prices_events e
     RIGHT JOIN crm.events_data d ON ((d.event_id = e.event_id)))
     LEFT JOIN crm.retailer_basic_data rb ON ((rb.retailer_id = e.retailer_id)))
     LEFT JOIN crm.retailer_business_unit_data ru ON ((ru.unit_id = e.unit_id)))
  WHERE (d.event_id = 4);


ALTER VIEW data_presentation.discontinued_products OWNER TO hrvoje;

--
-- Name: imp_pricelist; Type: TABLE; Schema: data_presentation; Owner: hrvoje
--

CREATE TABLE data_presentation.imp_pricelist (
    retailer_id integer,
    unit_id integer,
    product_name character varying(512),
    product_brand character varying(512),
    product_sku character varying(512),
    net_measure text,
    retail_price money,
    price_per_measure_unit money,
    barcode character varying(13),
    date_processed date,
    checksum character(32)
);


ALTER TABLE data_presentation.imp_pricelist OWNER TO hrvoje;

--
-- Name: imp_pricelist_arh; Type: TABLE; Schema: data_presentation; Owner: hrvoje
--

CREATE TABLE data_presentation.imp_pricelist_arh (
    retailer_id integer,
    unit_id integer,
    product_name character varying(512),
    product_brand character varying(512),
    product_sku character varying(512),
    net_measure text,
    retail_price money,
    price_per_measure_unit money,
    barcode character varying(13),
    date_processed date,
    checksum character(32)
);


ALTER TABLE data_presentation.imp_pricelist_arh OWNER TO hrvoje;

--
-- Name: new_products; Type: VIEW; Schema: data_presentation; Owner: hrvoje
--

CREATE VIEW data_presentation.new_products AS
 SELECT d.event_description,
    e.event_date,
    e.barcode,
    e.product_name,
    e.product_brand,
    e.old_price,
    e.new_price,
    rb.retailer_name,
    (((ru.street_name)::text || ' '::text) || (ru.street_number)::text) AS address,
    ru.settlement_name,
    ru.zip_code
   FROM (((data_presentation.prices_events e
     RIGHT JOIN crm.events_data d ON ((d.event_id = e.event_id)))
     LEFT JOIN crm.retailer_basic_data rb ON ((rb.retailer_id = e.retailer_id)))
     LEFT JOIN crm.retailer_business_unit_data ru ON ((ru.unit_id = e.unit_id)))
  WHERE (d.event_id = 3);


ALTER VIEW data_presentation.new_products OWNER TO hrvoje;

--
-- Name: price_change; Type: VIEW; Schema: data_presentation; Owner: hrvoje
--

CREATE VIEW data_presentation.price_change AS
 SELECT d.event_description,
    e.event_date,
    e.barcode,
    e.product_name,
    e.product_brand,
    e.old_price,
    e.new_price,
    rb.retailer_name,
    ((ru.street_name)::text || (ru.street_number)::text) AS address,
    ru.settlement_name,
    ru.zip_code
   FROM (((data_presentation.prices_events e
     RIGHT JOIN crm.events_data d ON ((d.event_id = e.event_id)))
     LEFT JOIN crm.retailer_basic_data rb ON ((rb.retailer_id = e.retailer_id)))
     LEFT JOIN crm.retailer_business_unit_data ru ON ((ru.unit_id = e.unit_id)));


ALTER VIEW data_presentation.price_change OWNER TO hrvoje;

--
-- Name: price_decrease; Type: VIEW; Schema: data_presentation; Owner: hrvoje
--

CREATE VIEW data_presentation.price_decrease AS
 SELECT d.event_description,
    e.event_date,
    e.barcode,
    e.product_name,
    e.product_brand,
    e.old_price,
    e.new_price,
    rb.retailer_name,
    (((ru.street_name)::text || ' '::text) || (ru.street_number)::text) AS address,
    ru.settlement_name,
    ru.zip_code
   FROM (((data_presentation.prices_events e
     RIGHT JOIN crm.events_data d ON ((d.event_id = e.event_id)))
     LEFT JOIN crm.retailer_basic_data rb ON ((rb.retailer_id = e.retailer_id)))
     LEFT JOIN crm.retailer_business_unit_data ru ON ((ru.unit_id = e.unit_id)))
  WHERE (d.event_id = 2);


ALTER VIEW data_presentation.price_decrease OWNER TO hrvoje;

--
-- Name: price_increase; Type: VIEW; Schema: data_presentation; Owner: hrvoje
--

CREATE VIEW data_presentation.price_increase AS
 SELECT d.event_description,
    e.event_date,
    e.barcode,
    e.product_name,
    e.product_brand,
    e.old_price,
    e.new_price,
    rb.retailer_name,
    (((ru.street_name)::text || ' '::text) || (ru.street_number)::text) AS address,
    ru.settlement_name,
    ru.zip_code
   FROM (((data_presentation.prices_events e
     RIGHT JOIN crm.events_data d ON ((d.event_id = e.event_id)))
     LEFT JOIN crm.retailer_basic_data rb ON ((rb.retailer_id = e.retailer_id)))
     LEFT JOIN crm.retailer_business_unit_data ru ON ((ru.unit_id = e.unit_id)))
  WHERE (d.event_id = 1);


ALTER VIEW data_presentation.price_increase OWNER TO hrvoje;

--
-- Name: retailers_units; Type: VIEW; Schema: data_presentation; Owner: hrvoje
--

CREATE VIEW data_presentation.retailers_units AS
 SELECT r.retailer_id,
    r.retailer_name,
    b.unit_id,
    b.filename,
    b.is_active
   FROM (crm.retailer_business_unit_data b
     LEFT JOIN crm.retailer_basic_data r ON ((r.retailer_id = b.retailer_id)));


ALTER VIEW data_presentation.retailers_units OWNER TO hrvoje;

--
-- Name: retailer_basic_data retailer_basic_data_pkey; Type: CONSTRAINT; Schema: crm; Owner: hrvoje
--

ALTER TABLE ONLY crm.retailer_basic_data
    ADD CONSTRAINT retailer_basic_data_pkey PRIMARY KEY (retailer_id);


--
-- Name: retailer_business_unit_data retailer_business_unit_data_pkey; Type: CONSTRAINT; Schema: crm; Owner: hrvoje
--

ALTER TABLE ONLY crm.retailer_business_unit_data
    ADD CONSTRAINT retailer_business_unit_data_pkey PRIMARY KEY (unit_id);


--
-- Name: retailer_business_unit_data fk_retailer_id; Type: FK CONSTRAINT; Schema: crm; Owner: hrvoje
--

ALTER TABLE ONLY crm.retailer_business_unit_data
    ADD CONSTRAINT fk_retailer_id FOREIGN KEY (retailer_id) REFERENCES crm.retailer_basic_data(retailer_id);


--
-- Name: SCHEMA admin; Type: ACL; Schema: -; Owner: hrvoje
--

GRANT ALL ON SCHEMA admin TO prices_app;


--
-- Name: SCHEMA crm; Type: ACL; Schema: -; Owner: hrvoje
--

GRANT ALL ON SCHEMA crm TO prices_app;


--
-- Name: TABLE logs_winservice; Type: ACL; Schema: admin; Owner: hrvoje
--

GRANT ALL ON TABLE admin.logs_winservice TO prices_app;


--
-- Name: TABLE retailer_basic_data; Type: ACL; Schema: crm; Owner: hrvoje
--

GRANT ALL ON TABLE crm.retailer_basic_data TO prices_app;


--
-- Name: SEQUENCE retailer_basic_data_retailer_id_seq; Type: ACL; Schema: crm; Owner: hrvoje
--

GRANT ALL ON SEQUENCE crm.retailer_basic_data_retailer_id_seq TO prices_app;


--
-- Name: TABLE retailer_business_unit_data; Type: ACL; Schema: crm; Owner: hrvoje
--

GRANT ALL ON TABLE crm.retailer_business_unit_data TO prices_app;


--
-- Name: SEQUENCE retailer_business_unit_data_unit_id_seq; Type: ACL; Schema: crm; Owner: hrvoje
--

GRANT ALL ON SEQUENCE crm.retailer_business_unit_data_unit_id_seq TO prices_app;


--
-- PostgreSQL database dump complete
--

