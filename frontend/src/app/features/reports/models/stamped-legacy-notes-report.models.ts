export interface StampedLegacyNotesReportFilters {
  fromDate: string;
  toDate: string;
  page: number;
  pageSize: number;
  receiverSearch?: string | null;
  uuid?: string | null;
  series?: string | null;
  folio?: string | null;
  legacyOrderId?: string | null;
  legacyOrderNumber?: string | null;
}

export interface StampedLegacyNoteReportItem {
  stampedAtUtc: string;
  stampedAtLocalText: string;
  legacyOrderId: string;
  legacyOrderNumber?: string | null;
  billingDocumentId: number;
  fiscalDocumentId: number;
  series?: string | null;
  folio?: string | null;
  uuid: string;
  fiscalStatus: string;
  cancellationStatus?: string | null;
  receiverName: string;
  receiverRfc: string;
  cfdiTotal: number;
  noteAmountInCfdi: number;
  currencyCode: string;
  itemCount: number;
}

export interface StampedLegacyNotesReportResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: StampedLegacyNoteReportItem[];
}
