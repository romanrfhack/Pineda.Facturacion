-- Read-only preflight for fiscal reconciliation.
-- Case:
--   fiscal_document.id = 1612
--   billing_document.id = 1661
--   fiscal_stamp.id = 1607
--   expected UUID = 7c432a75-2b53-4dc3-9266-aa46b0e538e1
--
-- Do not edit production data with this script.

SET @fiscal_document_id := 1612;
SET @billing_document_id := 1661;
SET @fiscal_stamp_id := 1607;
SET @xml_path := '/tmp/fd-1612-stamped-provider.xml';

SET @expected_uuid := '7c432a75-2b53-4dc3-9266-aa46b0e538e1';
SET @expected_series := 'A';
SET @expected_folio := '33611';
SET @expected_issuer_rfc := 'ARP9706105W2';
SET @expected_receiver_rfc := 'XAXX010101000';
SET @expected_payment_form := '04';
SET @expected_payment_method := 'PUE';
SET @expected_payment_condition := 'Contado';
SET @expected_subtotal := '18804.34';
SET @expected_total_impuestos_trasladados := '3008.69';
SET @expected_total := '21813.03';
SET @expected_fecha_local := '2026-05-29T12:09:47';
SET @expected_fecha_timbrado_local := '2026-05-29T12:11:13';
SET @expected_rfc_prov_certif := 'LSO1306189R5';
SET @expected_no_certificado_sat := '00001000000719545303';
SET @expected_stamped_at_utc := '2026-05-29 18:11:13';

SELECT
    DATABASE() AS current_database_name;

SELECT
    @@secure_file_priv AS secure_file_priv;

SELECT
    LOAD_FILE('/tmp/fd-1612-stamped-provider.xml') IS NOT NULL AS xml_can_be_loaded;

SET @xml_payload := LOAD_FILE(@xml_path);

SELECT
    @xml_payload IS NOT NULL AS xml_loaded,
    CHAR_LENGTH(@xml_payload) AS xml_length,
    INSTR(COALESCE(@xml_payload, ''), 'TimbreFiscalDigital') > 0 AS xml_has_timbre_fiscal_digital,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('UUID="', @expected_uuid, '"')) > 0 AS xml_has_expected_uuid,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('Serie="', @expected_series, '"')) > 0 AS xml_has_expected_series,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('Folio="', @expected_folio, '"')) > 0 AS xml_has_expected_folio,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('Rfc="', @expected_issuer_rfc, '"')) > 0 AS xml_has_expected_issuer_rfc,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('Rfc="', @expected_receiver_rfc, '"')) > 0 AS xml_has_expected_receiver_rfc,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('Fecha="', @expected_fecha_local, '"')) > 0 AS xml_has_expected_fecha,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('FechaTimbrado="', @expected_fecha_timbrado_local, '"')) > 0 AS xml_has_expected_fecha_timbrado,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('RfcProvCertif="', @expected_rfc_prov_certif, '"')) > 0 AS xml_has_expected_rfc_prov_certif,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('NoCertificadoSAT="', @expected_no_certificado_sat, '"')) > 0 AS xml_has_expected_no_certificado_sat,
    INSTR(COALESCE(@xml_payload, ''), 'SelloSAT="') > 0 AS xml_has_sello_sat,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('SubTotal="', @expected_subtotal, '"')) > 0 AS xml_has_expected_subtotal,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('TotalImpuestosTrasladados="', @expected_total_impuestos_trasladados, '"')) > 0 AS xml_has_expected_total_impuestos_trasladados,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('Total="', @expected_total, '"')) > 0 AS xml_has_expected_total,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('MetodoPago="', @expected_payment_method, '"')) > 0 AS xml_has_expected_metodo_pago,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('FormaPago="', @expected_payment_form, '"')) > 0 AS xml_has_expected_forma_pago,
    INSTR(COALESCE(@xml_payload, ''), CONCAT('CondicionesDePago="', @expected_payment_condition, '"')) > 0 AS xml_has_expected_condiciones_de_pago;

SELECT
    provider_code,
    provider_message,
    COUNT(*) AS matched_rows
FROM fiscal_stamp
WHERE status = 1
GROUP BY provider_code, provider_message
ORDER BY COUNT(*) DESC
LIMIT 20;

SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fd.status AS fiscal_document_status,
    fd.document_type,
    fd.series,
    fd.folio,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.payment_condition,
    fd.is_credit_sale,
    fd.credit_days,
    fd.issuer_rfc,
    fd.receiver_rfc,
    fd.subtotal,
    fd.tax_total,
    fd.total,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    fs.uuid,
    fs.stamped_at_utc,
    fs.provider_name,
    fs.provider_operation,
    fs.provider_request_hash,
    fs.provider_tracking_id,
    fs.provider_code,
    fs.provider_message,
    fs.error_code,
    fs.error_message,
    CHAR_LENGTH(fs.xml_content) AS xml_content_length,
    fs.xml_hash,
    CHAR_LENGTH(fs.raw_response_summary_json) AS raw_response_summary_json_length
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE fd.id = @fiscal_document_id
  AND fd.billing_document_id = @billing_document_id;

SELECT
    fd.id AS fiscal_document_id,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.payment_condition,
    fd.is_credit_sale,
    fd.credit_days,
    CASE
        WHEN fd.payment_method_sat = 'PPD'
         AND fd.payment_form_sat = '99'
         AND fd.is_credit_sale = 1
         AND COALESCE(fd.credit_days, 0) > 0
        THEN 1 ELSE 0
    END AS accounts_receivable_insert_applicable_expected_0_for_pue_contado,
    COUNT(ari.id) AS existing_accounts_receivable_invoice_count
FROM fiscal_document fd
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.id = @fiscal_document_id
GROUP BY
    fd.id,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.payment_condition,
    fd.is_credit_sale,
    fd.credit_days;

SELECT
    COUNT(*) AS existing_manual_reconciliation_audit_events
FROM audit_event ae
WHERE ae.action_type = 'FiscalStamp.ManualReconciliationFromAuthoritativeXml'
  AND ae.entity_type = 'FiscalDocument'
  AND ae.entity_id = CAST(@fiscal_document_id AS CHAR);
