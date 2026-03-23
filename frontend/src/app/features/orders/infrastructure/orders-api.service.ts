import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { CreateBillingDocumentRequest, CreateBillingDocumentResponse, ImportLegacyOrderResponse, SearchLegacyOrdersRequest, SearchLegacyOrdersResponse } from '../models/orders.models';

@Injectable({ providedIn: 'root' })
export class OrdersApiService {
  private readonly http = inject(HttpClient);

  searchLegacyOrders(request: SearchLegacyOrdersRequest): Observable<SearchLegacyOrdersResponse> {
    return this.http.get<SearchLegacyOrdersResponse>(buildApiUrl('/orders/legacy'), {
      params: {
        fromDate: request.fromDate,
        toDate: request.toDate,
        page: request.page,
        pageSize: request.pageSize
      }
    });
  }

  importLegacyOrder(legacyOrderId: string): Observable<ImportLegacyOrderResponse> {
    return this.http.post<ImportLegacyOrderResponse>(buildApiUrl(`/orders/${legacyOrderId}/import`), {});
  }

  createBillingDocument(salesOrderId: number, request: CreateBillingDocumentRequest): Observable<CreateBillingDocumentResponse> {
    return this.http.post<CreateBillingDocumentResponse>(buildApiUrl(`/sales-orders/${salesOrderId}/billing-documents`), request);
  }
}
