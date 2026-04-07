# Product Catalog / CFDI Audit

Date: 2026-04-07

## Scope
Audit of the current schema and flow around:
- product catalog
- SAT product/service codes
- SAT unit codes
- invoice concepts/lines
- stamped XML, fiscal JSON, and PDF generation

## Executive summary
- There is no canonical `products` table today. The closest current master table is `product_fiscal_profile`, keyed by `internal_code`, and commercial product identity also appears inside imported `sales_order_item`.
- SAT product/service has a local canonical table: `sat_product_service_catalog`.
- SAT unit does not have a canonical table today. The code is only copied as `sat_unit_code` across line/profile tables.
- Invoice concepts live in two layers:
  - commercial snapshot: `billing_document_item`
  - fiscal snapshot: `fiscal_document_item`
- Stamped XML lives in `fiscal_stamp.xml_content`.
- Fiscal request JSON is generated transiently inside `FacturaloPlusStampingGateway`; it is not persisted as a first-class business artifact. The system persists only `provider_request_hash` plus response summary JSON.
- PDF is not persisted. It is rendered on demand from `fiscal_document` plus `fiscal_stamp.xml_content`.
- Historical stamped CFDI does not depend on a FK to products, but pre-stamp fiscal preparation and later editable rebuild flows still resolve SAT/tax data from the current `product_fiscal_profile`.

## Current schema map

### Commercial/import snapshot
- `sales_order_item`
  - imported legacy line snapshot
  - includes `legacy_article_id`, `sku`, `description`, `unit_code`, `unit_name`, `sat_product_service_code`, `sat_unit_code`
  - evidence: [20260319154255_InitialSnapshotImport.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260319154255_InitialSnapshotImport.cs#L91)

### Invoice concepts
- `billing_document_item`
  - current commercial invoice line table
  - stores `product_internal_code`, `description`, `quantity`, `unit_price`, `tax_rate`, `tax_amount`, `line_total`, `sat_product_service_code`, `sat_unit_code`, `tax_object_code`
  - evidence: [BillingDocumentItemConfiguration.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/BillingDocumentItemConfiguration.cs#L11)

### Fiscal snapshot
- `fiscal_document`
  - fiscal header snapshot
  - includes issuer, receiver, payment method/form/condition, totals, folio, operational references
  - evidence: [20260319233811_AddFiscalDocumentFoundation.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260319233811_AddFiscalDocumentFoundation.cs#L15)
- `fiscal_document_item`
  - fiscal line snapshot used for timbrado
  - stores `internal_code`, `description`, `sat_product_service_code`, `sat_unit_code`, `tax_object_code`, `vat_rate`, `unit_text`, totals
  - evidence: [FiscalDocumentItemConfiguration.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/FiscalDocumentItemConfiguration.cs#L11)

### Master/catalog data
- `product_fiscal_profile`
  - current local SAT assignment master for products/services
  - stores `internal_code`, `description`, `sat_product_service_code`, `sat_unit_code`, `tax_object_code`, `vat_rate`, `default_unit_text`
  - evidence: [ProductFiscalProfileConfiguration.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/ProductFiscalProfileConfiguration.cs#L11)
- `sat_product_service_catalog`
  - current official SAT product/service catalog persisted locally
  - evidence: [20260404234601_AddSatProductServiceCatalog.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260404234601_AddSatProductServiceCatalog.cs#L14)
- Missing today:
  - no `sat_clave_unidad`
  - no `sat_catalog_imports`
  - no `product_sat_assignment`
  - `SatCatalogDescriptionProvider` formats payment form/method/export and receiver catalogs, but it does not expose a persisted SAT unit catalog lookup
  - evidence: [SatCatalogDescriptionProvider.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Documents/SatCatalogDescriptionProvider.cs#L6)

### Import staging already present
- `product_fiscal_profile_import_batch`
- `product_fiscal_profile_import_row`
- current pattern already supports staging, validation, and apply for product fiscal profiles
- evidence: [20260319225221_AddFiscalImportStagingPreview.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260319225221_AddFiscalImportStagingPreview.cs#L73)

### XML / JSON / PDF
- `fiscal_stamp`
  - stores `provider_request_hash`, `xml_content`, `xml_hash`, `original_string`, `qr_code_text_or_url`, `raw_response_summary_json`
  - evidence: [FiscalStampConfiguration.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/FiscalStampConfiguration.cs#L12)
- no persisted PDF table
- no persisted raw fiscal request JSON table
  - supporting test: [FiscalStampingServicesTests.cs](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalStampingServicesTests.cs#L348)

## Objective-by-objective answers

### 1. Where data lives today
| Target | Current location |
|---|---|
| Catalogo de productos | No canonical `products` table. Current equivalents are `product_fiscal_profile` for fiscal assignment and `sales_order_item` for imported commercial line identity. |
| Claves SAT producto/servicio | Canonical catalog in `sat_product_service_catalog`; assigned copies in `product_fiscal_profile`, `sales_order_item`, `billing_document_item`, `fiscal_document_item`. |
| Claves SAT unidad | No canonical catalog table. Copied as `sat_unit_code` in `product_fiscal_profile`, `sales_order_item`, `billing_document_item`, `fiscal_document_item`. |
| Conceptos/partidas de factura | `billing_document_item` and later `fiscal_document_item`. |
| XML timbrado / JSON fiscal / PDF | XML in `fiscal_stamp.xml_content`; response JSON summary in `fiscal_stamp.raw_response_summary_json`; request JSON not persisted; PDF rendered on demand only. |

### 2. Do invoice concepts keep snapshot fields?
Current answer: yes, but at two different levels and with a gap.

`billing_document_item` stores:
- description
- sat product/service code
- sat unit code
- tax rate
- tax amount
- tax object code

`fiscal_document_item` stores:
- description
- sat product/service code
- sat unit code
- tax object code
- VAT rate
- totals
- unit text

Gap:
- `billing_document_item` does not store tax code/factor explicitly; it assumes the standard VAT flow.
- `fiscal_document_item` stores `vat_rate` and totals, but not an extensible tax breakdown JSON per concept.

### 3. What does XML/PDF generation read today?
Current answer:
- `PrepareFiscalDocumentService` reads `billing_document_item` for commercial description/amounts, but resolves SAT/tax fields from the current `product_fiscal_profile`.
  - evidence: [PrepareFiscalDocumentService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/PrepareFiscalDocumentService.cs#L212)
- `StampFiscalDocumentService` builds the PAC request only from `fiscal_document` and `fiscal_document_item`.
  - evidence: [StampFiscalDocumentService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/StampFiscalDocumentService.cs#L294)
  - supporting test: [FiscalStampingServicesTests.cs](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalStampingServicesTests.cs#L195)
- `FacturaloPlusStampingGateway` turns that normalized request into provider JSON in-memory and persists only request hash plus response summary.
  - evidence: [FacturaloPlusStampingGateway.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.FacturaloPlus/FacturaloPlus/FacturaloPlusStampingGateway.cs#L68)
  - supporting rule: [020-pac-stamping-flow.md](/home/romanrfhack/code/Pineda.Facturacion/docs/020-pac-stamping-flow.md#L37)
- `GetFiscalDocumentPdfService` requires a successful `fiscal_stamp` with `xml_content`, and the renderer parses the XML to render the concept table.
  - evidence: [GetFiscalDocumentPdfService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/GetFiscalDocumentPdfService.cs#L23)
  - evidence: [FiscalDocumentPdfRenderer.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Documents/FiscalDocumentPdfRenderer.cs#L29)

Conclusion:
- XML/timbrado reads the persisted fiscal snapshot.
- PDF reads the stamped XML first, with `fiscal_document` used as fallback for some header values.
- The weak point is not timbrado/PDF after snapshot creation; it is snapshot creation and editable rebuilds before stamp.

### 4. Risks detected

#### Foreign keys from concepts to products
- There is no FK from `billing_document_item` or `fiscal_document_item` to `product_fiscal_profile` or a `products` table.
- This is good for historical isolation after snapshot persistence.
- Current FK dependency is instead:
  - `billing_document_item.sales_order_item_id -> sales_order_item.id`
  - `fiscal_document_item.billing_document_item_id -> optional lineage only`
- Evidence: [BillingDocumentItemConfiguration.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/BillingDocumentItemConfiguration.cs#L118)

#### Logical dependence on current catalog
- `PrepareFiscalDocumentService` resolves SAT/unit/tax from live `product_fiscal_profile`.
- editable rebuild flows do the same:
  - `RemoveBillingDocumentItemService`
  - `AssignPendingBillingItemsService`
  - `UpdateBillingDocumentOrderAssociationService`
  - `ReimportLegacyOrderService`
- evidence: [PrepareFiscalDocumentService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/PrepareFiscalDocumentService.cs#L225)
- evidence: [RemoveBillingDocumentItemService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/RemoveBillingDocumentItemService.cs#L260)
- evidence: [AssignPendingBillingItemsService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/AssignPendingBillingItemsService.cs#L300)
- evidence: [UpdateBillingDocumentOrderAssociationService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/UpdateBillingDocumentOrderAssociationService.cs#L320)
- evidence: [ReimportLegacyOrderService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs#L477)

Risk statement:
- a document already prepared but still editable can change SAT/tax semantics if `product_fiscal_profile` changes before the next rebuild or before re-preparation.

#### Triggers / jobs / procedures
- No SQL triggers, views, procedures, or functions were found in the repository search.
- Hosted services found:
  - `StandardVat16BackfillHostedService`
  - `SatProductServiceCatalogBootstrapHostedService`
- `StandardVat16BackfillHostedService` only normalizes zero-tax `sales_order` and non-fiscalized `billing_document`; it explicitly skips billing documents that already have a `fiscal_document`.
  - evidence: [StandardVat16BackfillHostedService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Security/StandardVat16BackfillHostedService.cs#L31)
- `SatProductServiceCatalogBootstrapHostedService` upserts `sat_product_service_catalog` on startup from an embedded JSON resource.
  - evidence: [SatProductServiceCatalogBootstrapHostedService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Security/SatProductServiceCatalogBootstrapHostedService.cs#L27)
  - evidence: [SatProductServiceCatalogSeedSource.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/SatCatalogs/SatProductServiceCatalogSeedSource.cs#L7)

#### Views that reconstruct invoices from current tables
- No SQL views found.
- The important read path for PDF does not reconstruct from current product catalog; it reconstructs from stamped XML.

#### Additional gaps
- no canonical SAT unit catalog
- no import audit for official SAT catalogs
- no persistent raw fiscal request artifact
- no independent product-to-SAT assignment history; `product_fiscal_profile` is mutable current state

## Proposed target design

Reference SQL:
- migration: [20260407_sat_catalog_migration.sql](/home/romanrfhack/code/Pineda.Facturacion/docs/sql/20260407_sat_catalog_migration.sql)
- load/import: [20260407_sat_catalog_import.sql](/home/romanrfhack/code/Pineda.Facturacion/docs/sql/20260407_sat_catalog_import.sql)
- regression checks: [20260407_sat_catalog_regression.sql](/home/romanrfhack/code/Pineda.Facturacion/docs/sql/20260407_sat_catalog_regression.sql)

### Canonical tables
- `sat_catalog_imports`
  - import audit by catalog, release, file, sheet, hash, row count
- `sat_clave_prodserv`
  - canonical official SAT product/service catalog
- `sat_clave_unidad`
  - canonical official SAT unit catalog
- `product_sat_assignment`
  - versioned internal assignment from product to SAT prodserv/unit plus tax semantics and review metadata

### Minimal changes in products
Recommended if a future canonical `products` table exists:
- keep only a pointer to the current assignment
- do not duplicate SAT fields directly in `products`
- add status fields for assignment completeness

### Minimal changes in invoice_lines
Recommended:
- keep invoice line snapshot columns authoritative
- optionally store the assignment id used to produce the snapshot
- never resolve stamped or already-snapshotted lines from the current assignment again

Current repo mapping:
- `products` equivalent today: `product_fiscal_profile` plus commercial identifiers in `sales_order_item`
- `invoice_lines` equivalent today: `billing_document_item`
- stamped fiscal line snapshot already exists in `fiscal_document_item`

## Proposed load strategy
1. Download the official SAT XLS/XLSX.
2. Export one CSV per sheet:
   - `prodserv.csv`
   - `unidad.csv`
3. Load each CSV into a staging table.
4. Insert one row per sheet into `sat_catalog_imports`.
5. Normalize and upsert into:
   - `sat_clave_prodserv`
   - `sat_clave_unidad`
6. Keep raw row JSON or raw source columns only at staging/import-audit level.
7. Build or refresh product assignment candidates separately from official catalog ingestion.

Why separate them:
- SAT official catalog and internal product assignment are different concerns.
- the SAT import should be deterministic and idempotent
- internal mapping quality should be reviewable with `source`, `confidence`, and `review_status`

## Proposed backfill
Recommended steps:
1. Seed `product_sat_assignment` from the current `product_fiscal_profile` where there is a one-to-one `internal_code`.
2. Mark provenance:
   - `source = 'product_fiscal_profile'`
   - `confidence = 0.95` for exact current mapping carried forward
   - `review_status = 'Approved'` only when exact canonical catalog matches exist
3. For products inferred from description/text matching only:
   - `source = 'heuristic'`
   - lower confidence
   - `review_status = 'NeedsReview'`
4. Queue ambiguities with:
   - multiple SAT candidates
   - missing unit code
   - invalid SAT code not found in canonical catalog
   - mismatched tax semantics
5. Freeze invoice line snapshots from the chosen assignment at invoice creation/preparation time.

Manual review queue rule:
- no extra table is required initially; query `product_sat_assignment` where `review_status = 'NeedsReview'`

## Regression tests to keep

### Existing behavior that must remain true
- `CreateBillingDocumentService` still snapshots line description and commercial amounts from source order.
  - evidence: [CreateBillingDocumentService.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/CreateBillingDocument/CreateBillingDocumentService.cs#L120)
- stamping still uses `fiscal_document` snapshot only
  - evidence: [FiscalStampingServicesTests.cs](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalStampingServicesTests.cs#L196)
- PDF still renders from stamped XML
  - evidence: [FiscalDocumentDeliveryServicesTests.cs](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalDocumentDeliveryServicesTests.cs#L15)

### New regression cases to add after migration
- changing current product assignment does not mutate existing `billing_document_item` snapshot columns
- changing current product assignment does not mutate existing `fiscal_document_item`
- stamping request still ignores live product repositories once `fiscal_document` exists
- PDF content remains stable after product assignment changes because it reads stamped XML
- import rerun for the same SAT release is idempotent
- backfill never creates assignments pointing to non-existent SAT codes
- `NeedsReview` queue is populated for ambiguous matches and empty for exact approved matches

The SQL smoke checks are in [20260407_sat_catalog_regression.sql](/home/romanrfhack/code/Pineda.Facturacion/docs/sql/20260407_sat_catalog_regression.sql).

## Rollback plan
1. Do not drop current tables during phase 1.
2. Deploy new canonical SAT tables and assignment tables additive-only.
3. Backfill `product_sat_assignment` without changing current read paths.
4. Add dual-write or compatibility reads only after validating row counts and regression checks.
5. If rollout fails:
   - stop writing new assignment ids
   - switch reads back to current `product_fiscal_profile`
   - keep new tables for forensic inspection
6. Only after stable adoption:
   - deprecate direct reads from `product_fiscal_profile` for new snapshot generation
   - optionally migrate `product_fiscal_profile` into a compatibility view or derived cache

Low-risk rollback property:
- current stamped history remains safe because `fiscal_document_item` and `fiscal_stamp.xml_content` already hold the stamped snapshot/evidence independently of the proposed new tables.
