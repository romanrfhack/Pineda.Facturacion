# Fiscal Master Data

## Purpose
Define the local master data required to create valid fiscal snapshots without live inference at PAC call time.

## Design principles
- Fiscal master data is local system data.
- Receiver search must be fast by RFC and suitable for autocomplete.
- Product SAT mappings must be stored once in a dedicated fiscal profile model.
- Missing mandatory fiscal data must fail validation before fiscal snapshot generation.

## Issuer profile model
Represents the local issuing entity configuration used for CFDI generation.

Required fields:
- `IssuerProfileId`
- `LegalName`
- `Rfc`
- `FiscalRegimeCode`
- `PostalCode`
- `CfdiVersion`
- `CertificateReference`
- `PrivateKeyReference`
- `PrivateKeyPasswordReference`
- `PacEnvironment`
- `IsActive`

Optional fields:
- `CommercialName`
- `Email`
- `Phone`
- `SeriesInvoice`
- `SeriesCreditNote`
- `LogoReference`
- `AdditionalTaxRegistration`

Rules:
- Certificate and key material must be referenced by placeholder or external secret identifiers only.
- Only one issuer profile should be active for a given billing scope unless a future multi-issuer flow is documented.
- Issuer profile changes must not retroactively mutate persisted FiscalDocument snapshots.

## Receiver model
Represents local customer fiscal master data searchable by RFC.

Required fields:
- `ReceiverId`
- `Rfc`
- `LegalName`
- `FiscalRegimeCode`
- `CfdiUseCode`
- `PostalCode`
- `IsActive`

Optional fields:
- `CommercialName`
- `TaxResidenceCountryCode`
- `ForeignTaxRegistration`
- `Email`
- `Phone`
- `ContactName`
- `SearchAlias`
- `Notes`

Rules:
- RFC uniqueness must be enforced locally for active records, or conflicts must be handled explicitly.
- Receiver records are local master data, not live PAC-derived data.
- Billing workflows should select a receiver by local master record, not free-text re-entry on every fiscal operation.

## Product fiscal profile model
Represents reusable SAT mapping data for billable products or service lines.

Required fields:
- `ProductFiscalProfileId`
- `InternalCode`
- `Description`
- `SatProductServiceCode`
- `SatUnitCode`
- `TaxObjectCode`
- `VatRate`
- `IsActive`

Optional fields:
- `DefaultUnitText`
- `DefaultSku`
- `Brand`
- `Category`
- `Notes`
- `ValidFromUtc`
- `ValidToUtc`

Rules:
- SAT mappings must be stored explicitly.
- Commercial product identifiers may map to one fiscal profile, but the fiscal profile remains the authoritative SAT mapping source.
- Future versioning can be introduced with validity dates instead of mutating historical mappings.

## Required vs optional strategy
Required means:
- the field must exist before `FiscalDocument` snapshot generation can succeed
- missing values must produce explicit validation errors

Optional means:
- the field may be absent during early phases
- absence must not cause silent SAT inference
- absence can still block specific document types if a later rule requires it

## RFC search and autocomplete requirements
The receiver search experience must support:
- exact RFC lookup
- prefix search by RFC
- prefix search by legal name or search alias
- deterministic ranking favoring exact RFC matches first
- active-only filtering by default

Operational expectations:
- autocomplete should return enough fields to select a receiver without opening the full record
- search must be case-insensitive for RFC and name inputs
- RFC normalization should remove surrounding spaces and standardize casing before persistence/search

## Import-from-excel strategy
Excel import should be a controlled ingestion pipeline, not a direct overwrite.

Recommended steps:
1. Upload file to a staging area.
2. Parse rows into staging records with row numbers.
3. Validate required fields, RFC format, SAT code format, and duplicates.
4. Present validation errors by row.
5. Persist only accepted records into local master tables.
6. Keep an import audit summary with file name, timestamp, and counts.

Applies to:
- receivers
- product fiscal profiles

Current implementation direction:
- Step 2B introduces preview-only staging tables and preview endpoints first.
- Imported Excel rows stay in staging batches until a later explicit apply step is implemented.
- Product imports must surface missing `TaxObjectCode` and `VatRate` as enrichment gaps; they must not be guessed from SAT or commercial data.
- Receiver and product mappings for preview are documented in [016-fiscal-import-staging.md](./016-fiscal-import-staging.md).

## Validation rules

### Receiver validation
- RFC required and normalized
- Legal name required
- Fiscal regime required
- CFDI use required
- Postal code required

### Product fiscal profile validation
- SAT product/service code required
- SAT unit code required
- Tax object code required
- VAT rate required

### Issuer validation
- Exactly one active issuer profile for the current scope
- Certificate/key references required
- PAC environment required

## Fallback strategy when SAT mapping is missing
The system must not guess.

Allowed fallback behavior:
- stop fiscal snapshot generation
- return explicit validation errors naming the missing receiver or product fiscal field
- keep the BillingDocument intact for later correction

Disallowed fallback behavior:
- defaulting unknown SAT product codes automatically
- defaulting unknown SAT unit codes automatically
- generating PAC payloads from partial mappings

## Immediate implementation direction
- Add issuer, receiver, and product fiscal profile persistence models first.
- Use local ids in application workflows.
- Validate master data completeness before `FiscalDocument` generation.
