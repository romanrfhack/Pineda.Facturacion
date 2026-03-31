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
