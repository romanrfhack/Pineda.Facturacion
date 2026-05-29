-- Post-validation for the reconciled fiscal document.
-- Run only after replacing ROLLBACK with COMMIT in the transactional script.

SET @fiscal_document_id := 1612;
SET @billing_document_id := 1661;
SET @fiscal_stamp_id := 1607;
SET @expected_uuid := '7c432a75-2b53-4dc3-9266-aa46b0e538e1';
SET @correlation_id := 'fd-1612-fs-1607-manual-xml-reconciliation-20260529';

SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fd.status AS fiscal_document_status,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.payment_condition,
    fd.is_credit_sale,
    fd.credit_days,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    fs.uuid,
    fs.stamped_at_utc,
    fs.provider_name,
    fs.provider_operation,
    fs.provider_tracking_id,
    fs.provider_code,
    fs.provider_message,
    fs.error_code,
    fs.error_message,
    CHAR_LENGTH(fs.xml_content) AS xml_content_length,
    INSTR(COALESCE(fs.xml_content, ''), 'TimbreFiscalDigital') > 0 AS xml_contains_timbre_fiscal_digital,
    ari.id AS accounts_receivable_invoice_id
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.id = @fiscal_document_id
  AND fd.billing_document_id = @billing_document_id;

SELECT
    COUNT(*) AS accounts_receivable_invoice_count_expected_0_for_pue_contado
FROM accounts_receivable_invoice ari
WHERE ari.fiscal_document_id = @fiscal_document_id;

SELECT
    COUNT(*) AS audit_event_count_expected_1
FROM audit_event ae
WHERE ae.correlation_id = @correlation_id
  AND ae.action_type = 'FiscalStamp.ManualReconciliationFromAuthoritativeXml'
  AND ae.entity_type = 'FiscalDocument'
  AND ae.entity_id = CAST(@fiscal_document_id AS CHAR);

SELECT
    fs.raw_response_summary_json
FROM fiscal_stamp fs
WHERE fs.id = @fiscal_stamp_id
  AND fs.fiscal_document_id = @fiscal_document_id
  AND fs.status = 1
  AND fs.uuid = @expected_uuid;
