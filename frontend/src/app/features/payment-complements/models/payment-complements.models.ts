export interface PaymentComplementRelatedDocumentResponse {
  id: number;
  accountsReceivableInvoiceId: number;
  fiscalDocumentId: number;
  fiscalStampId: number;
  relatedDocumentUuid: string;
  installmentNumber: number;
  previousBalance: number;
  paidAmount: number;
  remainingBalance: number;
  currencyCode: string;
  createdAtUtc: string;
}

export interface PaymentComplementDocumentResponse {
  id: number;
  accountsReceivablePaymentId: number;
  status: string;
  providerName?: string | null;
  cfdiVersion: string;
  documentType: string;
  appliesToIncomePpdInvoices: boolean;
  eligibilitySummary: string;
  issuedAtUtc: string;
  paymentDateUtc: string;
  currencyCode: string;
  totalPaymentsAmount: number;
  issuerProfileId?: number | null;
  fiscalReceiverId?: number | null;
  issuerRfc: string;
  issuerLegalName: string;
  issuerFiscalRegimeCode: string;
  issuerPostalCode: string;
  receiverRfc: string;
  receiverLegalName: string;
  receiverFiscalRegimeCode: string;
  receiverPostalCode: string;
  receiverCountryCode?: string | null;
  receiverForeignTaxRegistration?: string | null;
  pacEnvironment: string;
  hasCertificateReference: boolean;
  hasPrivateKeyReference: boolean;
  hasPrivateKeyPasswordReference: boolean;
  relatedDocuments: PaymentComplementRelatedDocumentResponse[];
}

export interface StampPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  paymentComplementId: number;
  status?: string | null;
  paymentComplementStampId?: number | null;
  uuid?: string | null;
  stampedAtUtc?: string | null;
  providerName?: string | null;
  providerTrackingId?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
}

export interface PaymentComplementStampResponse {
  id: number;
  paymentComplementDocumentId: number;
  providerName: string;
  providerOperation?: string | null;
  providerTrackingId?: string | null;
  status: string;
  uuid?: string | null;
  stampedAtUtc?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  xmlHash?: string | null;
  qrCodeTextOrUrl?: string | null;
  originalString?: string | null;
  lastKnownExternalStatus?: string | null;
  lastStatusProviderCode?: string | null;
  lastStatusProviderMessage?: string | null;
  lastStatusSupportMessage?: string | null;
  lastStatusRawResponseSummaryJson?: string | null;
  lastStatusCheckAtUtc?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CancelPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  paymentComplementId: number;
  paymentComplementStatus?: string | null;
  paymentComplementCancellationId?: number | null;
  cancellationStatus?: string | null;
  providerName?: string | null;
  providerTrackingId?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  cancelledAtUtc?: string | null;
}

export interface PaymentComplementCancellationResponse {
  paymentComplementId: number;
  status: string;
  cancellationReasonCode: string;
  replacementUuid?: string | null;
  providerName: string;
  providerTrackingId?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  requestedAtUtc: string;
  cancelledAtUtc?: string | null;
}

export interface RefreshPaymentComplementStatusResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  paymentComplementId: number;
  paymentComplementStatus?: string | null;
  uuid?: string | null;
  lastKnownExternalStatus?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  checkedAtUtc?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
}

export interface InternalRepBaseDocumentFilters {
  page: number;
  pageSize: number;
  fromDate?: string | null;
  toDate?: string | null;
  receiverRfc?: string | null;
  query?: string | null;
  eligible?: boolean | null;
  blocked?: boolean | null;
  withOutstandingBalance?: boolean | null;
  hasRepEmitted?: boolean | null;
}

export interface InternalRepBaseDocumentItemResponse {
  fiscalDocumentId: number;
  billingDocumentId?: number | null;
  salesOrderId?: number | null;
  accountsReceivableInvoiceId?: number | null;
  fiscalStampId?: number | null;
  uuid?: string | null;
  series: string;
  folio: string;
  receiverRfc: string;
  receiverLegalName: string;
  issuedAtUtc: string;
  paymentMethodSat: string;
  paymentFormSat: string;
  currencyCode: string;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  fiscalStatus: string;
  accountsReceivableStatus?: string | null;
  repOperationalStatus: string;
  isEligible: boolean;
  isBlocked: boolean;
  eligibilityReason: string;
  eligibility: InternalRepBaseDocumentEligibilityExplanationResponse;
  registeredPaymentCount: number;
  paymentComplementCount: number;
  stampedPaymentComplementCount: number;
  lastRepIssuedAtUtc?: string | null;
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
}

export interface InternalRepBaseDocumentListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: InternalRepBaseDocumentItemResponse[];
}

export interface InternalRepBaseDocumentPaymentApplicationResponse {
  accountsReceivablePaymentId: number;
  applicationSequence: number;
  paymentDateUtc: string;
  paymentFormSat: string;
  appliedAmount: number;
  previousBalance: number;
  newBalance: number;
  reference?: string | null;
  notes?: string | null;
  paymentAmount: number;
  remainingPaymentAmount: number;
  createdAtUtc: string;
}

export interface InternalRepBaseDocumentPaymentComplementResponse {
  paymentComplementId: number;
  accountsReceivablePaymentId: number;
  status: string;
  uuid?: string | null;
  paymentDateUtc: string;
  issuedAtUtc?: string | null;
  stampedAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  providerName?: string | null;
  installmentNumber: number;
  previousBalance: number;
  paidAmount: number;
  remainingBalance: number;
}

export interface InternalRepBaseDocumentPaymentHistoryResponse {
  accountsReceivablePaymentId: number;
  paymentDateUtc: string;
  paymentFormSat: string;
  paymentAmount: number;
  amountAppliedToDocument: number;
  remainingPaymentAmount: number;
  reference?: string | null;
  notes?: string | null;
  paymentComplementId?: number | null;
  paymentComplementStatus?: string | null;
  paymentComplementUuid?: string | null;
  createdAtUtc: string;
}

export interface InternalRepBaseDocumentEligibilitySignalResponse {
  code: string;
  severity: string;
  message: string;
}

export interface InternalRepBaseDocumentEligibilityExplanationResponse {
  status: string;
  primaryReasonCode: string;
  primaryReasonMessage: string;
  evaluatedAtUtc: string;
  secondarySignals: InternalRepBaseDocumentEligibilitySignalResponse[];
}

export interface InternalRepBaseDocumentOperationalStateResponse {
  lastEligibilityEvaluatedAtUtc: string;
  lastEligibilityStatus: string;
  lastPrimaryReasonCode: string;
  lastPrimaryReasonMessage: string;
  repPendingFlag: boolean;
  lastRepIssuedAtUtc?: string | null;
  repCount: number;
  totalPaidApplied: number;
}

export interface InternalRepBaseDocumentDetailResponse {
  summary: InternalRepBaseDocumentItemResponse;
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
  paymentHistory: InternalRepBaseDocumentPaymentHistoryResponse[];
  paymentApplications: InternalRepBaseDocumentPaymentApplicationResponse[];
  issuedReps: InternalRepBaseDocumentPaymentComplementResponse[];
}
