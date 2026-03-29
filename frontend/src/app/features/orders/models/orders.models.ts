export interface ImportLegacyOrderResponse {
  outcome: string;
  isSuccess: boolean;
  isIdempotent: boolean;
  errorMessage?: string | null;
  sourceSystem: string;
  sourceTable: string;
  legacyOrderId: string;
  sourceHash: string;
  legacyImportRecordId?: number | null;
  salesOrderId?: number | null;
  importStatus?: string | null;
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
