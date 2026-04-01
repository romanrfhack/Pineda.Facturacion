import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  CreateBillingDocumentRequest,
  CreateBillingDocumentResponse,
  ImportLegacyOrderPreviewResponse,
  ImportLegacyOrderRevisionHistoryResponse,
  ImportLegacyOrderResponse,
  ReimportLegacyOrderRequest,
  ReimportLegacyOrderResponse,
  SearchLegacyOrdersRequest,
  SearchLegacyOrdersResponse
} from '../models/orders.models';

@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly http = inject(HttpClient);

  searchLegacyOrders(request: SearchLegacyOrdersRequest): Observable<SearchLegacyOrdersResponse> {
    const params: Record<string, string | number> = {
      fromDate: request.fromDate,
      toDate: request.toDate,
      page: request.page,
      pageSize: request.pageSize
    };

    if (request.customerQuery?.trim()) {
      params['customerQuery'] = request.customerQuery.trim();
    }

    return this.http.get<SearchLegacyOrdersResponse>(buildApiUrl('/orders/legacy'), {
      params
    });
  }

  importLegacyOrder(legacyOrderId: string): Observable<ImportLegacyOrderResponse> {
    return this.http.post<ImportLegacyOrderResponse>(buildApiUrl(`/orders/${legacyOrderId}/import`), {});
  }

  previewLegacyOrderImport(legacyOrderId: string): Observable<ImportLegacyOrderPreviewResponse> {
    return this.http.get<ImportLegacyOrderPreviewResponse>(buildApiUrl(`/orders/${legacyOrderId}/import-preview`));
  }

  listLegacyOrderImportRevisions(legacyOrderId: string): Observable<ImportLegacyOrderRevisionHistoryResponse> {
    return this.http.get<ImportLegacyOrderRevisionHistoryResponse>(buildApiUrl(`/orders/${legacyOrderId}/import-revisions`));
  }

  reimportLegacyOrder(legacyOrderId: string, request: ReimportLegacyOrderRequest): Observable<ReimportLegacyOrderResponse> {
    return this.http.post<ReimportLegacyOrderResponse>(buildApiUrl(`/orders/${legacyOrderId}/reimport`), request);
  }

  createBillingDocument(salesOrderId: number, request: CreateBillingDocumentRequest): Observable<CreateBillingDocumentResponse> {
    return this.http.post<CreateBillingDocumentResponse>(buildApiUrl(`/sales-orders/${salesOrderId}/billing-documents`), request);
  }
}
