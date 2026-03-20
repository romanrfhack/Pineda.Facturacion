# Fiscal Import Staging

## Purpose
Define the controlled preview-only import flow for fiscal receivers and product fiscal profiles before any row is allowed to mutate local master tables.

## Why staging is required
- Excel files are operational input, not trusted master truth.
- The system must expose row-level validation results before applying any receiver or product change.
- Duplicate rows, malformed key fields, and enrichment gaps must be visible in a batch preview.
- Product files in the current sample do not contain all required fiscal fields, so a direct import would force guessing.

Preview-only is required because:
1. users need deterministic row-by-row feedback before changing master data
2. existing master matches must be reviewed as `Update` or `Conflict`, not applied silently
3. missing SAT fiscal fields must remain explicit and auditable

Preview and apply are separate phases:
- preview parses Excel and persists staging batches/rows only
- apply reads previously persisted staging rows and revalidates against current master-table state
- preview must never be treated as permission to write directly to master tables

## Staging model

### Receiver staging
- `FiscalReceiverImportBatch`
- `FiscalReceiverImportRow`

### Product staging
- `ProductFiscalProfileImportBatch`
- `ProductFiscalProfileImportRow`

Batch records store:
- source file name
- batch status
- row counts
- duplicate counts
- existing-master-match counts
- created/completed timestamps

Row records store:
- source spreadsheet row number
- raw row JSON
- normalized key/value fields used by validation
- status
- suggested action
- validation errors
- optional existing master id reference

## Status model

### ImportBatchStatus
- `Uploaded`
- `Parsed`
- `Validated`
- `Failed`

### ImportRowStatus
- `Valid`
- `Invalid`
- `Ignored`

### ImportSuggestedAction
- `Create`
- `Update`
- `Conflict`
- `Ignore`
- `NeedsEnrichment`

## Receiver Excel mapping
Supported input columns:
- `ID`
- `CountryCode`
- `TaxID`
- `ForeignTaxID`
- `Name`
- `UsoCFDI`
- `DomicilioFiscal`
- `RegimenFiscal`
- `Street`
- `ExteriorNumber`
- `Colony`
- `InteriorNumber`
- `Municipality`
- `Locality`
- `xState`
- `Reference`
- `PostalCode`
- `EMail`
- `Phone`
- `FirstNames`

Preview mapping:
- `TaxID -> Rfc`
- `Name -> LegalName`
- `UsoCFDI -> CfdiUseCodeDefault`
- `DomicilioFiscal` preferred, else `PostalCode -> PostalCode`
- `RegimenFiscal -> FiscalRegimeCode`
- `CountryCode -> CountryCode`
- `ForeignTaxID -> ForeignTaxRegistration`
- `EMail -> Email`
- `Phone -> Phone`
- `ID -> SourceExternalId`

Normalization:
- `Rfc` must be trimmed and uppercased
- name and search-oriented fields must be normalized for deterministic case-insensitive lookup
- postal code, CFDI use, and fiscal regime must be trimmed and normalized before validation

## Product Excel mapping
Supported input columns:
- `ID`
- `Description`
- `ClaveProdServ`
- `ClaveUnidad`
- `EAN`
- `SKU`
- `SELLER`
- `Unit`
- `SubQuantity`
- `ListPrice`
- `DiscountRate`

Preview mapping:
- `SELLER -> InternalCode`
- `Description -> Description`
- `ClaveProdServ -> SatProductServiceCode`
- `ClaveUnidad -> SatUnitCode`
- `Unit -> DefaultUnitText`
- `ID -> SourceExternalId`

Important constraint:
- the provided sample product file does not include `TaxObjectCode`
- the provided sample product file does not include `VatRate`
- preview must not invent either value

Allowed optional batch defaults for preview:
- `DefaultTaxObjectCode`
- `DefaultVatRate`
- `DefaultUnitText`

## Validation rules

### Receiver row
A receiver row is valid only if:
- RFC exists
- legal name exists
- fiscal regime code exists
- default CFDI use exists
- postal code exists using `DomicilioFiscal` first, else `PostalCode`

Suggested actions:
- `Create`: valid row with no existing master match
- `Update`: valid row with existing local receiver found by RFC
- `Conflict`: malformed row or duplicate RFC inside the same file
- `Ignore`: blank row

### Product row
A product row is valid only if:
- internal code exists
- description exists
- SAT product/service code exists
- SAT unit code exists
- tax object code exists from explicit input or optional batch default
- VAT rate exists from explicit input or optional batch default

Suggested actions:
- `Create`: valid row with no existing master match
- `Update`: valid row with existing local product fiscal profile found by internal code
- `NeedsEnrichment`: tax object code or VAT rate is still missing after optional defaults
- `Conflict`: duplicate internal code in the same file or malformed row
- `Ignore`: blank row

## Duplicate detection rules
- Receiver duplicates are detected by normalized RFC within the same batch.
- Product duplicates are detected by normalized internal code within the same batch.
- Duplicate detection happens inside staging preview before any apply step.
- Duplicate rows are not auto-merged.

## Existing master match rules
- Receiver existing-master match is by normalized RFC.
- Product existing-master match is by normalized internal code.
- Preview may suggest `Update`, but it must not write to the master tables in this phase.

## NeedsEnrichment for products
`NeedsEnrichment` means:
- the row is structurally recognizable
- the row may still identify an intended product fiscal profile
- but required fiscal fields for master persistence are still missing

Typical causes:
- missing `TaxObjectCode`
- missing `VatRate`

`NeedsEnrichment` exists to keep these rows visible and auditable without guessing SAT or tax values.

## Required header behavior
- only `.xlsx` files are supported
- the first worksheet is parsed
- row 1 is treated as headers
- extra columns are tolerated
- missing required headers must fail the preview clearly

## Current boundary
- preview endpoints create staging batches and rows only
- apply endpoints exist as a separate explicit step and must operate only from persisted staging rows
- no master-table mutation happens during preview
- no SAT live validation is performed in this phase
- no PAC logic is involved in staging preview

See also:
- [017-fiscal-import-apply.md](./017-fiscal-import-apply.md)
