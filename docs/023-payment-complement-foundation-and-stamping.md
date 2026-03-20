# Payment Complement Foundation And Stamping

## Scope
This phase adds:
- persisted payment-complement fiscal snapshots
- persisted related-document rows
- PAC stamping for those persisted complement snapshots

This phase does not add:
- multi-currency complements
- regrouping or splitting payment events
- negative adjustments or reversals

## Why complements start from the payment ledger
The payment complement must be derived from:
- persisted `AccountsReceivablePayment`
- persisted `AccountsReceivablePaymentApplication`
- persisted stamped invoice evidence (`FiscalDocument` + `FiscalStamp`)

This avoids reconstructing complement content from mutable commercial or master data after the payment event already happened.

## Current MVP rules
- One payment complement per `AccountsReceivablePayment`
- Complement preparation is explicit, never automatic
- All applications in one payment must belong to the same fiscal receiver
- All related invoices must have stamped UUID evidence already persisted
- Current MVP is `MXN`-only

## Installment number rule
`InstallmentNumber` is derived from persisted payment-application history for the related invoice:
1. load the invoice payment-application rows already stored locally
2. order them by persisted creation time, then sequence/id
3. find the current application row
4. use its 1-based position as the installment number

## Original invoice UUID source
The related-document UUID comes from persisted `FiscalStamp.Uuid`, never from:
- live SAT queries
- live PAC reconstruction
- live BillingDocument or SalesOrder reads

## Chosen FacturaloPlus payload mode
The MVP uses provider-specific JSON payload generation inside `Infrastructure.FacturaloPlus`.

Reasons:
- invoice stamping already follows the same adapter pattern
- the domain/application boundary remains provider-agnostic
- local XML generation is still deferred

## Persisted stamp evidence
`PaymentComplementStamp` stores:
- normalized provider status
- tracking id
- provider code/message
- UUID
- stamped timestamp
- XML content and XML hash
- original string
- QR text or URL
- redacted response summary

It does not store:
- raw secret values
- private key material
- raw request payloads containing secrets

Operational deployment and support guidance for the completed MVP is documented in [025-mvp-operational-hardening.md](./025-mvp-operational-hardening.md).

## Deferred items
- multi-currency complements
- payment-event regrouping and splitting
- payment reversals and adjustments
- automatic complement generation after application
- complement cancellation and latest-status refresh are covered in [024-payment-complement-cancellation-and-status-refresh.md](./024-payment-complement-cancellation-and-status-refresh.md)
