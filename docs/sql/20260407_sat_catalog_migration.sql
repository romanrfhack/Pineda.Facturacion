-- Proposal only.
-- MySQL 8.0 DDL for canonical SAT catalog ingestion and versioned product assignment.
-- This file is additive. It does not replace current repo tables.
--
-- Naming requested by audit:
--   sat_catalog_imports
--   sat_clave_prodserv
--   sat_clave_unidad
--   product_sat_assignment
--   minimal changes in products
--   minimal changes in invoice_lines
--
-- Current repo note:
--   there is no canonical products table today
--   current invoice line equivalent is billing_document_item
--   current stamped fiscal line snapshot is fiscal_document_item

SET NAMES utf8mb4;

CREATE TABLE IF NOT EXISTS sat_catalog_imports (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    catalog_name VARCHAR(30) NOT NULL,
    source_authority VARCHAR(20) NOT NULL DEFAULT 'SAT',
    source_release VARCHAR(100) NOT NULL,
    source_format VARCHAR(20) NOT NULL DEFAULT 'XLSX',
    source_file_name VARCHAR(255) NOT NULL,
    source_file_sha256 CHAR(64) NOT NULL,
    source_sheet_name VARCHAR(100) NULL,
    row_count INT NOT NULL DEFAULT 0,
    imported_at_utc DATETIME(6) NOT NULL,
    imported_by VARCHAR(100) NULL,
    notes VARCHAR(500) NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_sat_catalog_imports_source (
        source_authority,
        catalog_name,
        source_release,
        source_file_sha256,
        source_sheet_name
    ),
    KEY ix_sat_catalog_imports_catalog_release (catalog_name, source_release)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS sat_clave_prodserv (
    clave CHAR(8) NOT NULL,
    descripcion VARCHAR(500) NOT NULL,
    descripcion_normalizada VARCHAR(500) NOT NULL,
    keywords_normalized VARCHAR(1000) NULL,
    es_activa TINYINT(1) NOT NULL DEFAULT 1,
    import_id BIGINT UNSIGNED NOT NULL,
    raw_row_json JSON NULL,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (clave),
    KEY ix_sat_clave_prodserv_descripcion (descripcion_normalizada),
    KEY ix_sat_clave_prodserv_import_id (import_id),
    CONSTRAINT fk_sat_clave_prodserv_import
        FOREIGN KEY (import_id) REFERENCES sat_catalog_imports(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS sat_clave_unidad (
    clave VARCHAR(20) NOT NULL,
    nombre VARCHAR(255) NOT NULL,
    descripcion VARCHAR(500) NULL,
    simbolo VARCHAR(50) NULL,
    descripcion_normalizada VARCHAR(500) NOT NULL,
    es_activa TINYINT(1) NOT NULL DEFAULT 1,
    import_id BIGINT UNSIGNED NOT NULL,
    raw_row_json JSON NULL,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (clave),
    KEY ix_sat_clave_unidad_descripcion (descripcion_normalizada),
    KEY ix_sat_clave_unidad_import_id (import_id),
    CONSTRAINT fk_sat_clave_unidad_import
        FOREIGN KEY (import_id) REFERENCES sat_catalog_imports(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS product_sat_assignment (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    product_id BIGINT UNSIGNED NOT NULL,
    sat_prodserv_clave CHAR(8) NOT NULL,
    sat_unidad_clave VARCHAR(20) NOT NULL,
    unit_text VARCHAR(100) NULL,
    tax_object_code VARCHAR(10) NOT NULL,
    tax_code VARCHAR(3) NOT NULL DEFAULT '002',
    tax_factor VARCHAR(10) NOT NULL DEFAULT 'Tasa',
    vat_rate DECIMAL(9,6) NOT NULL,
    source VARCHAR(30) NOT NULL,
    confidence DECIMAL(5,4) NOT NULL DEFAULT 0.0000,
    review_status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    review_reason VARCHAR(255) NULL,
    is_primary TINYINT(1) NOT NULL DEFAULT 1,
    valid_from_utc DATETIME(6) NOT NULL,
    valid_to_utc DATETIME(6) NULL,
    sat_catalog_import_id BIGINT UNSIGNED NULL,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_product_sat_assignment_version (product_id, valid_from_utc),
    KEY ix_product_sat_assignment_current (product_id, valid_to_utc, is_primary),
    KEY ix_product_sat_assignment_review (review_status, confidence),
    KEY ix_product_sat_assignment_prodserv (sat_prodserv_clave),
    KEY ix_product_sat_assignment_unidad (sat_unidad_clave),
    CONSTRAINT fk_product_sat_assignment_prodserv
        FOREIGN KEY (sat_prodserv_clave) REFERENCES sat_clave_prodserv(clave),
    CONSTRAINT fk_product_sat_assignment_unidad
        FOREIGN KEY (sat_unidad_clave) REFERENCES sat_clave_unidad(clave),
    CONSTRAINT fk_product_sat_assignment_import
        FOREIGN KEY (sat_catalog_import_id) REFERENCES sat_catalog_imports(id)
    -- Add the FK to products(id) only in installations that already have a canonical products table.
    -- Example:
    -- ,CONSTRAINT fk_product_sat_assignment_product
    --     FOREIGN KEY (product_id) REFERENCES products(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Optional section for installations that have a canonical products table.
-- Keep product master clean: store only current assignment pointer and status.
--
-- ALTER TABLE products
--     ADD COLUMN current_product_sat_assignment_id BIGINT UNSIGNED NULL,
--     ADD COLUMN sat_assignment_status VARCHAR(20) NOT NULL DEFAULT 'Unassigned',
--     ADD COLUMN sat_assignment_updated_at_utc DATETIME(6) NULL,
--     ADD CONSTRAINT fk_products_current_product_sat_assignment
--         FOREIGN KEY (current_product_sat_assignment_id) REFERENCES product_sat_assignment(id);

-- Optional section for installations that have invoice_lines.
-- The line snapshot remains authoritative even if current assignment changes later.
--
-- ALTER TABLE invoice_lines
--     ADD COLUMN product_sat_assignment_id BIGINT UNSIGNED NULL,
--     ADD COLUMN snapshot_sat_prodserv_clave CHAR(8) NULL,
--     ADD COLUMN snapshot_sat_unidad_clave VARCHAR(20) NULL,
--     ADD COLUMN snapshot_unit_text VARCHAR(100) NULL,
--     ADD COLUMN snapshot_tax_object_code VARCHAR(10) NULL,
--     ADD COLUMN snapshot_tax_code VARCHAR(3) NOT NULL DEFAULT '002',
--     ADD COLUMN snapshot_tax_factor VARCHAR(10) NOT NULL DEFAULT 'Tasa',
--     ADD COLUMN snapshot_tax_rate DECIMAL(9,6) NULL,
--     ADD COLUMN snapshot_tax_amount DECIMAL(18,6) NULL,
--     ADD COLUMN snapshot_source VARCHAR(30) NOT NULL DEFAULT 'assignment',
--     ADD COLUMN snapshot_captured_at_utc DATETIME(6) NULL,
--     ADD KEY ix_invoice_lines_assignment (product_sat_assignment_id),
--     ADD KEY ix_invoice_lines_snapshot_prodserv (snapshot_sat_prodserv_clave),
--     ADD KEY ix_invoice_lines_snapshot_unidad (snapshot_sat_unidad_clave),
--     ADD CONSTRAINT fk_invoice_lines_assignment
--         FOREIGN KEY (product_sat_assignment_id) REFERENCES product_sat_assignment(id);

-- Current repo adaptation note:
-- In this repository, the closest equivalent changes would be:
--   billing_document_item:
--     add product_sat_assignment_id, snapshot_source, snapshot_confidence, snapshot_captured_at_utc
--   fiscal_document_item:
--     add source_product_sat_assignment_id for lineage only, never for live resolution
-- This proposal keeps those adaptations out of the generic DDL above.
