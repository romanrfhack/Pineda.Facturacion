export interface AccountsReceivablePaymentApplicationResponse {
  id: number;
  accountsReceivablePaymentId: number;
  accountsReceivableInvoiceId: number;
  applicationSequence: number;
  appliedAmount: number;
  previousBalance: number;
  newBalance: number;
  createdAtUtc: string;
}

export interface AccountsReceivableInvoiceResponse {
  id: number;
  billingDocumentId: number;
  fiscalDocumentId: number;
  fiscalStampId: number;
  fiscalReceiverId?: number | null;
  status: string;
  paymentMethodSat: string;
  paymentFormSatInitial: string;
  isCreditSale: boolean;
  creditDays?: number | null;
  issuedAtUtc: string;
  dueAtUtc?: string | null;
  currencyCode: string;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  applications: AccountsReceivablePaymentApplicationResponse[];
}

export interface AccountsReceivablePortfolioItemResponse {
  accountsReceivableInvoiceId: number;
  fiscalDocumentId?: number | null;
  fiscalReceiverId?: number | null;
  receiverRfc?: string | null;
  receiverLegalName?: string | null;
  fiscalSeries?: string | null;
  fiscalFolio?: string | null;
  fiscalUuid?: string | null;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  issuedAtUtc: string;
  dueAtUtc?: string | null;
  status: string;
  daysPastDue: number;
}

export interface AccountsReceivablePortfolioResponse {
  items: AccountsReceivablePortfolioItemResponse[];
}

export interface SearchAccountsReceivablePortfolioRequest {
  fiscalReceiverId?: number | null;
  receiverQuery?: string | null;
  status?: string | null;
  dueDateFrom?: string | null;
  dueDateTo?: string | null;
  hasPendingBalance?: boolean | null;
}

export interface CreateAccountsReceivableInvoiceRequest {
  overrideCreditDays?: number | null;
}

export interface CreateAccountsReceivableInvoiceResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  accountsReceivableInvoice?: AccountsReceivableInvoiceResponse | null;
}

export interface CreateAccountsReceivablePaymentRequest {
  paymentDateUtc: string;
  paymentFormSat: string;
  amount: number;
  reference?: string | null;
  notes?: string | null;
}

export interface AccountsReceivablePaymentResponse {
  id: number;
  paymentDateUtc: string;
  paymentFormSat: string;
  currencyCode: string;
  amount: number;
  appliedTotal: number;
  remainingAmount: number;
  reference?: string | null;
  notes?: string | null;
  receivedFromFiscalReceiverId?: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  applications: AccountsReceivablePaymentApplicationResponse[];
}

export interface CreateAccountsReceivablePaymentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  payment?: AccountsReceivablePaymentResponse | null;
}

export interface ApplyAccountsReceivablePaymentRowRequest {
  accountsReceivableInvoiceId: number;
  appliedAmount: number;
}

export interface ApplyAccountsReceivablePaymentRequest {
  applications: ApplyAccountsReceivablePaymentRowRequest[];
}

export interface ApplyAccountsReceivablePaymentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  accountsReceivablePaymentId: number;
  appliedCount: number;
  remainingPaymentAmount: number;
  payment?: AccountsReceivablePaymentResponse | null;
  applications: AccountsReceivablePaymentApplicationResponse[];
}

export interface PreparePaymentComplementRequest {
  issuedAtUtc?: string | null;
}

export interface PreparePaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  accountsReceivablePaymentId: number;
  paymentComplementId?: number | null;
  status?: string | null;
}
