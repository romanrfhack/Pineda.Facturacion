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
