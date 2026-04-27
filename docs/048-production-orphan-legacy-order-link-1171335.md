# Production Fix Runbook: Orphan Legacy Order Link 1171335

## Scope

Case:

- Legacy order: `1171335`
- Expected total: `911.00`
- Expected customer: `RAUL RENDON LIMA`
- Current wrong billing document link: `billing_document.id = 761`
- Known valid order/item in document `761`: legacy order `1171000`, code/reference `A280496`
- Expected internal sales order id from the incident: `1235`

This runbook only releases the operational import-to-billing-document link for legacy order `1171335`.
It must not modify `billing_document`, `billing_document_item`, `fiscal_document`, `fiscal_document_item`,
`fiscal_stamp`, XML, UUID, totals, fiscal status, or the valid order `1171000`.

## Schema Findings

- Legacy source id is persisted in `legacy_import_record.source_document_id`.
- Legacy import state is `legacy_import_record.import_status`: `0 = Pending`, `1 = Imported`, `2 = Failed`.
- The operational relation from imported legacy order to a billing document is `legacy_import_record.billing_document_id`.
- Imported commercial snapshot is `sales_order.legacy_import_record_id -> legacy_import_record.id`.
- Imported item snapshot is `sales_order_item.sales_order_id -> sales_order.id`.
- Internal billing document is `billing_document.id`; its primary order is `billing_document.sales_order_id`.
- Billing document concepts are `billing_document_item.billing_document_id`, with source links:
  `sales_order_id`, `sales_order_item_id`, and `source_legacy_order_id`.
- Fiscal document is `fiscal_document.billing_document_id -> billing_document.id`.
- Fiscal concepts are `fiscal_document_item.fiscal_document_id -> fiscal_document.id`, optionally linked to
  `billing_document_item.id` through `fiscal_document_item.billing_document_item_id`.
- Fiscal stamp evidence is `fiscal_stamp.fiscal_document_id -> fiscal_document.id`; stamped evidence is
  `fiscal_stamp.status = 1`, non-empty `uuid`, and `stamped_at_utc`.
- Stamped document statuses are `billing_document.status = 3` and `fiscal_document.status = 3`.

Important distinction: setting `legacy_import_record.billing_document_id = NULL` makes the already imported
snapshot available to bill again. It does not make `import_status` become `Pending`, and should not. The
snapshot in `sales_order` remains the source for recreating or reusing the billing flow.

## Likely Code Cause

`UpdateBillingDocumentOrderAssociationService` mutates `targetImportRecord.BillingDocumentId` before all
composition/fiscal validation has succeeded. If a later validation returns a failure before the service's own
`SaveChangesAsync`, the endpoint still writes an audit record. `AuditService.RecordAsync` uses the same scoped
`IUnitOfWork`, so the audit `SaveChangesAsync` can flush the already-tracked legacy import link mutation.

## Diagnostic SQL

Run these first. They are read-only.

```sql
SET @legacy_order_id := '1171335';
SET @valid_legacy_order_id := '1171000';
SET @known_valid_code := 'A280496';
SET @billing_document_id := 761;
SET @expected_sales_order_id := 1235;
SET @source_system := 'legacy';
SET @source_table := 'pedidos';

-- 1. Legacy import record and imported sales-order snapshot.
SELECT
    lir.id AS legacy_import_record_id,
    lir.source_system,
    lir.source_table,
    lir.source_document_id,
    lir.source_document_type,
    lir.import_status,
    CASE lir.import_status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Imported'
        WHEN 2 THEN 'Failed'
        ELSE CONCAT('Unknown:', lir.import_status)
    END AS import_status_name,
    lir.imported_at_utc,
    lir.last_seen_at_utc,
    lir.billing_document_id,
    lir.error_message,
    so.id AS sales_order_id,
    so.legacy_order_number,
    so.customer_name,
    so.total,
    so.status AS sales_order_status
FROM legacy_import_record lir
LEFT JOIN sales_order so
    ON so.legacy_import_record_id = lir.id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

-- 2. Sanity check against incident data.
SELECT
    CASE WHEN so.id = @expected_sales_order_id THEN 'OK' ELSE 'REVIEW' END AS sales_order_id_check,
    CASE WHEN ROUND(so.total, 2) = 911.00 THEN 'OK' ELSE 'REVIEW' END AS total_check,
    CASE WHEN UPPER(so.customer_name) = 'RAUL RENDON LIMA' THEN 'OK' ELSE 'REVIEW' END AS customer_check,
    so.id,
    so.customer_name,
    so.total,
    so.legacy_order_number
FROM legacy_import_record lir
JOIN sales_order so
    ON so.legacy_import_record_id = lir.id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

-- 3. Current wrong relation to billing document 761.
SELECT
    lir.id AS legacy_import_record_id,
    lir.source_document_id,
    lir.billing_document_id,
    bd.id AS billing_document_id,
    bd.status AS billing_document_status,
    CASE bd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyToStamp'
        WHEN 2 THEN 'Stamping'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampFailed'
        WHEN 5 THEN 'Cancelled'
        ELSE CONCAT('Unknown:', bd.status)
    END AS billing_document_status_name,
    bd.total AS billing_document_total,
    bd.updated_at_utc
FROM legacy_import_record lir
LEFT JOIN billing_document bd
    ON bd.id = lir.billing_document_id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

-- 4. Fiscal/timbre status for billing document 761.
SELECT
    bd.id AS billing_document_id,
    bd.status AS billing_document_status,
    bd.subtotal AS billing_subtotal,
    bd.discount_total AS billing_discount_total,
    bd.tax_total AS billing_tax_total,
    bd.total AS billing_total,
    fd.id AS fiscal_document_id,
    fd.status AS fiscal_document_status,
    CASE fd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyForStamping'
        WHEN 2 THEN 'StampingRequested'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampingRejected'
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        WHEN 7 THEN 'CancellationRejected'
        WHEN 8 THEN 'DiscardedUnstamped'
        ELSE CONCAT('Unknown:', fd.status)
    END AS fiscal_document_status_name,
    fd.series,
    fd.folio,
    fd.total AS fiscal_total,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    CASE fs.status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Succeeded'
        WHEN 2 THEN 'Rejected'
        WHEN 3 THEN 'Unavailable'
        WHEN 4 THEN 'ValidationFailed'
        ELSE CONCAT('Unknown:', fs.status)
    END AS fiscal_stamp_status_name,
    fs.uuid,
    fs.stamped_at_utc,
    fs.xml_hash,
    CHAR_LENGTH(fs.xml_content) AS xml_content_length
FROM billing_document bd
LEFT JOIN fiscal_document fd
    ON fd.billing_document_id = bd.id
   AND fd.status <> 8
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE bd.id = @billing_document_id;

-- 5. Actual billing concepts in document 761.
SELECT
    bdi.id AS billing_document_item_id,
    bdi.line_number,
    bdi.sales_order_id,
    bdi.sales_order_item_id,
    lir.source_document_id AS legacy_order_id,
    so.legacy_order_number,
    bdi.source_legacy_order_id,
    bdi.sku,
    bdi.product_internal_code,
    bdi.description,
    bdi.quantity,
    bdi.unit_price,
    bdi.discount_amount,
    bdi.line_total,
    bdi.tax_amount,
    bdi.line_total + bdi.tax_amount AS line_gross_total
FROM billing_document_item bdi
JOIN sales_order so
    ON so.id = bdi.sales_order_id
JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
WHERE bdi.billing_document_id = @billing_document_id
ORDER BY bdi.line_number, bdi.id;

-- 6. Actual fiscal concepts in document 761, joined back to billing concepts when possible.
SELECT
    fd.id AS fiscal_document_id,
    fdi.id AS fiscal_document_item_id,
    fdi.line_number,
    fdi.billing_document_item_id,
    lir.source_document_id AS linked_legacy_order_id,
    bdi.source_legacy_order_id,
    fdi.internal_code,
    fdi.description,
    fdi.quantity,
    fdi.subtotal,
    fdi.tax_total,
    fdi.total,
    fdi.sat_product_service_code,
    fdi.sat_unit_code
FROM fiscal_document fd
JOIN fiscal_document_item fdi
    ON fdi.fiscal_document_id = fd.id
LEFT JOIN billing_document_item bdi
    ON bdi.id = fdi.billing_document_item_id
LEFT JOIN sales_order so
    ON so.id = bdi.sales_order_id
LEFT JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
WHERE fd.billing_document_id = @billing_document_id
  AND fd.status <> 8
ORDER BY fdi.line_number, fdi.id;

-- 7. Absence of billing/fiscal concepts from legacy order 1171335 in document 761.
SELECT
    SUM(CASE WHEN bdi.id IS NOT NULL THEN 1 ELSE 0 END) AS billing_item_count_for_1171335,
    SUM(CASE WHEN fdi.id IS NOT NULL THEN 1 ELSE 0 END) AS linked_fiscal_item_count_for_1171335,
    (
        SELECT COUNT(*)
        FROM billing_document_item bdi_text
        WHERE bdi_text.billing_document_id = @billing_document_id
          AND bdi_text.source_legacy_order_id LIKE CONCAT('%', @legacy_order_id, '%')
    ) AS billing_item_text_trace_count_for_1171335
FROM legacy_import_record lir
JOIN sales_order so
    ON so.legacy_import_record_id = lir.id
LEFT JOIN billing_document_item bdi
    ON bdi.sales_order_id = so.id
   AND bdi.billing_document_id = @billing_document_id
LEFT JOIN fiscal_document fd
    ON fd.billing_document_id = @billing_document_id
   AND fd.status <> 8
LEFT JOIN fiscal_document_item fdi
    ON fdi.fiscal_document_id = fd.id
   AND fdi.billing_document_item_id = bdi.id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

-- 8. Unlinked fiscal concepts require manual review because they cannot be traced to legacy order by schema.
SELECT
    fd.id AS fiscal_document_id,
    fdi.id AS fiscal_document_item_id,
    fdi.line_number,
    fdi.internal_code,
    fdi.description,
    fdi.total
FROM fiscal_document fd
JOIN fiscal_document_item fdi
    ON fdi.fiscal_document_id = fd.id
WHERE fd.billing_document_id = @billing_document_id
  AND fd.status <> 8
  AND fdi.billing_document_item_id IS NULL
ORDER BY fdi.line_number, fdi.id;

-- 9. Valid legacy order 1171000 / code A280496 remains present in document 761.
SELECT
    bdi.id AS billing_document_item_id,
    bdi.line_number,
    lir.source_document_id AS legacy_order_id,
    so.legacy_order_number,
    bdi.source_legacy_order_id,
    bdi.sku,
    bdi.product_internal_code,
    soi.legacy_article_id,
    bdi.description,
    bdi.quantity,
    bdi.line_total,
    bdi.tax_amount
FROM billing_document_item bdi
JOIN sales_order so
    ON so.id = bdi.sales_order_id
JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
JOIN sales_order_item soi
    ON soi.id = bdi.sales_order_item_id
WHERE bdi.billing_document_id = @billing_document_id
  AND lir.source_document_id = @valid_legacy_order_id
  AND (
      bdi.product_internal_code = @known_valid_code
      OR bdi.sku = @known_valid_code
      OR soi.legacy_article_id = @known_valid_code
      OR soi.sku = @known_valid_code
      OR bdi.source_legacy_order_id LIKE CONCAT('%', @known_valid_code, '%')
  );

-- 10. Final decision matrix. Only proceed if decision = SAFE_TO_RELEASE_LINK_ONLY.
SELECT
    CASE
        WHEN lir.billing_document_id = @billing_document_id
         AND EXISTS (
             SELECT 1
             FROM billing_document bd
             JOIN fiscal_document fd
                 ON fd.billing_document_id = bd.id
                AND fd.status = 3
             JOIN fiscal_stamp fs
                 ON fs.fiscal_document_id = fd.id
                AND fs.status = 1
                AND fs.uuid IS NOT NULL
                AND fs.uuid <> ''
             WHERE bd.id = @billing_document_id
               AND bd.status = 3
         )
         AND NOT EXISTS (
             SELECT 1
             FROM sales_order so2
             JOIN billing_document_item bdi2
                 ON bdi2.sales_order_id = so2.id
                AND bdi2.billing_document_id = @billing_document_id
             WHERE so2.legacy_import_record_id = lir.id
         )
         AND NOT EXISTS (
             SELECT 1
             FROM billing_document_item bdi_text
             WHERE bdi_text.billing_document_id = @billing_document_id
               AND bdi_text.source_legacy_order_id LIKE CONCAT('%', @legacy_order_id, '%')
         )
         AND NOT EXISTS (
             SELECT 1
             FROM sales_order so3
             JOIN billing_document_item bdi3
                 ON bdi3.sales_order_id = so3.id
                AND bdi3.billing_document_id = @billing_document_id
             JOIN fiscal_document fd3
                 ON fd3.billing_document_id = @billing_document_id
                AND fd3.status <> 8
             JOIN fiscal_document_item fdi3
                 ON fdi3.fiscal_document_id = fd3.id
                AND fdi3.billing_document_item_id = bdi3.id
             WHERE so3.legacy_import_record_id = lir.id
         )
         AND NOT EXISTS (
             SELECT 1
             FROM fiscal_document fd4
             JOIN fiscal_document_item fdi4
                 ON fdi4.fiscal_document_id = fd4.id
             WHERE fd4.billing_document_id = @billing_document_id
               AND fd4.status <> 8
               AND fdi4.billing_document_item_id IS NULL
         )
        THEN 'SAFE_TO_RELEASE_LINK_ONLY'
        ELSE 'DO_NOT_UPDATE'
    END AS decision,
    lir.id AS legacy_import_record_id,
    lir.billing_document_id
FROM legacy_import_record lir
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;
```

## Correction SQL

This script uses MySQL syntax. It creates a persistent backup table outside the transaction because MySQL DDL
causes implicit commits. The data change itself is guarded and transactional. The final statement is `ROLLBACK`
by default; replace only the last `ROLLBACK` with `COMMIT` after verifying `update_rows_expected_1 = 1`,
`audit_rows_expected_1 = 1`, and all post-checks.

```sql
SET @legacy_order_id := '1171335';
SET @valid_legacy_order_id := '1171000';
SET @known_valid_code := 'A280496';
SET @billing_document_id := 761;
SET @source_system := 'legacy';
SET @source_table := 'pedidos';
SET @fix_reason := 'prod-fix-release-orphan-link-1171335-from-billing-document-761';

CREATE TABLE IF NOT EXISTS ops_legacy_import_record_backup (
    backup_id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    backup_reason VARCHAR(200) NOT NULL,
    backup_created_at_utc DATETIME(6) NOT NULL,
    legacy_import_record_id BIGINT NOT NULL,
    source_system VARCHAR(50) NOT NULL,
    source_table VARCHAR(100) NOT NULL,
    source_document_id VARCHAR(100) NOT NULL,
    source_document_type VARCHAR(50) NOT NULL,
    source_hash CHAR(64) NOT NULL,
    import_status INT NOT NULL,
    imported_at_utc DATETIME(6) NULL,
    last_seen_at_utc DATETIME(6) NOT NULL,
    billing_document_id BIGINT NULL,
    error_message VARCHAR(1000) NULL,
    UNIQUE KEY ux_ops_lir_backup_reason_row (backup_reason, legacy_import_record_id)
);

INSERT IGNORE INTO ops_legacy_import_record_backup (
    backup_reason,
    backup_created_at_utc,
    legacy_import_record_id,
    source_system,
    source_table,
    source_document_id,
    source_document_type,
    source_hash,
    import_status,
    imported_at_utc,
    last_seen_at_utc,
    billing_document_id,
    error_message
)
SELECT
    @fix_reason,
    UTC_TIMESTAMP(6),
    lir.id,
    lir.source_system,
    lir.source_table,
    lir.source_document_id,
    lir.source_document_type,
    lir.source_hash,
    lir.import_status,
    lir.imported_at_utc,
    lir.last_seen_at_utc,
    lir.billing_document_id,
    lir.error_message
FROM legacy_import_record lir
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id
  AND lir.billing_document_id = @billing_document_id;

SET @backup_rows_inserted := ROW_COUNT();

SELECT
    @backup_rows_inserted AS backup_rows_inserted_zero_or_one,
    COUNT(*) AS backup_rows_available_for_fix
FROM ops_legacy_import_record_backup
WHERE backup_reason = @fix_reason
  AND source_document_id = @legacy_order_id
  AND billing_document_id = @billing_document_id;

START TRANSACTION;

-- Lock the target import row and the stamped document evidence while validating.
SELECT
    lir.id,
    lir.source_document_id,
    lir.import_status,
    lir.billing_document_id
FROM legacy_import_record lir
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id
FOR UPDATE;

SELECT
    bd.id AS billing_document_id,
    bd.status AS billing_document_status,
    fd.id AS fiscal_document_id,
    fd.status AS fiscal_document_status,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    fs.uuid,
    fs.stamped_at_utc,
    fs.xml_hash
FROM billing_document bd
JOIN fiscal_document fd
    ON fd.billing_document_id = bd.id
   AND fd.status <> 8
JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE bd.id = @billing_document_id
FOR UPDATE;

UPDATE legacy_import_record lir
SET lir.billing_document_id = NULL
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id
  AND lir.import_status = 1
  AND lir.billing_document_id = @billing_document_id
  AND EXISTS (
      SELECT 1
      FROM ops_legacy_import_record_backup b
      WHERE b.backup_reason = @fix_reason
        AND b.legacy_import_record_id = lir.id
        AND b.source_document_id = @legacy_order_id
        AND b.billing_document_id = @billing_document_id
  )
  AND EXISTS (
      SELECT 1
      FROM billing_document bd
      JOIN fiscal_document fd
          ON fd.billing_document_id = bd.id
         AND fd.status = 3
      JOIN fiscal_stamp fs
          ON fs.fiscal_document_id = fd.id
         AND fs.status = 1
         AND fs.uuid IS NOT NULL
         AND fs.uuid <> ''
      WHERE bd.id = @billing_document_id
        AND bd.status = 3
  )
  AND NOT EXISTS (
      SELECT 1
      FROM sales_order so
      JOIN billing_document_item bdi
          ON bdi.sales_order_id = so.id
         AND bdi.billing_document_id = @billing_document_id
      WHERE so.legacy_import_record_id = lir.id
  )
  AND NOT EXISTS (
      SELECT 1
      FROM billing_document_item bdi_text
      WHERE bdi_text.billing_document_id = @billing_document_id
        AND bdi_text.source_legacy_order_id LIKE CONCAT('%', @legacy_order_id, '%')
  )
  AND NOT EXISTS (
      SELECT 1
      FROM sales_order so
      JOIN billing_document_item bdi
          ON bdi.sales_order_id = so.id
         AND bdi.billing_document_id = @billing_document_id
      JOIN fiscal_document fd
          ON fd.billing_document_id = @billing_document_id
         AND fd.status <> 8
      JOIN fiscal_document_item fdi
          ON fdi.fiscal_document_id = fd.id
         AND fdi.billing_document_item_id = bdi.id
      WHERE so.legacy_import_record_id = lir.id
  )
  AND NOT EXISTS (
      SELECT 1
      FROM fiscal_document fd_unlinked
      JOIN fiscal_document_item fdi_unlinked
          ON fdi_unlinked.fiscal_document_id = fd_unlinked.id
      WHERE fd_unlinked.billing_document_id = @billing_document_id
        AND fd_unlinked.status <> 8
        AND fdi_unlinked.billing_document_item_id IS NULL
  )
  AND EXISTS (
      SELECT 1
      FROM billing_document_item bdi
      JOIN sales_order so
          ON so.id = bdi.sales_order_id
      JOIN legacy_import_record valid_lir
          ON valid_lir.id = so.legacy_import_record_id
      JOIN sales_order_item soi
          ON soi.id = bdi.sales_order_item_id
      WHERE bdi.billing_document_id = @billing_document_id
        AND valid_lir.source_document_id = @valid_legacy_order_id
        AND (
            bdi.product_internal_code = @known_valid_code
            OR bdi.sku = @known_valid_code
            OR soi.legacy_article_id = @known_valid_code
            OR soi.sku = @known_valid_code
            OR bdi.source_legacy_order_id LIKE CONCAT('%', @known_valid_code, '%')
        )
  );

SET @updated_rows := ROW_COUNT();

SELECT @updated_rows AS update_rows_expected_1;

INSERT INTO audit_event (
    occurred_at_utc,
    actor_user_id,
    actor_username,
    action_type,
    entity_type,
    entity_id,
    outcome,
    correlation_id,
    request_summary_json,
    response_summary_json,
    error_message,
    ip_address,
    user_agent,
    created_at_utc
)
SELECT
    UTC_TIMESTAMP(6),
    NULL,
    'manual-sql',
    'LegacyImportRecord.ReleaseOrphanBillingDocumentLink',
    'LegacyImportRecord',
    CAST(lir.id AS CHAR),
    'Applied',
    @fix_reason,
    JSON_OBJECT(
        'legacyOrderId', @legacy_order_id,
        'billingDocumentId', @billing_document_id,
        'reason', 'Release orphan import link only; stamped document concepts/evidence untouched'
    ),
    JSON_OBJECT(
        'legacyImportRecordId', lir.id,
        'oldBillingDocumentId', @billing_document_id,
        'newBillingDocumentId', NULL,
        'updatedRows', @updated_rows
    ),
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6)
FROM legacy_import_record lir
WHERE @updated_rows = 1
  AND lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id
  AND lir.billing_document_id IS NULL;

SET @audit_rows := ROW_COUNT();

SELECT @audit_rows AS audit_rows_expected_1;

-- In-transaction verification before deciding whether to COMMIT.
SELECT
    lir.id AS legacy_import_record_id,
    lir.source_document_id,
    lir.import_status,
    lir.billing_document_id,
    CASE
        WHEN lir.import_status = 1 AND lir.billing_document_id IS NULL THEN 'AVAILABLE_FOR_BILLING'
        ELSE 'REVIEW'
    END AS availability_after_update
FROM legacy_import_record lir
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

SELECT
    bd.id AS billing_document_id,
    bd.status AS billing_document_status,
    bd.subtotal,
    bd.discount_total,
    bd.tax_total,
    bd.total,
    COUNT(DISTINCT bdi.id) AS billing_item_count,
    SUM(bdi.line_total) AS billing_items_subtotal_sum,
    SUM(bdi.tax_amount) AS billing_items_tax_sum
FROM billing_document bd
LEFT JOIN billing_document_item bdi
    ON bdi.billing_document_id = bd.id
WHERE bd.id = @billing_document_id
GROUP BY bd.id, bd.status, bd.subtotal, bd.discount_total, bd.tax_total, bd.total;

SELECT
    fd.id AS fiscal_document_id,
    fd.status AS fiscal_document_status,
    fd.total AS fiscal_total,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    fs.uuid,
    fs.stamped_at_utc,
    fs.xml_hash,
    CHAR_LENGTH(fs.xml_content) AS xml_content_length,
    COUNT(DISTINCT fdi.id) AS fiscal_item_count
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN fiscal_document_item fdi
    ON fdi.fiscal_document_id = fd.id
WHERE fd.billing_document_id = @billing_document_id
  AND fd.status <> 8
GROUP BY fd.id, fd.status, fd.total, fs.id, fs.status, fs.uuid, fs.stamped_at_utc, fs.xml_hash, fs.xml_content;

SELECT
    COUNT(*) AS billing_item_count_for_1171335_after_update
FROM sales_order so
JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
JOIN billing_document_item bdi
    ON bdi.sales_order_id = so.id
   AND bdi.billing_document_id = @billing_document_id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

SELECT
    COUNT(*) AS billing_item_text_trace_count_for_1171335_after_update
FROM billing_document_item bdi_text
WHERE bdi_text.billing_document_id = @billing_document_id
  AND bdi_text.source_legacy_order_id LIKE CONCAT('%', @legacy_order_id, '%');

SELECT
    COUNT(*) AS valid_1171000_a280496_item_count_after_update
FROM billing_document_item bdi
JOIN sales_order so
    ON so.id = bdi.sales_order_id
JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
JOIN sales_order_item soi
    ON soi.id = bdi.sales_order_item_id
WHERE bdi.billing_document_id = @billing_document_id
  AND lir.source_document_id = @valid_legacy_order_id
  AND (
      bdi.product_internal_code = @known_valid_code
      OR bdi.sku = @known_valid_code
      OR soi.legacy_article_id = @known_valid_code
      OR soi.sku = @known_valid_code
      OR bdi.source_legacy_order_id LIKE CONCAT('%', @known_valid_code, '%')
  );

-- Default safety behavior. Replace this final ROLLBACK with COMMIT only after reviewing all checks.
ROLLBACK;
-- COMMIT;
```

## Post-Commit Verification SQL

Run these after replacing the final statement with `COMMIT` and executing the correction.

```sql
SET @legacy_order_id := '1171335';
SET @valid_legacy_order_id := '1171000';
SET @known_valid_code := 'A280496';
SET @billing_document_id := 761;
SET @source_system := 'legacy';
SET @source_table := 'pedidos';

-- 1. Order 1171335 remains imported as a snapshot but is available for a new billing document.
SELECT
    lir.id AS legacy_import_record_id,
    lir.source_document_id,
    lir.import_status,
    lir.billing_document_id,
    so.id AS sales_order_id,
    so.customer_name,
    so.total,
    CASE
        WHEN lir.import_status = 1 AND lir.billing_document_id IS NULL THEN 'AVAILABLE_FOR_BILLING'
        ELSE 'REVIEW'
    END AS availability
FROM legacy_import_record lir
LEFT JOIN sales_order so
    ON so.legacy_import_record_id = lir.id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

-- 2. Document 761 fiscal/timbre evidence remains present.
SELECT
    bd.id AS billing_document_id,
    bd.status AS billing_document_status,
    bd.subtotal,
    bd.discount_total,
    bd.tax_total,
    bd.total,
    fd.id AS fiscal_document_id,
    fd.status AS fiscal_document_status,
    fd.total AS fiscal_total,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    fs.uuid,
    fs.stamped_at_utc,
    fs.xml_hash,
    CHAR_LENGTH(fs.xml_content) AS xml_content_length
FROM billing_document bd
LEFT JOIN fiscal_document fd
    ON fd.billing_document_id = bd.id
   AND fd.status <> 8
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE bd.id = @billing_document_id;

-- 3. No billing concepts from 1171335 exist in document 761.
SELECT COUNT(*) AS billing_item_count_for_1171335
FROM sales_order so
JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
JOIN billing_document_item bdi
    ON bdi.sales_order_id = so.id
   AND bdi.billing_document_id = @billing_document_id
WHERE lir.source_system = @source_system
  AND lir.source_table = @source_table
  AND lir.source_document_id = @legacy_order_id;

SELECT COUNT(*) AS billing_item_text_trace_count_for_1171335
FROM billing_document_item bdi_text
WHERE bdi_text.billing_document_id = @billing_document_id
  AND bdi_text.source_legacy_order_id LIKE CONCAT('%', @legacy_order_id, '%');

-- 4. Valid order 1171000 / A280496 remains linked.
SELECT
    bdi.id AS billing_document_item_id,
    bdi.line_number,
    lir.source_document_id AS legacy_order_id,
    bdi.source_legacy_order_id,
    bdi.sku,
    bdi.product_internal_code,
    soi.legacy_article_id,
    bdi.description,
    bdi.quantity,
    bdi.line_total,
    bdi.tax_amount
FROM billing_document_item bdi
JOIN sales_order so
    ON so.id = bdi.sales_order_id
JOIN legacy_import_record lir
    ON lir.id = so.legacy_import_record_id
JOIN sales_order_item soi
    ON soi.id = bdi.sales_order_item_id
WHERE bdi.billing_document_id = @billing_document_id
  AND lir.source_document_id = @valid_legacy_order_id
  AND (
      bdi.product_internal_code = @known_valid_code
      OR bdi.sku = @known_valid_code
      OR soi.legacy_article_id = @known_valid_code
      OR soi.sku = @known_valid_code
      OR bdi.source_legacy_order_id LIKE CONCAT('%', @known_valid_code, '%')
  );

-- 5. Audit entry exists if the correction was committed.
SELECT
    id,
    occurred_at_utc,
    actor_username,
    action_type,
    entity_type,
    entity_id,
    outcome,
    correlation_id,
    request_summary_json,
    response_summary_json
FROM audit_event
WHERE correlation_id = 'prod-fix-release-orphan-link-1171335-from-billing-document-761'
ORDER BY id DESC;
```

## When Not To Use Direct SQL

Do not run the correction if any diagnostic shows billing or fiscal concepts from `1171335` inside document `761`.
In that case the relation is not just an orphan operational link, and direct SQL would be fiscally unsafe because the
document is already stamped. Use a formal fiscal correction/replacement/cancellation process instead.

Do not change `import_status` back to `Pending`, and do not delete `sales_order` or `sales_order_item` rows. The
safe release is only `legacy_import_record.billing_document_id = NULL`.

## Code Fix Proposal

- Move mutation-lock validation before any import-record mutation in `UpdateBillingDocumentOrderAssociationService`.
- Do not set `targetImportRecord.BillingDocumentId` until after all billing and fiscal item composition has succeeded.
- Defer primary import-record repair from `EnsurePrimaryAssociationAndLoadSalesOrdersAsync`; collect required link changes and apply them immediately before the single successful `SaveChangesAsync`.
- Add an explicit transaction abstraction for multi-step document composition and import-link updates.
- Make audit writes use a separate `BillingDbContext` scope or factory so audit `SaveChangesAsync` cannot flush dirty domain entities from a failed use case.
- Keep the stamped-document guard explicit: reject if `billing_document.status != Draft`, or if an operational fiscal document exists outside `Draft`, `ReadyForStamping`, or `StampingRejected`, and additionally treat any persisted UUID as immutable evidence.
- Add structured logging on failed add-order attempts with `billing_document_id`, `sales_order_id`, `legacy_order_id`, user, timestamp, fiscal document id/status, and exception/error.
- Add an automated regression test: attempt to add a second order to a document with an editable fiscal document but missing product fiscal profile so fiscal item composition fails; then simulate endpoint audit save and assert `legacy_import_record.billing_document_id` for the second order remains `NULL` and no billing/fiscal items were added.
