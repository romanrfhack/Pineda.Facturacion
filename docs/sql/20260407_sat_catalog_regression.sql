-- Proposal only.
-- Smoke checks to run after deploying the additive SAT catalog model and backfill.
-- All queries should be reviewed with expected row counts before enabling new reads/writes.

SET NAMES utf8mb4;

-- 1. Canonical SAT catalogs should be non-empty.
SELECT 'sat_clave_prodserv_count' AS check_name, COUNT(*) AS row_count
FROM sat_clave_prodserv;

SELECT 'sat_clave_unidad_count' AS check_name, COUNT(*) AS row_count
FROM sat_clave_unidad;

-- 2. Every current product assignment should resolve to canonical SAT tables.
SELECT 'broken_product_assignments' AS check_name, COUNT(*) AS row_count
FROM product_sat_assignment psa
LEFT JOIN sat_clave_prodserv sp
    ON sp.clave = psa.sat_prodserv_clave
LEFT JOIN sat_clave_unidad su
    ON su.clave = psa.sat_unidad_clave
WHERE psa.valid_to_utc IS NULL
  AND (sp.clave IS NULL OR su.clave IS NULL);

-- 3. Review queue should show all ambiguous assignments explicitly.
SELECT
    id,
    product_id,
    sat_prodserv_clave,
    sat_unidad_clave,
    source,
    confidence,
    review_status,
    review_reason
FROM product_sat_assignment
WHERE review_status = 'NeedsReview'
ORDER BY confidence ASC, updated_at_utc DESC;

-- 4. Current repo check: billing concepts should already keep line snapshot fields.
SELECT 'billing_document_item_missing_snapshot_fields' AS check_name, COUNT(*) AS row_count
FROM billing_document_item
WHERE description IS NULL
   OR tax_rate IS NULL
   OR tax_amount IS NULL
   OR tax_object_code IS NULL;

-- 5. Current repo check: fiscal concepts used for stamping must stay complete.
SELECT 'fiscal_document_item_missing_stampable_snapshot' AS check_name, COUNT(*) AS row_count
FROM fiscal_document_item
WHERE description IS NULL
   OR sat_product_service_code IS NULL
   OR sat_unit_code IS NULL
   OR tax_object_code IS NULL
   OR vat_rate IS NULL;

-- 6. Stamped documents must still have persisted XML evidence.
SELECT 'stamped_documents_without_xml' AS check_name, COUNT(*) AS row_count
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE fd.status = 3
  AND (fs.id IS NULL OR fs.xml_content IS NULL OR fs.uuid IS NULL);

-- 7. Current repo check: prepared or re-built fiscal documents should not have concept count drift.
SELECT
    fd.id AS fiscal_document_id,
    COUNT(DISTINCT fdi.id) AS fiscal_item_count,
    COUNT(DISTINCT bdi.id) AS billing_item_count
FROM fiscal_document fd
JOIN billing_document bd
    ON bd.id = fd.billing_document_id
LEFT JOIN fiscal_document_item fdi
    ON fdi.fiscal_document_id = fd.id
LEFT JOIN billing_document_item bdi
    ON bdi.billing_document_id = bd.id
GROUP BY fd.id
HAVING COUNT(DISTINCT fdi.id) <> COUNT(DISTINCT bdi.id);

-- 8. Optional future check for generic invoice_lines installations.
-- Replace with your real invoice_lines table once introduced.
--
-- SELECT 'invoice_lines_missing_fiscal_snapshot' AS check_name, COUNT(*) AS row_count
-- FROM invoice_lines
-- WHERE snapshot_sat_prodserv_clave IS NULL
--    OR snapshot_sat_unidad_clave IS NULL
--    OR snapshot_tax_object_code IS NULL
--    OR snapshot_tax_rate IS NULL;

-- 9. Operational sanity query: sample stamped history vs current assignment drift.
-- Differences here are acceptable and often desirable; this query exists to verify historical isolation.
-- Adapt product join rules to your installation.
--
-- SELECT
--     fd.id AS fiscal_document_id,
--     fdi.line_number,
--     fdi.sat_product_service_code AS stamped_prodserv,
--     psa.sat_prodserv_clave AS current_assignment_prodserv,
--     fdi.sat_unit_code AS stamped_unidad,
--     psa.sat_unidad_clave AS current_assignment_unidad
-- FROM fiscal_document fd
-- JOIN fiscal_document_item fdi
--   ON fdi.fiscal_document_id = fd.id
-- JOIN billing_document_item bdi
--   ON bdi.id = fdi.billing_document_item_id
-- JOIN products p
--   ON p.internal_code = bdi.product_internal_code
-- JOIN product_sat_assignment psa
--   ON psa.product_id = p.id
--  AND psa.valid_to_utc IS NULL
-- WHERE fd.status IN (3, 5)
--   AND (
--       fdi.sat_product_service_code <> psa.sat_prodserv_clave
--       OR fdi.sat_unit_code <> psa.sat_unidad_clave
--   )
-- ORDER BY fd.id, fdi.line_number;
