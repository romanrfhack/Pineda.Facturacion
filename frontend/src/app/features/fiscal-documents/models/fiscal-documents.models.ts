export interface IssuerProfileResponse {
  id: number;
  legalName: string;
  rfc: string;
  fiscalRegimeCode: string;
  postalCode: string;
  cfdiVersion: string;
  hasCertificateReference: boolean;
  hasPrivateKeyReference: boolean;
  hasPrivateKeyPasswordReference: boolean;
  pacEnvironment: string;
  isActive: boolean;
}

export interface FiscalReceiverSearchResponse {
  id: number;
  rfc: string;
  legalName: string;
  postalCode: string;
  fiscalRegimeCode: string;
  cfdiUseCodeDefault: string;
  isActive: boolean;
}

export interface BillingDocumentLookupResponse {
  billingDocumentId: number;
  salesOrderId: number;
  legacyOrderId: string;
  status: string;
  documentType: string;
  currencyCode: string;
  total: number;
  createdAtUtc: string;
  fiscalDocumentId?: number | null;
  fiscalDocumentStatus?: string | null;
}

export interface PrepareFiscalDocumentRequest {
  fiscalReceiverId: number;
  issuerProfileId?: number | null;
  paymentMethodSat: string;
  paymentFormSat: string;
  paymentCondition?: string | null;
  isCreditSale: boolean;
  creditDays?: number | null;
  receiverCfdiUseCode?: string | null;
  issuedAtUtc?: string | null;
}

export interface PrepareFiscalDocumentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  billingDocumentId: number;
  fiscalDocumentId?: number | null;
  status?: string | null;
}

export interface FiscalDocumentItemResponse {
  id: number;
  fiscalDocumentId: number;
  lineNumber: number;
  billingDocumentItemId?: number | null;
  internalCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  subtotal: number;
  taxTotal: number;
  total: number;
  satProductServiceCode: string;
  satUnitCode: string;
  taxObjectCode: string;
  vatRate: number;
  unitText?: string | null;
}

export interface FiscalDocumentResponse {
  id: number;
  billingDocumentId: number;
  issuerProfileId: number;
  fiscalReceiverId: number;
  status: string;
  cfdiVersion: string;
  documentType: string;
  issuedAtUtc: string;
  currencyCode: string;
  exchangeRate?: number | null;
  paymentMethodSat: string;
  paymentFormSat: string;
  paymentCondition?: string | null;
  isCreditSale: boolean;
  creditDays?: number | null;
  issuerRfc: string;
  issuerLegalName: string;
  issuerFiscalRegimeCode: string;
  issuerPostalCode: string;
  pacEnvironment: string;
  hasCertificateReference: boolean;
  hasPrivateKeyReference: boolean;
  hasPrivateKeyPasswordReference: boolean;
  receiverRfc: string;
  receiverLegalName: string;
  receiverFiscalRegimeCode: string;
  receiverCfdiUseCode: string;
  receiverPostalCode: string;
  receiverCountryCode?: string | null;
  receiverForeignTaxRegistration?: string | null;
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  total: number;
  items: FiscalDocumentItemResponse[];
}

export interface StampFiscalDocumentRequest {
  retryRejected: boolean;
}

export interface StampFiscalDocumentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  fiscalDocumentStatus?: string | null;
  fiscalStampId?: number | null;
  uuid?: string | null;
  stampedAtUtc?: string | null;
  providerName?: string | null;
  providerTrackingId?: string | null;
}

export interface FiscalStampResponse {
  id: number;
  fiscalDocumentId: number;
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
  xmlHash?: string | null;
  qrCodeTextOrUrl?: string | null;
  originalString?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CancelFiscalDocumentRequest {
  cancellationReasonCode: string;
  replacementUuid?: string | null;
}

export interface CancelFiscalDocumentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  fiscalDocumentStatus?: string | null;
  fiscalCancellationId?: number | null;
  cancellationStatus?: string | null;
  providerName?: string | null;
  providerTrackingId?: string | null;
  cancelledAtUtc?: string | null;
}

export interface FiscalCancellationResponse {
  fiscalDocumentId: number;
  status: string;
  cancellationReasonCode: string;
  replacementUuid?: string | null;
  providerName: string;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  requestedAtUtc: string;
  cancelledAtUtc?: string | null;
}

export interface RefreshFiscalDocumentStatusResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  fiscalDocumentStatus?: string | null;
  uuid?: string | null;
  lastKnownExternalStatus?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  checkedAtUtc?: string | null;
}
