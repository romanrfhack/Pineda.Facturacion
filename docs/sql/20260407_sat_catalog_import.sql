-- Proposal only.
-- Load strategy:
--   official SAT XLS/XLSX -> one CSV per sheet -> staging -> upsert canonical tables.

SET NAMES utf8mb4;

DROP TABLE IF EXISTS stg_sat_clave_prodserv;
CREATE TABLE stg_sat_clave_prodserv (
    clave VARCHAR(20) NULL,
    descripcion VARCHAR(500) NULL,
    keywords VARCHAR(1000) NULL,
    estado VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS stg_sat_clave_unidad;
CREATE TABLE stg_sat_clave_unidad (
    clave VARCHAR(20) NULL,
    nombre VARCHAR(255) NULL,
    descripcion VARCHAR(500) NULL,
    simbolo VARCHAR(50) NULL,
    estado VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Replace file paths with local CSV exports.
-- Example export:
--   xlsx -> prodserv.csv
--   xlsx -> unidad.csv
--
-- LOAD DATA LOCAL INFILE '/tmp/prodserv.csv'
-- INTO TABLE stg_sat_clave_prodserv
-- FIELDS TERMINATED BY ','
-- OPTIONALLY ENCLOSED BY '"'
-- LINES TERMINATED BY '\n'
-- IGNORE 1 LINES
-- (clave, descripcion, keywords, estado);
--
-- LOAD DATA LOCAL INFILE '/tmp/unidad.csv'
-- INTO TABLE stg_sat_clave_unidad
-- FIELDS TERMINATED BY ','
-- OPTIONALLY ENCLOSED BY '"'
-- LINES TERMINATED BY '\n'
-- IGNORE 1 LINES
-- (clave, nombre, descripcion, simbolo, estado);

-- Register import audit rows.
INSERT INTO sat_catalog_imports (
    catalog_name,
    source_authority,
    source_release,
    source_format,
    source_file_name,
    source_file_sha256,
    source_sheet_name,
    row_count,
    imported_at_utc,
    imported_by,
    notes
)
VALUES (
    'prodserv',
    'SAT',
    'REPLACE_RELEASE',
    'CSV',
    'prodserv.csv',
    'REPLACE_SHA256',
    'prodserv',
    (SELECT COUNT(*) FROM stg_sat_clave_prodserv),
    UTC_TIMESTAMP(6),
    'migration',
    'Loaded from official SAT spreadsheet export'
);

SET @prodserv_import_id := LAST_INSERT_ID();

INSERT INTO sat_catalog_imports (
    catalog_name,
    source_authority,
    source_release,
    source_format,
    source_file_name,
    source_file_sha256,
    source_sheet_name,
    row_count,
    imported_at_utc,
    imported_by,
    notes
)
VALUES (
    'unidad',
    'SAT',
    'REPLACE_RELEASE',
    'CSV',
    'unidad.csv',
    'REPLACE_SHA256',
    'unidad',
    (SELECT COUNT(*) FROM stg_sat_clave_unidad),
    UTC_TIMESTAMP(6),
    'migration',
    'Loaded from official SAT spreadsheet export'
);

SET @unidad_import_id := LAST_INSERT_ID();

-- Canonical product/service upsert.
INSERT INTO sat_clave_prodserv (
    clave,
    descripcion,
    descripcion_normalizada,
    keywords_normalized,
    es_activa,
    import_id,
    raw_row_json,
    created_at_utc,
    updated_at_utc
)
SELECT
    UPPER(TRIM(clave)) AS clave,
    TRIM(descripcion) AS descripcion,
    UPPER(TRIM(descripcion)) AS descripcion_normalizada,
    NULLIF(UPPER(TRIM(keywords)), '') AS keywords_normalized,
    CASE
        WHEN UPPER(TRIM(COALESCE(estado, 'ACTIVO'))) IN ('ACTIVO', 'VIGENTE', '1', 'TRUE') THEN 1
        ELSE 0
    END AS es_activa,
    @prodserv_import_id,
    JSON_OBJECT(
        'clave', clave,
        'descripcion', descripcion,
        'keywords', keywords,
        'estado', estado
    ),
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM stg_sat_clave_prodserv
WHERE NULLIF(TRIM(clave), '') IS NOT NULL
ON DUPLICATE KEY UPDATE
    descripcion = VALUES(descripcion),
    descripcion_normalizada = VALUES(descripcion_normalizada),
    keywords_normalized = VALUES(keywords_normalized),
    es_activa = VALUES(es_activa),
    import_id = VALUES(import_id),
    raw_row_json = VALUES(raw_row_json),
    updated_at_utc = VALUES(updated_at_utc);

-- Canonical unit upsert.
INSERT INTO sat_clave_unidad (
    clave,
    nombre,
    descripcion,
    simbolo,
    descripcion_normalizada,
    es_activa,
    import_id,
    raw_row_json,
    created_at_utc,
    updated_at_utc
)
SELECT
    UPPER(TRIM(clave)) AS clave,
    TRIM(nombre) AS nombre,
    NULLIF(TRIM(descripcion), '') AS descripcion,
    NULLIF(TRIM(simbolo), '') AS simbolo,
    UPPER(TRIM(COALESCE(nombre, descripcion, clave))) AS descripcion_normalizada,
    CASE
        WHEN UPPER(TRIM(COALESCE(estado, 'ACTIVO'))) IN ('ACTIVO', 'VIGENTE', '1', 'TRUE') THEN 1
        ELSE 0
    END AS es_activa,
    @unidad_import_id,
    JSON_OBJECT(
        'clave', clave,
        'nombre', nombre,
        'descripcion', descripcion,
        'simbolo', simbolo,
        'estado', estado
    ),
    UTC_TIMESTAMP(6),
    UTC_TIMESTAMP(6)
FROM stg_sat_clave_unidad
WHERE NULLIF(TRIM(clave), '') IS NOT NULL
ON DUPLICATE KEY UPDATE
    nombre = VALUES(nombre),
    descripcion = VALUES(descripcion),
    simbolo = VALUES(simbolo),
    descripcion_normalizada = VALUES(descripcion_normalizada),
    es_activa = VALUES(es_activa),
    import_id = VALUES(import_id),
    raw_row_json = VALUES(raw_row_json),
    updated_at_utc = VALUES(updated_at_utc);

-- Backfill seed from current repo equivalent:
-- product_fiscal_profile acts as today's mutable product-to-SAT assignment source.
-- Translate internal_code to product_id in installations that already have a canonical products table.
--
-- Example generic backfill:
-- INSERT INTO product_sat_assignment (
--     product_id,
--     sat_prodserv_clave,
--     sat_unidad_clave,
--     unit_text,
--     tax_object_code,
--     vat_rate,
--     source,
--     confidence,
--     review_status,
--     review_reason,
--     is_primary,
--     valid_from_utc,
--     valid_to_utc,
--     sat_catalog_import_id,
--     created_at_utc,
--     updated_at_utc
-- )
-- SELECT
--     p.id,
--     pfp.sat_product_service_code,
--     pfp.sat_unit_code,
--     pfp.default_unit_text,
--     pfp.tax_object_code,
--     pfp.vat_rate,
--     'product_fiscal_profile',
--     CASE
--         WHEN sp.clave IS NOT NULL AND su.clave IS NOT NULL THEN 0.9500
--         ELSE 0.2500
--     END AS confidence,
--     CASE
--         WHEN sp.clave IS NOT NULL AND su.clave IS NOT NULL THEN 'Approved'
--         ELSE 'NeedsReview'
--     END AS review_status,
--     CASE
--         WHEN sp.clave IS NULL THEN 'SAT prodserv code not found in canonical catalog'
--         WHEN su.clave IS NULL THEN 'SAT unit code not found in canonical catalog'
--         ELSE NULL
--     END AS review_reason,
--     1,
--     UTC_TIMESTAMP(6),
--     NULL,
--     NULL,
--     UTC_TIMESTAMP(6),
--     UTC_TIMESTAMP(6)
-- FROM products p
-- JOIN product_fiscal_profile pfp
--   ON p.internal_code = pfp.internal_code
-- LEFT JOIN sat_clave_prodserv sp
--   ON sp.clave = pfp.sat_product_service_code
-- LEFT JOIN sat_clave_unidad su
--   ON su.clave = pfp.sat_unit_code
-- WHERE NOT EXISTS (
--     SELECT 1
--     FROM product_sat_assignment psa
--     WHERE psa.product_id = p.id
--       AND psa.valid_to_utc IS NULL
-- );

-- Manual review queue:
-- SELECT *
-- FROM product_sat_assignment
-- WHERE review_status = 'NeedsReview'
-- ORDER BY confidence ASC, updated_at_utc DESC;
