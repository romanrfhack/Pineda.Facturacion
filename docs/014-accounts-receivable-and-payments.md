# Accounts Receivable And Payments

## Purpose
Define the commercial receivable model needed for credit sales, payment tracking, and CFDI payment complements.

## Credit terms concept
Credit terms describe the commercial agreement under which a billing document may be paid after issuance.

Examples:
- cash / immediate payment
- net 15
- net 30
- split schedule

Recommended fields:
- `CreditTermId`
- `Code`
- `Description`
- `DueDays`
- `IsActive`
- `AllowsPartialPayments`

Rules:
- Credit terms are commercial rules, not PAC rules.
- A BillingDocument may reference credit terms without becoming a fiscal complement automatically.

## Payment record
A payment record represents a commercial payment event captured locally.

Recommended fields:
- `PaymentRecordId`
- `PaymentDateUtc`
- `Amount`
- `CurrencyCode`
- `ExchangeRate`
- `PaymentMethod`
- `Reference`
- `PayerName`
- `ReceiverAccountReference`
- `Notes`
- `Status`

Rules:
- A payment record can exist before full fiscal complement generation.
- Payment records are local source-of-truth commercial events.

## Payment application
A payment application links one payment record to one or more BillingDocuments.

Recommended fields:
- `PaymentApplicationId`
- `PaymentRecordId`
- `BillingDocumentId`
- `AppliedAmount`
- `ApplicationDateUtc`
- `SequenceNumber`

Rules:
- One payment can apply to multiple documents.
- One document can receive multiple partial payment applications.
- Applications, not payment headers alone, drive outstanding balance and complement eligibility.

## Outstanding balance concept
Outstanding balance is derived, not manually typed.

Formula:
- `OutstandingBalance = BillingDocument.Total - Sum(AppliedAmount for active payment applications)`

Rules:
- Outstanding balance must be computed from local payment applications.
- A “paid” flag alone is insufficient for fiscal complement logic.
- Reversals or voided applications must be excluded from the active applied sum.

## Payment complement generation strategy
Payment complements must be triggered from payment events and applications.

Recommended flow:
1. A commercial payment is recorded locally.
2. One or more payment applications are created against BillingDocuments.
3. The system evaluates whether the applied payment event requires a CFDI payment complement.
4. A persisted fiscal payment-complement snapshot is generated from:
   - issuer profile
   - receiver master data
   - payment record
   - payment applications
   - related stamped invoice identities
5. PAC integration consumes that persisted complement snapshot.

Rules:
- Complement generation must not wait only for “fully paid” status.
- Partial payments can generate complements.
- Multiple complements over time may apply to the same original invoice.

## Commercial payment tracking vs fiscal payment complement

### Commercial payment tracking
- Used for receivable control
- Can exist for cash, transfer, card, or internal reconciliation flows
- Tracks balances, due dates, and applications

### Fiscal payment complement
- Fiscal document emitted when payment events require CFDI complement behavior
- Depends on stamped original invoice references and payment applications
- Is not a substitute for the commercial AR ledger

## Recommended boundaries
- AR logic belongs in local Application + BillingWrite layers
- Payment complement generation belongs in fiscal Application use cases
- PAC adapter should only consume normalized complement snapshots

## Immediate implementation direction
- Model credit terms, payment records, and payment applications before complement generation
- Keep complement generation event-driven from payment applications
- Preserve a clear boundary between AR events and PAC submission
- Current MVP AR implementation creates receivable invoices only for stamped credit-sale `PPD` fiscal documents
- Payment applications persist previous/new balance snapshots per application row
- Payment complements remain deferred until a persisted complement snapshot model exists
