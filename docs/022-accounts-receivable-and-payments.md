# Accounts Receivable And Payments Foundation

## Scope
This phase adds the local accounts-receivable and payment-application ledger required before CFDI payment complements can be prepared.

This phase does not include:
- payment complement fiscal snapshot generation
- PAC complement submission
- negative adjustments
- credit notes

## Why the payment ledger is required first
CFDI payment complements must be driven from persisted payment events and persisted payment applications.

Without a local ledger, the system cannot reliably answer:
- how much was actually paid
- when the payment happened
- which stamped invoices the payment was applied to
- what the previous and resulting balances were at each application step

## Model

### AccountsReceivableInvoice
- Created from persisted `FiscalDocument` + `FiscalStamp` evidence only
- Represents the collectible balance for one stamped invoice
- Current MVP creates AR invoices only for credit-sale `PPD` fiscal documents

### AccountsReceivablePayment
- Represents one local payment event
- Stored independently from invoice application
- One payment can later apply to one or many AR invoices

### AccountsReceivablePaymentApplication
- Represents one allocation of a payment to one AR invoice
- Persists:
  - application sequence
  - applied amount
  - previous balance
  - new balance

## Balance semantics
- `PaidTotal` is the sum of applied payment amounts persisted locally
- `OutstandingBalance = Total - PaidTotal`
- Balance is not inferred from ad hoc flags
- Over-application is blocked in the current MVP

## PPD / credit-sale relationship
- AR invoice creation is limited to stamped `FiscalDocument` records where:
  - `IsCreditSale = true`
  - `PaymentMethodSat = PPD`
- Cash / `PUE` documents do not create AR invoices in this MVP slice
- This keeps the complement path explicit and avoids mixing cash invoices into the receivable ledger

## Partial payment rules
- A payment amount must be greater than zero
- An applied amount must be greater than zero
- One payment can apply to many invoices
- One invoice can receive many payments over time
- An application cannot exceed:
  - the remaining unapplied payment amount
  - the invoice outstanding balance

## Status semantics
- `Open`: outstanding balance equals total
- `PartiallyPaid`: outstanding balance is greater than zero and less than total
- `Paid`: outstanding balance equals zero
- `Cancelled`: reserved for later synchronization with fiscal cancellation lifecycle
- `Overpaid`: reserved in the enum but not used in the current MVP because over-application is blocked

## Complement handoff
The next phase should generate persisted payment-complement fiscal snapshots from:
- issuer fiscal snapshot data
- receiver fiscal snapshot data
- payment ledger events
- payment applications
- original stamped invoice UUIDs

No complement should be built directly from a “fully paid” flag alone.

Current implemented complement-preparation rule:
- one payment complement per payment event
- only when all related applied invoices belong to the same receiver
