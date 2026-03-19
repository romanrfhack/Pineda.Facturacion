# Database Design

## Database name
facturacion_v2

## Purpose
This database stores the new billing system write model.
It is independent from the legacy ERP database.

## Design principles
- The legacy database is not the new domain model.
- The new database stores snapshots, billing documents, fiscal documents, PAC traces, and audit data.
- Every billable operation must be traceable to its legacy source.
- The model must support idempotent imports and stable billing from snapshots.

## Core schemas
For now, the system will use a single MySQL database and the default schema strategy for MySQL.

## Main tables

### 1. legacy_import_record
Tracks each imported legacy source document.

Suggested columns:
- id
- source_system
- source_table
- source_document_id
- source_document_type
- source_hash
- import_status
- imported_at_utc
- last_seen_at_utc
- billing_document_id
- error_message

Constraints:
- unique on source_system + source_table + source_document_id

### 2. sales_order
Stores the imported snapshot header of the legacy order.

Suggested columns:
- id
- legacy_import_record_id
- legacy_order_number
- legacy_order_type
- customer_legacy_id
- customer_name
- customer_rfc
- payment_condition
- price_list_code
- delivery_type
- currency_code
- subtotal
- discount_total
- tax_total
- total
- snapshot_taken_at_utc
- status

### 3. sales_order_item
Stores the imported snapshot lines of the legacy order.

Suggested columns:
- id
- sales_order_id
- line_number
- legacy_article_id
- sku
- description
- unit_code
- unit_name
- quantity
- unit_price
- discount_amount
- tax_rate
- tax_amount
- line_total
- sat_product_service_code
- sat_unit_code

### 4. billing_document
Internal billable document generated from a snapshot.

Suggested columns:
- id
- sales_order_id
- document_type
- series
- folio
- status
- payment_condition
- payment_method_sat
- payment_form_sat
- issued_at_utc
- subtotal
- discount_total
- tax_total
- total
- created_at_utc
- updated_at_utc

Constraints:
- optional unique on document_type + series + folio

### 5. billing_document_item
Internal document lines.

Suggested columns:
- id
- billing_document_id
- line_number
- sku
- description
- quantity
- unit_price
- discount_amount
- tax_rate
- tax_amount
- line_total
- sat_product_service_code
- sat_unit_code
- tax_object_code

### 6. fiscal_document
Represents the fiscal layer of the billing document.

Suggested columns:
- id
- billing_document_id
- provider_name
- provider_environment
- fiscal_status
- cfdi_version
- uuid
- sat_status_code
- sat_status_description
- issued_at_utc
- stamped_at_utc
- cancelled_at_utc
- last_status_checked_at_utc

Constraints:
- unique on uuid

### 7. fiscal_stamp
Stores stamping evidence and PAC response details.

Suggested columns:
- id
- fiscal_document_id
- pac_tracking_id
- xml_unsigned
- xml_stamped
- original_string
- sat_original_string
- cfdi_seal
- sat_seal
- certificate_number
- sat_certificate_number
- qr_payload
- provider_raw_response
- created_at_utc

### 8. fiscal_cancellation
Stores cancellation intent and results.

Suggested columns:
- id
- fiscal_document_id
- reason_code
- replacement_uuid
- requested_at_utc
- cancelled_at_utc
- provider_raw_response
- cancellation_ack_xml
- status

### 9. pac_request_log
Technical audit of outbound PAC requests.

Suggested columns:
- id
- operation_name
- request_payload
- created_at_utc
- correlation_id

### 10. pac_response_log
Technical audit of inbound PAC responses.

Suggested columns:
- id
- pac_request_log_id
- response_payload
- http_status_code
- provider_code
- provider_message
- received_at_utc

### 11. system_setting
Stores configurable values for the billing system.

Suggested columns:
- id
- setting_key
- setting_value
- updated_at_utc

Examples:
- tax.vat.default_rate
- billing.series.invoice
- facturaloplus.environment

## Relationships
- legacy_import_record -> sales_order
- sales_order -> sales_order_item
- sales_order -> billing_document
- billing_document -> billing_document_item
- billing_document -> fiscal_document
- fiscal_document -> fiscal_stamp
- fiscal_document -> fiscal_cancellation
- pac_request_log -> pac_response_log

## Notes
- Exact SQL types and constraints may be refined during EF Core configuration.
- Snapshot tables represent immutable billing inputs.
- Billing and fiscal tables represent the new system source of truth.
