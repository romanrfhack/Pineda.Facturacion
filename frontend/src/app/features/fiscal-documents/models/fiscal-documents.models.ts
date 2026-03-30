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
  fiscalSeries?: string | null;
  nextFiscalFolio?: number | null;
  lastUsedFiscalFolio?: number | null;
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
  items?: BillingDocumentLookupItemResponse[];
  associatedOrders?: BillingDocumentAssociatedOrderResponse[];
}

export interface BillingDocumentLookupItemResponse {
  lineNumber: number;
  productInternalCode?: string | null;
  description: string;
}

export interface BillingDocumentAssociatedOrderResponse {
  salesOrderId: number;
  legacyOrderId: string;
  customerName: string;
  total: number;
  isPrimary: boolean;
}

export interface IssuedFiscalDocumentListItemResponse {
  fiscalDocumentId: number;
  billingDocumentId: number;
  status: string;
  issuedAtUtc: string;
  stampedAtUtc?: string | null;
  issuerRfc: string;
  issuerLegalName: string;
  series: string;
  folio: string;
  uuid?: string | null;
  receiverRfc: string;
  receiverLegalName: string;
  receiverCfdiUseCode: string;
  paymentMethodSat: string;
  paymentFormSat: string;
  documentType: string;
  total: number;
}

export interface IssuedFiscalDocumentListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: IssuedFiscalDocumentListItemResponse[];
}

export interface IssuedFiscalDocumentFilters {
  page: number;
  pageSize: number;
  fromDate?: string | null;
  toDate?: string | null;
  receiverRfc?: string | null;
  receiverName?: string | null;
  uuid?: string | null;
  series?: string | null;
  folio?: string | null;
  status?: string | null;
  query?: string | null;
  specialFieldCode?: string | null;
  specialFieldValue?: string | null;
}

export interface IssuedFiscalDocumentSpecialFieldOptionResponse {
  code: string;
  label: string;
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
  specialFields?: PrepareFiscalDocumentSpecialFieldValueRequest[];
}

export interface PrepareFiscalDocumentSpecialFieldValueRequest {
  fieldCode: string;
  value: string;
}

export interface PrepareFiscalDocumentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  billingDocumentId: number;
  fiscalDocumentId?: number | null;
  status?: string | null;
}

export interface UpdateBillingDocumentOrderAssociationResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  billingDocumentId: number;
  billingDocumentStatus?: string | null;
  salesOrderId: number;
  fiscalDocumentId?: number | null;
  fiscalDocumentStatus?: string | null;
  associatedOrderCount: number;
  total: number;
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
  series?: string | null;
  folio?: string | null;
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
  specialFields?: FiscalDocumentSpecialFieldValueResponse[];
  items: FiscalDocumentItemResponse[];
}

export interface FiscalDocumentSpecialFieldValueResponse {
  id: number;
  fiscalDocumentId: number;
  fiscalReceiverSpecialFieldDefinitionId: number;
  fieldCode: string;
  fieldLabelSnapshot: string;
  dataType: string;
  value: string;
  displayOrder: number;
  createdAtUtc: string;
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

export interface FiscalDocumentEmailDraftResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  defaultRecipientEmail?: string | null;
  suggestedSubject?: string | null;
  suggestedBody?: string | null;
}

export interface SendFiscalDocumentEmailRequest {
  recipients: string[];
  subject?: string | null;
  body?: string | null;
}

export interface SendFiscalDocumentEmailResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  recipients: string[];
  sentAtUtc?: string | null;
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
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  rawResponseSummaryJson?: string | null;
  supportMessage?: string | null;
  cancelledAtUtc?: string | null;
}

export interface FiscalCancellationResponse {
  fiscalDocumentId: number;
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
  operationalStatus?: string | null;
  operationalMessage?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  checkedAtUtc?: string | null;
}
