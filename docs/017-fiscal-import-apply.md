# Fiscal Import Apply

## Purpose
Define the controlled apply step that moves validated staging rows into local fiscal master tables after preview has already been completed.

## Why apply is separate from preview
- preview is advisory and file-oriented
- apply changes local master data and must be auditable
- current master-table state may have changed between preview and apply
- create/update decisions must be re-evaluated at apply time, not trusted from stale preview results

## Apply eligibility rules
A staging row is eligible for apply only when:
- `Status = Valid`
- `SuggestedAction = Create` or `SuggestedAction = Update`

Rows must not be applied when:
- `Status = Ignored`
- `Status = Invalid`
- `SuggestedAction = Conflict`
- `SuggestedAction = NeedsEnrichment`
- the row was already applied successfully and no reapply mode exists

## Apply modes

### CreateOnly
- applies only rows whose current effective action resolves to `Create`
- if a row resolves to `Update` at apply time, it must be skipped

### CreateAndUpdate
- applies rows that currently resolve to `Create`
- applies rows that currently resolve to `Update`

## Revalidation at apply time
Preview is not authoritative for final action selection.

Apply must re-check the current master tables:
- receiver key: normalized RFC
- product key: normalized internal code

Consequences:
- if preview said `Create` but the master now exists, the row now resolves to `Update`
- if preview said `Update` but the master no longer exists, the row now resolves to `Create`
- preview `ExistingMasterEntityId` is informational only and must not be trusted blindly

Current-state revalidation wins over preview metadata.

## Receiver update policy
The receiver apply step must never update `Rfc`.

Allowed overwrite fields:
- `LegalName`
- `FiscalRegimeCode`
- `CfdiUseCodeDefault`
- `PostalCode`
- `CountryCode`

Optional fields:
- `ForeignTaxRegistration`
- `Email`
- `Phone`

Optional-field policy:
- do not erase an existing master value when staging value is null or blank
- overwrite only when staging value is non-empty

Alias policy:
- do not derive `SearchAlias` from import in this phase
- preserve the existing alias when staging has no explicit alias data

## Product update policy
The product apply step must never update `InternalCode`.

Allowed overwrite fields:
- `Description`
- `SatProductServiceCode`
- `SatUnitCode`
- `TaxObjectCode`
- `VatRate`
- `DefaultUnitText`

Optional-field policy:
- do not erase an existing master value when staging value is null or blank
- overwrite only when staging value is non-empty

## Idempotent re-run behavior
- a row already applied successfully must not create duplicates on re-run
- re-running apply on the same successful row should mark the row as `AlreadyApplied`
- the original `AppliedAtUtc` and applied master id remain the evidence that the row was applied previously

## Row-level audit behavior
Each staging row stores:
- `ApplyStatus`
- `AppliedAtUtc`
- `ApplyErrorMessage`
- `AppliedMasterEntityId`

`ImportApplyStatus` values:
- `NotApplied`
- `Applied`
- `Skipped`
- `Failed`
- `AlreadyApplied`

Batch-level apply summary stores:
- `AppliedRows`
- `ApplyFailedRows`
- `ApplySkippedRows`
- `LastAppliedAtUtc`

These fields make the apply step auditable without adding PAC or fiscal-document behavior.

## No PAC involvement
- apply affects only local fiscal master tables
- no PAC call is made during apply
- no SAT live validation is introduced in this phase
- no `FiscalDocument` snapshot generation is involved yet

## Immediate implementation direction
- use preview to load and validate staging rows
- use apply to create/update local masters with revalidation
- keep master mutations explicit, typed, and auditable before moving on to fiscal snapshot generation
