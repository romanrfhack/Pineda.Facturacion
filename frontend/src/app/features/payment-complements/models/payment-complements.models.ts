export interface PaymentComplementRelatedDocumentResponse {
  id: number;
  accountsReceivableInvoiceId: number;
  fiscalDocumentId?: number | null;
  fiscalStampId?: number | null;
  externalRepBaseDocumentId?: number | null;
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
  alertCode?: string | null;
  severity?: string | null;
  nextRecommendedAction?: string | null;
  quickView?: string | null;
}

export interface RegisterInternalRepBaseDocumentPaymentRequest {
  paymentDate: string;
  paymentFormSat: string;
  amount: number;
  reference?: string | null;
  notes?: string | null;
}

export interface RegisterInternalRepBaseDocumentPaymentApplicationResponse {
  applicationId: number;
  accountsReceivablePaymentId: number;
  accountsReceivableInvoiceId: number;
  applicationSequence: number;
  appliedAmount: number;
  previousBalance: number;
  newBalance: number;
}

export interface RegisterInternalRepBaseDocumentPaymentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  warningMessages: string[];
  fiscalDocumentId: number;
  accountsReceivableInvoiceId?: number | null;
  accountsReceivablePaymentId?: number | null;
  appliedAmount: number;
  remainingBalance: number;
  remainingPaymentAmount: number;
  repOperationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  eligibilityReason?: string | null;
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
  applications: RegisterInternalRepBaseDocumentPaymentApplicationResponse[];
}

export interface RepOperationalAlertResponse {
  code: string;
  severity: string;
  message: string;
}

export interface RepOperationalCountResponse {
  code: string;
  count: number;
}

export interface RepOperationalSummaryCountsResponse {
  infoCount: number;
  warningCount: number;
  errorCount: number;
  criticalCount: number;
  blockedCount: number;
  alertCounts: RepOperationalCountResponse[];
  nextRecommendedActionCounts: RepOperationalCountResponse[];
  quickViewCounts: RepOperationalCountResponse[];
}

export interface RepBaseDocumentBulkRefreshDocumentRequest {
  sourceType?: string | null;
  sourceId: number;
}

export interface RepBaseDocumentBulkRefreshRequest {
  mode: string;
  documents?: RepBaseDocumentBulkRefreshDocumentRequest[];
  fromDate?: string | null;
  toDate?: string | null;
  receiverRfc?: string | null;
  query?: string | null;
  sourceType?: string | null;
  validationStatus?: string | null;
  eligible?: boolean | null;
  blocked?: boolean | null;
  withOutstandingBalance?: boolean | null;
  hasRepEmitted?: boolean | null;
  alertCode?: string | null;
  severity?: string | null;
  nextRecommendedAction?: string | null;
  quickView?: string | null;
}

export interface RepBaseDocumentBulkRefreshUpdatedStateResponse {
  operationalStatus: string;
  isEligible: boolean;
  isBlocked: boolean;
  primaryReasonMessage: string;
  nextRecommendedAction: string;
  alerts: RepOperationalAlertResponse[];
}

export interface RepBaseDocumentBulkRefreshItemResponse {
  sourceType: string;
  sourceId: number;
  attempted: boolean;
  outcome: string;
  message: string;
  paymentComplementDocumentId?: number | null;
  paymentComplementStatus?: string | null;
  lastKnownExternalStatus?: string | null;
  updatedState?: RepBaseDocumentBulkRefreshUpdatedStateResponse | null;
}

export interface RepBaseDocumentBulkRefreshResponse {
  isSuccess: boolean;
  errorMessage?: string | null;
  mode: string;
  maxDocuments: number;
  totalRequested: number;
  totalAttempted: number;
  refreshedCount: number;
  noChangesCount: number;
  blockedCount: number;
  failedCount: number;
  items: RepBaseDocumentBulkRefreshItemResponse[];
}

export interface PrepareInternalRepBaseDocumentPaymentComplementRequest {
  accountsReceivablePaymentId?: number | null;
}

export interface PrepareInternalRepBaseDocumentPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  warningMessages: string[];
  fiscalDocumentId: number;
  accountsReceivablePaymentId?: number | null;
  paymentComplementDocumentId?: number | null;
  status?: string | null;
  relatedDocumentCount: number;
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
}

export interface StampInternalRepBaseDocumentPaymentComplementRequest {
  paymentComplementDocumentId?: number | null;
  retryRejected?: boolean;
}

export interface StampInternalRepBaseDocumentPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  warningMessages: string[];
  fiscalDocumentId: number;
  accountsReceivablePaymentId?: number | null;
  paymentComplementDocumentId?: number | null;
  status?: string | null;
  paymentComplementStampId?: number | null;
  stampUuid?: string | null;
  stampedAtUtc?: string | null;
  xmlAvailable: boolean;
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
}

export interface RefreshInternalRepBaseDocumentPaymentComplementStatusRequest {
  paymentComplementDocumentId?: number | null;
}

export interface RefreshInternalRepBaseDocumentPaymentComplementStatusResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  paymentComplementDocumentId?: number | null;
  paymentComplementStatus?: string | null;
  uuid?: string | null;
  lastKnownExternalStatus?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  checkedAtUtc?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  repOperationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  eligibilityReason?: string | null;
  nextRecommendedAction?: string | null;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
}

export interface CancelInternalRepBaseDocumentPaymentComplementRequest {
  paymentComplementDocumentId?: number | null;
  cancellationReasonCode?: string;
  replacementUuid?: string | null;
}

export interface CancelInternalRepBaseDocumentPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  fiscalDocumentId: number;
  paymentComplementDocumentId?: number | null;
  paymentComplementStatus?: string | null;
  paymentComplementCancellationId?: number | null;
  cancellationStatus?: string | null;
  cancelledAtUtc?: string | null;
  providerName?: string | null;
  providerTrackingId?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  repOperationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  eligibilityReason?: string | null;
  nextRecommendedAction?: string | null;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
}

export interface ExternalRepBaseDocumentImportResponse {
  outcome: string;
  isSuccess: boolean;
  externalRepBaseDocumentId?: number | null;
  validationStatus: string;
  reasonCode: string;
  reasonMessage: string;
  errorMessage?: string | null;
  uuid?: string | null;
  issuerRfc?: string | null;
  receiverRfc?: string | null;
  paymentMethodSat?: string | null;
  paymentFormSat?: string | null;
  currencyCode?: string | null;
  total?: number | null;
  isDuplicate: boolean;
}

export interface ExternalRepBaseDocumentDetailResponse {
  summary: ExternalRepBaseDocumentItemResponse;
  timeline?: RepBaseDocumentTimelineEntryResponse[];
  paymentHistory: ExternalRepBaseDocumentPaymentHistoryResponse[];
  paymentApplications: ExternalRepBaseDocumentPaymentApplicationResponse[];
  issuedReps: ExternalRepBaseDocumentPaymentComplementResponse[];
}

export interface RepBaseDocumentTimelineEntryResponse {
  eventType: string;
  occurredAtUtc: string;
  sourceType: string;
  severity?: string | null;
  title: string;
  description: string;
  status?: string | null;
  referenceId?: number | null;
  referenceUuid?: string | null;
  metadata: Record<string, string | null>;
}

export interface ExternalRepBaseDocumentFilters {
  page: number;
  pageSize: number;
  fromDate?: string | null;
  toDate?: string | null;
  receiverRfc?: string | null;
  query?: string | null;
  validationStatus?: string | null;
  eligible?: boolean | null;
  blocked?: boolean | null;
  alertCode?: string | null;
  severity?: string | null;
  nextRecommendedAction?: string | null;
  quickView?: string | null;
}

export interface ExternalRepBaseDocumentItemResponse {
  externalRepBaseDocumentId: number;
  accountsReceivableInvoiceId?: number | null;
  uuid: string;
  cfdiVersion: string;
  documentType: string;
  series: string;
  folio: string;
  issuedAtUtc: string;
  issuerRfc: string;
  issuerLegalName?: string | null;
  receiverRfc: string;
  receiverLegalName?: string | null;
  currencyCode: string;
  exchangeRate: number;
  subtotal: number;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  paymentMethodSat: string;
  paymentFormSat: string;
  validationStatus: string;
  reasonCode: string;
  reasonMessage: string;
  satStatus: string;
  lastSatCheckAtUtc?: string | null;
  lastSatExternalStatus?: string | null;
  lastSatCancellationStatus?: string | null;
  lastSatProviderCode?: string | null;
  lastSatProviderMessage?: string | null;
  lastSatRawResponseSummaryJson?: string | null;
  importedAtUtc: string;
  importedByUserId?: number | null;
  importedByUsername?: string | null;
  sourceFileName: string;
  xmlHash: string;
  registeredPaymentCount: number;
  paymentComplementCount: number;
  stampedPaymentComplementCount: number;
  lastRepIssuedAtUtc?: string | null;
  operationalStatus: string;
  isEligible: boolean;
  isBlocked: boolean;
  primaryReasonCode: string;
  primaryReasonMessage: string;
  hasAppliedPaymentsWithoutStampedRep: boolean;
  hasPreparedRepPendingStamp: boolean;
  hasRepWithError: boolean;
  hasBlockedOperation: boolean;
  nextRecommendedAction: string;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
}

export interface ExternalRepBaseDocumentPaymentHistoryResponse {
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

export interface ExternalRepBaseDocumentPaymentApplicationResponse {
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

export interface ExternalRepBaseDocumentPaymentComplementResponse {
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

export interface RegisterExternalRepBaseDocumentPaymentRequest {
  paymentDate: string;
  paymentFormSat: string;
  amount: number;
  reference?: string | null;
  notes?: string | null;
}

export interface RegisterExternalRepBaseDocumentPaymentApplicationResponse {
  applicationId: number;
  accountsReceivablePaymentId: number;
  accountsReceivableInvoiceId: number;
  applicationSequence: number;
  appliedAmount: number;
  previousBalance: number;
  newBalance: number;
}

export interface RegisterExternalRepBaseDocumentPaymentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  warningMessages: string[];
  externalRepBaseDocumentId: number;
  accountsReceivableInvoiceId?: number | null;
  accountsReceivablePaymentId?: number | null;
  appliedAmount: number;
  remainingBalance: number;
  remainingPaymentAmount: number;
  repOperationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  eligibilityReason?: string | null;
  applications: RegisterExternalRepBaseDocumentPaymentApplicationResponse[];
}

export interface PrepareExternalRepBaseDocumentPaymentComplementRequest {
  accountsReceivablePaymentId?: number | null;
}

export interface PrepareExternalRepBaseDocumentPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  warningMessages: string[];
  externalRepBaseDocumentId: number;
  accountsReceivablePaymentId?: number | null;
  paymentComplementDocumentId?: number | null;
  status?: string | null;
  relatedDocumentCount: number;
  repOperationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  eligibilityReason?: string | null;
}

export interface StampExternalRepBaseDocumentPaymentComplementRequest {
  paymentComplementDocumentId?: number | null;
  retryRejected?: boolean;
}

export interface StampExternalRepBaseDocumentPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  warningMessages: string[];
  externalRepBaseDocumentId: number;
  accountsReceivablePaymentId?: number | null;
  paymentComplementDocumentId?: number | null;
  status?: string | null;
  paymentComplementStampId?: number | null;
  stampUuid?: string | null;
  stampedAtUtc?: string | null;
  xmlAvailable: boolean;
  repOperationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  eligibilityReason?: string | null;
}

export interface RefreshExternalRepBaseDocumentPaymentComplementStatusRequest {
  paymentComplementDocumentId?: number | null;
}

export interface RefreshExternalRepBaseDocumentPaymentComplementStatusResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  externalRepBaseDocumentId: number;
  paymentComplementDocumentId?: number | null;
  paymentComplementStatus?: string | null;
  uuid?: string | null;
  lastKnownExternalStatus?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  checkedAtUtc?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  operationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  primaryReasonMessage?: string | null;
  nextRecommendedAction?: string | null;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
}

export interface CancelExternalRepBaseDocumentPaymentComplementRequest {
  paymentComplementDocumentId?: number | null;
  cancellationReasonCode?: string;
  replacementUuid?: string | null;
}

export interface CancelExternalRepBaseDocumentPaymentComplementResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  externalRepBaseDocumentId: number;
  paymentComplementDocumentId?: number | null;
  paymentComplementStatus?: string | null;
  paymentComplementCancellationId?: number | null;
  cancellationStatus?: string | null;
  cancelledAtUtc?: string | null;
  providerName?: string | null;
  providerTrackingId?: string | null;
  providerCode?: string | null;
  providerMessage?: string | null;
  errorCode?: string | null;
  supportMessage?: string | null;
  rawResponseSummaryJson?: string | null;
  operationalStatus?: string | null;
  isEligible?: boolean | null;
  isBlocked?: boolean | null;
  primaryReasonMessage?: string | null;
  nextRecommendedAction?: string | null;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
}

export interface ExternalRepBaseDocumentListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: ExternalRepBaseDocumentItemResponse[];
  summaryCounts?: RepOperationalSummaryCountsResponse;
}

export interface RepBaseDocumentFilters {
  page: number;
  pageSize: number;
  fromDate?: string | null;
  toDate?: string | null;
  receiverRfc?: string | null;
  query?: string | null;
  sourceType?: string | null;
  validationStatus?: string | null;
  eligible?: boolean | null;
  blocked?: boolean | null;
  alertCode?: string | null;
  severity?: string | null;
  nextRecommendedAction?: string | null;
  quickView?: string | null;
}

export interface RepBaseDocumentItemResponse {
  sourceType: string;
  sourceId: number;
  fiscalDocumentId?: number | null;
  externalRepBaseDocumentId?: number | null;
  billingDocumentId?: number | null;
  uuid?: string | null;
  series: string;
  folio: string;
  issuedAtUtc: string;
  issuerRfc?: string | null;
  issuerLegalName?: string | null;
  receiverRfc: string;
  receiverLegalName: string;
  currencyCode: string;
  total: number;
  paymentMethodSat: string;
  paymentFormSat: string;
  operationalStatus: string;
  validationStatus?: string | null;
  satStatus?: string | null;
  outstandingBalance?: number | null;
  repCount?: number | null;
  isEligible: boolean;
  isBlocked: boolean;
  primaryReasonCode: string;
  primaryReasonMessage: string;
  hasAppliedPaymentsWithoutStampedRep: boolean;
  hasPreparedRepPendingStamp: boolean;
  hasRepWithError: boolean;
  hasBlockedOperation: boolean;
  nextRecommendedAction: string;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
  importedAtUtc?: string | null;
}

export interface RepBaseDocumentListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: RepBaseDocumentItemResponse[];
  summaryCounts?: RepOperationalSummaryCountsResponse;
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
  hasAppliedPaymentsWithoutStampedRep: boolean;
  hasPreparedRepPendingStamp: boolean;
  hasRepWithError: boolean;
  hasBlockedOperation: boolean;
  nextRecommendedAction: string;
  availableActions: string[];
  alerts: RepOperationalAlertResponse[];
  operationalState?: InternalRepBaseDocumentOperationalStateResponse | null;
}

export interface InternalRepBaseDocumentListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: InternalRepBaseDocumentItemResponse[];
  summaryCounts?: RepOperationalSummaryCountsResponse;
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
  timeline?: RepBaseDocumentTimelineEntryResponse[];
  paymentHistory: InternalRepBaseDocumentPaymentHistoryResponse[];
  paymentApplications: InternalRepBaseDocumentPaymentApplicationResponse[];
  issuedReps: InternalRepBaseDocumentPaymentComplementResponse[];
}
