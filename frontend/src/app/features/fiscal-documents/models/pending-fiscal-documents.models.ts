export type PendingFiscalDocumentWorkFilter =
  | 'All'
  | 'PendingPreparation'
  | 'ReadyForStamping'
  | 'RequiresAttention';

export type PendingFiscalDocumentSort = 'LastActivityDesc' | 'OldestFirst' | 'TotalDesc';

export interface PendingFiscalDocumentSearchRequest {
  page: number;
  pageSize: number;
  query?: string | null;
  workStatus: PendingFiscalDocumentWorkFilter;
  sort: PendingFiscalDocumentSort;
}

export interface PendingFiscalDocumentListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: PendingFiscalDocumentListItemResponse[];
}

export interface PendingFiscalDocumentListItemResponse {
  billingDocumentId: number;
  fiscalDocumentId?: number | null;
  billingDocumentStatus: string;
  fiscalDocumentStatus?: string | null;
  workStatus: string;
  workStatusLabel: string;
  requiresAttention: boolean;
  documentType: string;
  series?: string | null;
  folio?: string | null;
  receiverName: string;
  receiverRfc?: string | null;
  currencyCode: string;
  total: number;
  associatedOrderCount: number;
  itemCount: number;
  orderReferences: string[];
  createdAtUtc: string;
  lastActivityAtUtc: string;
  fiscalPreparedAtUtc?: string | null;
  paymentMethodSat?: string | null;
  paymentFormSat?: string | null;
}
