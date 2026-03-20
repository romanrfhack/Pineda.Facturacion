# Fiscal Document Foundation

## Purpose
Define the first persisted fiscal snapshot layer created from an existing `BillingDocument` plus local fiscal master data, without PAC, XML generation, cancellation, or payment complements yet.

## BillingDocument vs FiscalDocument
`BillingDocument`:
- internal commercial document
- derived from the imported sales snapshot
- suitable for internal billing workflows

`FiscalDocument`:
- persisted fiscal snapshot
- created from `BillingDocument` plus issuer/receiver/product fiscal master data
- the future source for PAC payload generation

`FiscalDocumentItem`:
- persisted line-level fiscal snapshot
- stores SAT and tax fields required by later PAC integration

## Why FiscalDocument is a snapshot
- issuer, receiver, and product fiscal data can change over time
- PAC must consume stable persisted data, not live master records
- once created, a FiscalDocument must be sufficient for later PAC operations without reconstructing from `SalesOrder` or current master tables
- PAC operational references from the selected issuer profile are also snapshotted so stamping does not depend on mutable issuer configuration

## Resolution rules

### Issuer
- use the provided `IssuerProfileId` when present
- otherwise use the active issuer profile
- issuer must be active and contain:
  - legal name
  - RFC
  - fiscal regime code
  - postal code
  - CFDI version
- the fiscal snapshot also stores:
  - PAC environment reference
  - certificate reference
  - private key reference
  - private key password reference

### Receiver
- receiver is selected explicitly by `FiscalReceiverId`
- receiver must be active and contain:
  - RFC
  - legal name
  - fiscal regime code
  - postal code
  - CFDI use code
- `ReceiverCfdiUseCode` in the snapshot:
  - use command override when present
  - otherwise use `FiscalReceiver.CfdiUseCodeDefault`

### Product fiscal mapping
- each BillingDocument line must resolve a `ProductFiscalProfile`
- current stable key: `BillingDocumentItem.ProductInternalCode -> ProductFiscalProfile.InternalCode`
- no free-text guessing is allowed
- if a line has no stable internal code or no active product fiscal profile match, the entire operation fails

### Currency semantics
- `BillingDocument.CurrencyCode` is the commercial source of truth for the fiscal snapshot
- `BillingDocument.ExchangeRate` is the commercial source of truth for the fiscal snapshot
- `PrepareFiscalDocument` must not re-read `SalesOrder` for currency semantics once BillingDocument exists
- current MVP policy is `MXN`-only for BillingDocument creation and FiscalDocument preparation
- multi-currency is deferred until the imported commercial snapshot includes a reliable exchange rate

## Failure behavior
The system must fail the whole snapshot creation when required fiscal data is missing.

Examples:
- no active issuer profile
- missing receiver
- missing receiver CFDI use
- any product line missing SAT mapping
- inconsistent item tax rate vs product fiscal profile VAT rate
- unsupported non-`MXN` BillingDocument currency in the current MVP

No partial FiscalDocument should be created in these cases.

## Payment fields included in the snapshot
The snapshot persists:
- payment method SAT
- payment form SAT
- payment condition
- credit sale flag
- credit days

This is required so later PAC and credit-sale flows can read fiscal payment semantics from the snapshot itself.

## Current hardening note
- `UnitText` still comes from `ProductFiscalProfile.DefaultUnitText`
- this remains acceptable for now because unit text is not yet explicitly modeled on BillingDocumentItem

## ReadyForStamping in this phase
`ReadyForStamping` means:
- fiscal snapshot creation succeeded
- issuer, receiver, and item-level SAT data were resolved and copied locally
- payment fields are present
- PAC operational references were copied from the selected issuer profile
- the document is structurally ready for a future PAC integration phase

It does not mean:
- PAC has been called
- XML exists
- any digital signature exists
- cancellation exists
- multi-currency support exists in the current MVP

## Explicit non-goals in this step
- no PAC calls
- no XML generation
- no digital signatures
- no cancellation
- no payment complement generation
- no SAT live catalog validation
