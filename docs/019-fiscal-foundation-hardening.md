# Fiscal Foundation Hardening

## Purpose
Close the remaining Step 3 gaps so fiscal snapshot preparation depends on `BillingDocument` as the commercial source of truth and uses an explicit persisted product-resolution key.

## Why CurrencyCode and ExchangeRate must live on BillingDocument
`BillingDocument` is the internal commercial document used as the source for later fiscal snapshot creation.

If currency semantics remain only on `SalesOrder`:
- fiscal snapshot preparation must still reach back to another aggregate
- BillingDocument is not self-sufficient as the commercial source of truth
- future fiscal operations risk depending on data outside the billing layer

For this reason `BillingDocument` now persists:
- `CurrencyCode`
- `ExchangeRate`

Rules:
- `CurrencyCode` is required
- `ExchangeRate` for `MXN` is persisted as `1`
- the current MVP supports `MXN` only in the normal BillingDocument -> FiscalDocument flow
- multi-currency is deferred until the import snapshot can carry a reliable exchange rate from source data

## Why PrepareFiscalDocument must not read SalesOrder anymore
`PrepareFiscalDocument` should prepare a fiscal snapshot from:
- `BillingDocument`
- selected `FiscalReceiver`
- selected/active `IssuerProfile`
- resolved `ProductFiscalProfile`

It should not pull missing commercial semantics from `SalesOrder` after `BillingDocument` already exists.

Current hardening result:
- currency code comes from `BillingDocument.CurrencyCode`
- exchange rate comes from `BillingDocument.ExchangeRate`
- no `ISalesOrderSnapshotRepository` dependency is required for fiscal snapshot creation
- current MVP validation rejects non-`MXN` BillingDocument currency during creation and fiscal snapshot preparation

## Chosen product-resolution invariant
`BillingDocumentItem.ProductInternalCode` is the persisted commercial product key used for fiscal-profile resolution.

Relationship:
- `BillingDocumentItem.ProductInternalCode -> ProductFiscalProfile.InternalCode`

Rules:
- `PrepareFiscalDocument` resolves product fiscal data using `ProductInternalCode` only
- `Sku` may still exist as commercial/display data, but it is no longer the implicit fiscal-resolution contract
- description must never be used for fiscal-profile lookup
- if `ProductInternalCode` is missing or unmatched, the whole fiscal snapshot creation fails

## How BillingDocument creation sets the invariant
During BillingDocument creation:
- `ProductInternalCode` is populated from the imported commercial `Sku` when available
- the value is normalized before persistence
- `CurrencyCode` is copied from the imported commercial snapshot
- `ExchangeRate` is set to `1` for `MXN`
- non-`MXN` SalesOrder snapshots are rejected for now because the legacy import model does not carry a reliable exchange rate

This keeps newly created BillingDocuments aligned with later fiscal preparation.

## Explicit MVP currency policy
The current MVP is `MXN`-only.

Reason:
- the legacy import mapping currently sets `CurrencyCode = MXN` as a constant
- no exchange-rate field exists in `LegacyOrderReadModel`, `SalesOrder`, or the documented legacy mapping
- persisting a non-`MXN` BillingDocument without reliable source exchange-rate data would create an unusable fiscal snapshot path

Deferred enhancement:
- add exchange-rate capture to the imported commercial snapshot
- persist it on `SalesOrder`
- carry it into `BillingDocument`
- allow non-`MXN` fiscal snapshot preparation only after that source data is trustworthy

## Remaining intentional gap
`FiscalDocumentItem.UnitText` still comes from `ProductFiscalProfile.DefaultUnitText`.

This remains intentional for now because:
- `BillingDocumentItem` does not yet persist a dedicated commercial unit text field
- the current hardening target is currency semantics and durable product-resolution

If later phases require unit text to be part of the commercial snapshot itself, that should be modeled explicitly on `BillingDocumentItem`.
