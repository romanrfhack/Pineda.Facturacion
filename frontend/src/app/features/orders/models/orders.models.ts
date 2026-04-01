export type ImportLegacyOrderAllowedAction =
  | 'view_existing_sales_order'
  | 'view_existing_billing_document'
  | 'view_existing_fiscal_document'
  | 'reimport_not_available'
  | 'reimport_preview_not_available_yet';

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
