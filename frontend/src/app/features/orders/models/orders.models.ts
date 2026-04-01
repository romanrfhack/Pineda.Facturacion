export type ImportLegacyOrderAllowedAction =
  | 'view_existing_sales_order'
  | 'view_existing_billing_document'
  | 'view_existing_fiscal_document'
  | 'preview_reimport'
  | 'reimport_not_available'
  | 'reimport_preview_not_available_yet';

export type ImportLegacyOrderPreviewChangeType = 'Added' | 'Removed' | 'Modified';

export type ImportLegacyOrderReimportEligibilityStatus =
  | 'Allowed'
  | 'BlockedByStampedFiscalDocument'
  | 'BlockedByProtectedState'
  | 'NotNeededNoChanges'
  | 'NotAvailableYet';

export type ImportLegacyOrderReimportReasonCode =
  | 'None'
  | 'FiscalDocumentStamped'
  | 'ProtectedDocumentState'
  | 'NoChangesDetected'
  | 'PreviewOnly';

export interface ImportLegacyOrderResponse {
  outcome: string;
  isSuccess: boolean;
  isIdempotent: boolean;
  errorMessage?: string | null;
  errorCode?: string | null;
  sourceSystem: string;
  sourceTable: string;
  legacyOrderId: string;
  sourceHash: string;
  legacyImportRecordId?: number | null;
  salesOrderId?: number | null;
  importStatus?: string | null;
  existingSalesOrderId?: number | null;
  existingSalesOrderStatus?: string | null;
  existingBillingDocumentId?: number | null;
  existingBillingDocumentStatus?: string | null;
  existingFiscalDocumentId?: number | null;
  existingFiscalDocumentStatus?: string | null;
  fiscalUuid?: string | null;
  importedAtUtc?: string | null;
  existingSourceHash?: string | null;
  currentSourceHash?: string | null;
  currentRevisionNumber?: number;
  allowedActions?: ImportLegacyOrderAllowedAction[];
}

export interface LegacyOrderListItem {
  legacyOrderId: string;
  orderDateUtc: string;
  customerName: string;
  total: number;
  legacyOrderType?: string | null;
  isImported: boolean;
  salesOrderId?: number | null;
  billingDocumentId?: number | null;
  billingDocumentStatus?: string | null;
  fiscalDocumentId?: number | null;
  fiscalDocumentStatus?: string | null;
  importStatus?: string | null;
}

export interface SearchLegacyOrdersRequest {
  fromDate: string;
  toDate: string;
  customerQuery?: string | null;
  page: number;
  pageSize: number;
}

export interface SearchLegacyOrdersResponse {
  isSuccess: boolean;
  errorMessage?: string | null;
  items: LegacyOrderListItem[];
  totalCount: number;
  totalPages: number;
  page: number;
  pageSize: number;
}

export interface CreateBillingDocumentRequest {
  documentType: string;
}

export interface CreateBillingDocumentResponse {
  outcome: string;
  isSuccess: boolean;
  errorMessage?: string | null;
  salesOrderId: number;
  billingDocumentId?: number | null;
  billingDocumentStatus?: string | null;
}

export interface ImportLegacyOrderPreviewResponse {
  isSuccess: boolean;
  errorMessage?: string | null;
  legacyOrderId: string;
  existingSalesOrderId?: number | null;
  existingSalesOrderStatus?: string | null;
  existingBillingDocumentId?: number | null;
  existingBillingDocumentStatus?: string | null;
  existingFiscalDocumentId?: number | null;
  existingFiscalDocumentStatus?: string | null;
  fiscalUuid?: string | null;
  existingSourceHash: string;
  currentSourceHash: string;
  currentRevisionNumber: number;
  hasChanges: boolean;
  changedOrderFields: string[];
  changeSummary: ImportLegacyOrderPreviewChangeSummary;
  lineChanges: ImportLegacyOrderPreviewLineChange[];
  reimportEligibility: ImportLegacyOrderPreviewEligibility;
  allowedActions: ImportLegacyOrderAllowedAction[];
}

export interface ImportLegacyOrderPreviewChangeSummary {
  addedLines: number;
  removedLines: number;
  modifiedLines: number;
  unchangedLines: number;
  oldSubtotal: number;
  newSubtotal: number;
  oldTotal: number;
  newTotal: number;
}

export interface ImportLegacyOrderPreviewLineChange {
  changeType: ImportLegacyOrderPreviewChangeType;
  matchKey: string;
  oldLine?: ImportLegacyOrderPreviewLine | null;
  newLine?: ImportLegacyOrderPreviewLine | null;
  changedFields: string[];
}

export interface ImportLegacyOrderPreviewLine {
  lineNumber: number;
  legacyArticleId: string;
  sku?: string | null;
  description: string;
  unitCode?: string | null;
  unitName?: string | null;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  taxAmount: number;
  lineTotal: number;
}

export interface ImportLegacyOrderPreviewEligibility {
  status: ImportLegacyOrderReimportEligibilityStatus;
  reasonCode: ImportLegacyOrderReimportReasonCode;
  reasonMessage: string;
}

export interface ReimportLegacyOrderRequest {
  expectedExistingSourceHash: string;
  expectedCurrentSourceHash: string;
  confirmationMode: 'ReplaceExistingImport';
}

export interface ReimportLegacyOrderResponse {
  outcome: string;
  isSuccess: boolean;
  errorCode?: string | null;
  errorMessage?: string | null;
  legacyOrderId: string;
  legacyImportRecordId?: number | null;
  salesOrderId?: number | null;
  salesOrderStatus?: string | null;
  billingDocumentId?: number | null;
  billingDocumentStatus?: string | null;
  fiscalDocumentId?: number | null;
  fiscalDocumentStatus?: string | null;
  fiscalUuid?: string | null;
  previousSourceHash: string;
  newSourceHash: string;
  currentRevisionNumber: number;
  reimportApplied: boolean;
  reimportMode: string;
  reimportEligibility: ImportLegacyOrderPreviewEligibility;
  allowedActions: ImportLegacyOrderAllowedAction[];
  warnings: string[];
}

export interface ImportLegacyOrderRevisionHistoryResponse {
  isSuccess: boolean;
  errorMessage?: string | null;
  legacyOrderId: string;
  currentRevisionNumber: number;
  revisions: ImportLegacyOrderRevision[];
}

export interface ImportLegacyOrderRevision {
  legacyOrderId: string;
  revisionNumber: number;
  previousRevisionNumber?: number | null;
  actionType: string;
  outcome: string;
  sourceHash: string;
  previousSourceHash?: string | null;
  appliedAtUtc: string;
  isCurrent: boolean;
  actorUserId?: number | null;
  actorUsername?: string | null;
  salesOrderId?: number | null;
  billingDocumentId?: number | null;
  fiscalDocumentId?: number | null;
  eligibilityStatus: string;
  eligibilityReasonCode: string;
  eligibilityReasonMessage: string;
  changeSummary: ImportLegacyOrderRevisionChangeSummary;
  snapshotJson?: string | null;
  diffJson?: string | null;
}

export interface ImportLegacyOrderRevisionChangeSummary {
  addedLines: number;
  removedLines: number;
  modifiedLines: number;
  unchangedLines: number;
  oldSubtotal: number;
  newSubtotal: number;
  oldTotal: number;
  newTotal: number;
}
