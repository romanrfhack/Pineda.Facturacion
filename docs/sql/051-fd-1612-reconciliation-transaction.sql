-- Transactional fiscal reconciliation for a single stamped CFDI.
-- Default behavior is ROLLBACK.
--
-- Case:
--   fiscal_document.id = 1612
--   billing_document.id = 1661
--   fiscal_stamp.id = 1607
--   expected UUID = 7c432a75-2b53-4dc3-9266-aa46b0e538e1
--
-- Provider code decision:
--   Leave @provider_code_for_success = NULL unless the preflight query proves
--   that successful fiscal_stamp rows in the target database normally persist
--   provider_code = '200' for this stamping flow.
--
-- Safe LOAD_FILE fallback if needed:
--   If LOAD_FILE() returns NULL, do not paste XML into git, logs, or audit_event.
--   In a secure operator session, comment the direct LOAD_FILE() line and use a
--   temporary table populated from LOCAL INFILE on the workstation instead:
--
--   SET SESSION group_concat_max_len = 16 * 1024 * 1024;
--   CREATE TEMPORARY TABLE tmp_provider_xml_line (
--       line_no BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
--       line_text LONGTEXT NOT NULL
--   ) ENGINE = InnoDB;
--   LOAD DATA LOCAL INFILE '/tmp/fd-1612-stamped-provider.xml'
--   INTO TABLE tmp_provider_xml_line
--   CHARACTER SET utf8mb4
--   LINES TERMINATED BY '\n'
--   (@line_text)
--   SET line_text = TRIM(TRAILING '\r' FROM @line_text);
--   SELECT GROUP_CONCAT(line_text ORDER BY line_no SEPARATOR '\n')
--   INTO @xml_payload
--   FROM tmp_provider_xml_line;
--   DROP TEMPORARY TABLE tmp_provider_xml_line;

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
SET @expected_stamped_at_utc := '2026-05-29 18:11:13';
SET @expected_stamped_at_utc_iso := '2026-05-29T18:11:13Z';
SET @expected_rfc_prov_certif := 'LSO1306189R5';
SET @expected_no_certificado_sat := '00001000000719545303';

SET @correlation_id := 'fd-1612-fs-1607-manual-xml-reconciliation-20260529';
SET @provider_code_for_success := NULL;
SET @provider_message_for_success := NULL;
SET @provider_request_hash_for_success := NULL;
SET @provider_tracking_id_for_success := NULL;

START TRANSACTION;

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

SET @previous_fiscal_document_status := NULL;
SET @previous_fiscal_stamp_status := NULL;
SET @previous_uuid := NULL;
SET @previous_provider_code := NULL;
SET @previous_provider_message := NULL;
SET @previous_error_message := NULL;
SET @previous_raw_response_summary_json := NULL;
SET @previous_provider_request_hash := NULL;
SET @previous_provider_tracking_id := NULL;

SELECT
    fd.status
INTO
    @previous_fiscal_document_status
FROM fiscal_document fd
WHERE fd.id = @fiscal_document_id
  AND fd.billing_document_id = @billing_document_id
LIMIT 1
FOR UPDATE;

SELECT
    fs.status,
    fs.uuid,
    fs.provider_code,
    fs.provider_message,
    fs.error_message,
    fs.raw_response_summary_json,
    fs.provider_request_hash,
    fs.provider_tracking_id
INTO
    @previous_fiscal_stamp_status,
    @previous_uuid,
    @previous_provider_code,
    @previous_provider_message,
    @previous_error_message,
    @previous_raw_response_summary_json,
    @previous_provider_request_hash,
    @previous_provider_tracking_id
FROM fiscal_stamp fs
WHERE fs.id = @fiscal_stamp_id
  AND fs.fiscal_document_id = @fiscal_document_id
LIMIT 1
FOR UPDATE;

UPDATE fiscal_stamp fs
JOIN fiscal_document fd
    ON fd.id = fs.fiscal_document_id
SET
    fs.status = 1,
    fs.uuid = @expected_uuid,
    fs.stamped_at_utc = @expected_stamped_at_utc,
    fs.xml_content = @xml_payload,
    fs.xml_hash = SHA2(@xml_payload, 256),
    fs.provider_code = @provider_code_for_success,
    fs.provider_message = @provider_message_for_success,
    fs.provider_request_hash = @provider_request_hash_for_success,
    fs.provider_tracking_id = @provider_tracking_id_for_success,
    fs.raw_response_summary_json = JSON_OBJECT(
        'kind', 'manual-xml-reconciliation',
        'reconciledAtUtc', DATE_FORMAT(UTC_TIMESTAMP(6), '%Y-%m-%dT%H:%i:%s.%fZ'),
        'reconciledBy', 'manual-sql',
        'fiscalDocumentId', @fiscal_document_id,
        'billingDocumentId', @billing_document_id,
        'fiscalStampId', @fiscal_stamp_id,
        'xmlFilePath', @xml_path,
        'expectedUuid', @expected_uuid,
        'expectedSerie', @expected_series,
        'expectedFolio', @expected_folio,
        'expectedMetodoPago', @expected_payment_method,
        'expectedFormaPago', @expected_payment_form,
        'expectedCondicionesDePago', @expected_payment_condition,
        'expectedFecha', @expected_fecha_local,
        'expectedFechaTimbrado', @expected_fecha_timbrado_local,
        'expectedStampedAtUtc', @expected_stamped_at_utc_iso,
        'providerCodeApplied', @provider_code_for_success,
        'providerMessageApplied', @provider_message_for_success,
        'previousFiscalDocumentStatus', @previous_fiscal_document_status,
        'previousFiscalStampStatus', @previous_fiscal_stamp_status,
        'previousUuid', @previous_uuid,
        'previousProviderCode', @previous_provider_code,
        'previousProviderMessage', @previous_provider_message,
        'previousErrorMessage', @previous_error_message,
        'previousRawResponseSummaryJson',
            CASE
                WHEN @previous_raw_response_summary_json IS NULL
                  OR CHAR_LENGTH(TRIM(@previous_raw_response_summary_json)) = 0
                THEN NULL
                ELSE @previous_raw_response_summary_json
            END,
        'previousProviderRequestHash', @previous_provider_request_hash,
        'previousProviderTrackingId', @previous_provider_tracking_id
    ),
    fs.error_code = NULL,
    fs.error_message = NULL,
    fs.updated_at_utc = UTC_TIMESTAMP(6)
WHERE fs.id = @fiscal_stamp_id
  AND fs.fiscal_document_id = @fiscal_document_id
  AND fd.billing_document_id = @billing_document_id
  AND @xml_payload IS NOT NULL
  AND INSTR(@xml_payload, 'TimbreFiscalDigital') > 0
  AND INSTR(@xml_payload, CONCAT('UUID="', @expected_uuid, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('Serie="', @expected_series, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('Folio="', @expected_folio, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('Rfc="', @expected_issuer_rfc, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('Rfc="', @expected_receiver_rfc, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('Fecha="', @expected_fecha_local, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('FechaTimbrado="', @expected_fecha_timbrado_local, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('RfcProvCertif="', @expected_rfc_prov_certif, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('NoCertificadoSAT="', @expected_no_certificado_sat, '"')) > 0
  AND INSTR(@xml_payload, 'SelloSAT="') > 0
  AND INSTR(@xml_payload, CONCAT('SubTotal="', @expected_subtotal, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('TotalImpuestosTrasladados="', @expected_total_impuestos_trasladados, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('Total="', @expected_total, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('MetodoPago="', @expected_payment_method, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('FormaPago="', @expected_payment_form, '"')) > 0
  AND INSTR(@xml_payload, CONCAT('CondicionesDePago="', @expected_payment_condition, '"')) > 0;

SET @updated_fiscal_stamp := ROW_COUNT();

SELECT @updated_fiscal_stamp AS updated_fiscal_stamp_must_be_1;

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
    END AS accounts_receivable_insert_applicable_expected_0_for_pue_contado
FROM fiscal_document fd
WHERE fd.id = @fiscal_document_id;

SET @ar_rows := 0;

SELECT @ar_rows AS inserted_ar_invoice_expected_0_for_pue_contado;

UPDATE fiscal_document fd
SET
    fd.status = 3,
    fd.updated_at_utc = UTC_TIMESTAMP(6)
WHERE fd.id = @fiscal_document_id
  AND fd.billing_document_id = @billing_document_id
  AND fd.status <> 8
  AND EXISTS (
      SELECT 1
      FROM fiscal_stamp fs
      WHERE fs.id = @fiscal_stamp_id
        AND fs.fiscal_document_id = fd.id
        AND fs.status = 1
        AND fs.uuid = @expected_uuid
        AND fs.xml_content IS NOT NULL
        AND INSTR(fs.xml_content, 'TimbreFiscalDigital') > 0
  );

SET @updated_fiscal_document := ROW_COUNT();

SELECT @updated_fiscal_document AS updated_fiscal_document_must_be_1;

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
    'FiscalStamp.ManualReconciliationFromAuthoritativeXml',
    'FiscalDocument',
    CAST(fd.id AS CHAR),
    'Applied',
    @correlation_id,
    JSON_OBJECT(
        'fiscalDocumentId', @fiscal_document_id,
        'billingDocumentId', @billing_document_id,
        'fiscalStampId', @fiscal_stamp_id,
        'xmlFilePath', @xml_path,
        'expectedUuid', @expected_uuid,
        'expectedSerie', @expected_series,
        'expectedFolio', @expected_folio,
        'expectedMetodoPago', @expected_payment_method,
        'expectedFormaPago', @expected_payment_form,
        'expectedCondicionesDePago', @expected_payment_condition,
        'expectedFecha', @expected_fecha_local,
        'expectedFechaTimbrado', @expected_fecha_timbrado_local
    ),
    JSON_OBJECT(
        'fiscalDocumentStatus', fd.status,
        'fiscalStampStatus', fs.status,
        'uuid', fs.uuid,
        'xmlContainsTimbreFiscalDigital', INSTR(fs.xml_content, 'TimbreFiscalDigital') > 0,
        'providerCodeApplied', fs.provider_code,
        'providerMessageApplied', fs.provider_message,
        'updatedFiscalStampRows', @updated_fiscal_stamp,
        'updatedFiscalDocumentRows', @updated_fiscal_document,
        'insertedArInvoiceRows', @ar_rows
    ),
    NULL,
    NULL,
    NULL,
    UTC_TIMESTAMP(6)
FROM fiscal_document fd
JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE fd.id = @fiscal_document_id
  AND fd.billing_document_id = @billing_document_id
  AND fd.status = 3
  AND fs.id = @fiscal_stamp_id
  AND fs.status = 1
  AND fs.uuid = @expected_uuid
  AND fs.xml_content IS NOT NULL
  AND INSTR(fs.xml_content, 'TimbreFiscalDigital') > 0
  AND NOT EXISTS (
      SELECT 1
      FROM audit_event ae
      WHERE ae.correlation_id = @correlation_id
        AND ae.action_type = 'FiscalStamp.ManualReconciliationFromAuthoritativeXml'
        AND ae.entity_type = 'FiscalDocument'
        AND ae.entity_id = CAST(fd.id AS CHAR)
  );

SET @inserted_audit_event := ROW_COUNT();

SELECT @inserted_audit_event AS inserted_audit_event_must_be_1;

SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fd.status AS fiscal_document_status,
    fs.id AS fiscal_stamp_id,
    fs.status AS fiscal_stamp_status,
    fs.uuid,
    fs.stamped_at_utc,
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
    COUNT(*) AS audit_event_count_in_transaction_expected_1
FROM audit_event ae
WHERE ae.correlation_id = @correlation_id
  AND ae.action_type = 'FiscalStamp.ManualReconciliationFromAuthoritativeXml'
  AND ae.entity_type = 'FiscalDocument'
  AND ae.entity_id = CAST(@fiscal_document_id AS CHAR);

ROLLBACK;
-- COMMIT;
