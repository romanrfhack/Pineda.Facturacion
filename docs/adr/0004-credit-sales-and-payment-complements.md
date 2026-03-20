# Title
Credit Sales And Payment Complements

## Status
Accepted

## Context
The roadmap must support credit terms, partial payments, and CFDI payment complements. A simple paid/unpaid flag is not enough for those flows.

## Decision
Model credit sales with local payment records and payment applications. Outstanding balance must be derived from applied payments. Payment complements must be driven by payment events and payment applications, not only by “fully paid” status.

## Consequences
- Partial payments are supported by design.
- Accounts receivable logic stays local and explicit.
- Complement generation has a deterministic source.
- Commercial payment tracking remains distinct from fiscal complement issuance.
