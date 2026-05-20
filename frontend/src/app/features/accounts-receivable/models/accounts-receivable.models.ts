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
  receiverRfc?: string | null;
  receiverLegalName?: string | null;
  fiscalSeries?: string | null;
  fiscalFolio?: string | null;
  fiscalUuid?: string | null;
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
  agingBucket: string;
  hasPendingCommitment: boolean;
  nextCommitmentDateUtc?: string | null;
  nextFollowUpAtUtc?: string | null;
  followUpPending: boolean;
  collectionCommitments: CollectionCommitmentResponse[];
  collectionNotes: CollectionNoteResponse[];
  relatedPayments: AccountsReceivablePaymentResponse[];
  relatedPaymentComplements: AccountsReceivablePaymentComplementSummaryResponse[];
  timeline: AccountsReceivableTimelineEntryResponse[];
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
  currencyCode?: string | null;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  issuedAtUtc: string;
  dueAtUtc?: string | null;
  status: string;
  daysPastDue: number;
  agingBucket: string;
  hasPendingCommitment: boolean;
  nextCommitmentDateUtc?: string | null;
  nextFollowUpAtUtc?: string | null;
  followUpPending: boolean;
}

export interface AccountsReceivablePortfolioResponse {
  items: AccountsReceivablePortfolioItemResponse[];
}

export interface AccountsReceivableReceiverWorkspaceSummaryResponse {
  pendingBalanceTotal: number;
  overdueBalanceTotal: number;
  currentBalanceTotal: number;
  openInvoicesCount: number;
  overdueInvoicesCount: number;
  paymentsCount: number;
  paymentsWithUnappliedAmountCount: number;
  paymentsPendingRepCount: number;
  nextFollowUpAtUtc?: string | null;
  hasPendingCommitment: boolean;
  pendingCommitmentsCount: number;
  recentNotesCount: number;
  paymentsReadyToPrepareRepCount: number;
  paymentsPreparedRepCount: number;
  paymentsStampedRepCount: number;
}

export interface AccountsReceivableReceiverWorkspaceCommitmentResponse {
  id: number;
  accountsReceivableInvoiceId: number;
  promisedAmount: number;
  promisedDateUtc: string;
  status: string;
  notes?: string | null;
  createdAtUtc: string;
}

export interface AccountsReceivableReceiverWorkspaceNoteResponse {
  id: number;
  accountsReceivableInvoiceId: number;
  noteType: string;
  content: string;
  nextFollowUpAtUtc?: string | null;
  createdAtUtc: string;
  createdByUsername?: string | null;
}

export interface AccountsReceivableReceiverWorkspaceResponse {
  fiscalReceiverId: number;
  rfc: string;
  legalName: string;
  summary: AccountsReceivableReceiverWorkspaceSummaryResponse;
  invoices: AccountsReceivablePortfolioItemResponse[];
  payments: AccountsReceivablePaymentSummaryItemResponse[];
  pendingCommitments: AccountsReceivableReceiverWorkspaceCommitmentResponse[];
  recentNotes: AccountsReceivableReceiverWorkspaceNoteResponse[];
}

export interface SearchAccountsReceivablePortfolioRequest {
  fiscalReceiverId?: number | null;
  receiverQuery?: string | null;
  status?: string | null;
  dueDateFrom?: string | null;
  dueDateTo?: string | null;
  hasPendingBalance?: boolean | null;
  overdueOnly?: boolean | null;
  dueSoonOnly?: boolean | null;
  hasPendingCommitment?: boolean | null;
  followUpPending?: boolean | null;
}

export interface CreateCollectionCommitmentRequest {
  promisedAmount: number;
  promisedDateUtc: string;
  notes?: string | null;
}

export interface CollectionCommitmentResponse {
  id: number;
  accountsReceivableInvoiceId: number;
  promisedAmount: number;
  promisedDateUtc: string;
  status: string;
  notes?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  createdByUsername?: string | null;
}

export interface CreateCollectionCommitmentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  commitment?: CollectionCommitmentResponse | null;
}

export interface CollectionCommitmentsResponse {
  items: CollectionCommitmentResponse[];
}

export interface CreateCollectionNoteRequest {
  noteType: string;
  content: string;
  nextFollowUpAtUtc?: string | null;
}

export interface CollectionNoteResponse {
  id: number;
  accountsReceivableInvoiceId: number;
  noteType: string;
  content: string;
  nextFollowUpAtUtc?: string | null;
  createdAtUtc: string;
  createdByUsername?: string | null;
}

export interface AccountsReceivablePaymentComplementSummaryResponse {
  paymentComplementId: number;
  accountsReceivablePaymentId: number;
  status: string;
  totalPaymentsAmount: number;
  issuedAtUtc: string;
  paymentDateUtc: string;
  uuid?: string | null;
  stampedAtUtc?: string | null;
  cancelledAtUtc?: string | null;
}

export interface AccountsReceivableTimelineEntryResponse {
  atUtc: string;
  kind: string;
  title: string;
  description?: string | null;
  sourceType: string;
  sourceId: number;
  status?: string | null;
}

export interface CreateCollectionNoteResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  note?: CollectionNoteResponse | null;
}

export interface CollectionNotesResponse {
  items: CollectionNoteResponse[];
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
  accountsReceivableInvoiceId?: number | null;
  paymentDateUtc: string;
  paymentFormSat: string;
  amount: number;
  reference?: string | null;
  notes?: string | null;
  receivedFromFiscalReceiverId?: number | null;
}

export interface UpdateAccountsReceivablePaymentAmountRequest {
  amount: number;
}

export interface AccountsReceivablePaymentResponse {
  id: number;
  paymentDateUtc: string;
  paymentFormSat: string;
  currencyCode: string;
  amount: number;
  appliedTotal: number;
  remainingAmount: number;
  customerCreditBalanceAmount: number;
  reference?: string | null;
  notes?: string | null;
  receivedFromFiscalReceiverId?: number | null;
  operationalStatus: string;
  repStatus: string;
  readyToPrepareRep: boolean;
  repBlockReason?: string | null;
  unappliedDisposition: string;
  repDocumentStatus?: string | null;
  repReservedAmount: number;
  repFiscalizedAmount: number;
  applicationsCount: number;
  linkedFiscalDocumentId?: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  applications: AccountsReceivablePaymentApplicationResponse[];
}

export interface AccountsReceivablePaymentSummaryItemResponse {
  paymentId: number;
  receivedAtUtc: string;
  amount: number;
  appliedAmount: number;
  unappliedAmount: number;
  customerCreditBalanceAmount: number;
  currencyCode: string;
  reference?: string | null;
  payerName?: string | null;
  fiscalReceiverId?: number | null;
  operationalStatus: string;
  repStatus: string;
  readyToPrepareRep: boolean;
  repBlockReason?: string | null;
  unappliedDisposition: string;
  repDocumentStatus?: string | null;
  applicationsCount: number;
  linkedFiscalDocumentId?: number | null;
  repReservedAmount: number;
  repFiscalizedAmount: number;
}

export interface AccountsReceivablePaymentsResponse {
  items: AccountsReceivablePaymentSummaryItemResponse[];
}

export interface SearchAccountsReceivablePaymentsRequest {
  fiscalReceiverId?: number | null;
  operationalStatus?: string | null;
  receivedFrom?: string | null;
  receivedTo?: string | null;
  hasUnappliedAmount?: boolean | null;
  linkedFiscalDocumentId?: number | null;
}

export interface CreateAccountsReceivablePaymentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  payment?: AccountsReceivablePaymentResponse | null;
}

export interface UpdateAccountsReceivablePaymentAmountResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  accountsReceivablePaymentId: number;
  previousAmount: number;
  updatedAmount: number;
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

export interface SetAccountsReceivablePaymentUnappliedDispositionRequest {
  unappliedDisposition: string;
}

export interface SetAccountsReceivablePaymentUnappliedDispositionResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  accountsReceivablePaymentId: number;
}

export interface DeleteAccountsReceivablePaymentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  accountsReceivablePaymentId: number;
  deletedAmount: number;
  receivedFromFiscalReceiverId?: number | null;
}

export interface PreparePaymentComplementRequest {
  additionalPaymentIds?: number[];
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

export type ReceivablesSummaryScope =
  | 'all_pending'
  | 'overdue'
  | 'manual'
  | 'current_selection';

export type ReceivablesSummaryFormat = 'html' | 'html_with_pdf' | 'pdf';

export interface ReceivablesSummaryPartyResponse {
  id?: number | null;
  legalName: string;
  rfc: string;
  email?: string | null;
  fiscalRegimeCode?: string | null;
  postalCode?: string | null;
}

export interface ReceivablesSummaryCandidateResponse {
  accountsReceivableInvoiceId: number;
  fiscalDocumentId?: number | null;
  fiscalSeries?: string | null;
  fiscalFolio?: string | null;
  fiscalUuid?: string | null;
  issuedAtUtc: string;
  dueAtUtc?: string | null;
  daysPastDue: number;
  currencyCode: string;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  status: string;
  isOverdue: boolean;
  documentLink?: string | null;
}

export interface ReceivablesSummaryCandidatesResponse {
  receiver: ReceivablesSummaryPartyResponse;
  issuer: ReceivablesSummaryPartyResponse;
  defaultTo: string[];
  defaultSubject: string;
  defaultMessage: string;
  invoices: ReceivablesSummaryCandidateResponse[];
}

export interface ReceivablesSummaryIncludeOptionsRequest {
  invoiceTable: boolean;
  totalsByCurrency: boolean;
  highlightOverdue: boolean;
  paymentInstructions: boolean;
  receiverFiscalData: boolean;
  issuerData: boolean;
  invoiceLinks: boolean;
}

export interface ReceivablesSummaryRequest {
  receiverId: string;
  invoiceIds: number[];
  scope: ReceivablesSummaryScope;
  to: string[];
  cc: string[];
  bcc: string[];
  subject: string;
  message: string;
  format: ReceivablesSummaryFormat;
  includeOptions: ReceivablesSummaryIncludeOptionsRequest;
}

export interface ReceivablesSummaryTotalByCurrencyResponse {
  currencyCode: string;
  invoiceCount: number;
  total: number;
  paidTotal: number;
  outstandingBalance: number;
  overdueBalance: number;
  currentBalance: number;
}

export interface ReceivablesSummarySelectionResponse {
  invoiceCount: number;
  outstandingBalance: number;
  overdueBalance: number;
  currentBalance: number;
  totalsByCurrency: ReceivablesSummaryTotalByCurrencyResponse[];
}

export interface ReceivablesSummaryFinalResponse {
  to: string[];
  cc: string[];
  bcc: string[];
  subject: string;
  invoiceCount: number;
  format: string;
  attachedPdf: boolean;
  totalsByCurrency: ReceivablesSummaryTotalByCurrencyResponse[];
}

export interface ReceivablesSummaryPreviewResponse {
  outcome: string;
  success: boolean;
  errorMessage?: string | null;
  html?: string | null;
  pdfBase64?: string | null;
  pdfFileName?: string | null;
  summary?: ReceivablesSummarySelectionResponse | null;
  finalSummary?: ReceivablesSummaryFinalResponse | null;
}

export interface SendReceivablesSummaryResponse {
  success: boolean;
  outcome: string;
  errorMessage?: string | null;
  sentAt?: string | null;
  historyId?: string | null;
  emailProviderMessageId?: string | null;
  attachedPdf: boolean;
  summary?: ReceivablesSummarySelectionResponse | null;
}
