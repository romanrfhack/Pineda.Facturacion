import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  BillingDocumentLookupResponse,
  AssignPendingBillingItemsRequest,
  AssignPendingBillingItemsResponse,
  CancelFiscalDocumentRequest,
  CancelFiscalDocumentResponse,
  FiscalCancellationResponse,
  FiscalDocumentResponse,
  IssuedFiscalDocumentFilters,
  IssuedFiscalDocumentListResponse,
  FiscalReceiverSearchResponse,
  FiscalStampResponse,
  QueryRemoteFiscalStampResponse,
  FiscalDocumentEmailDraftResponse,
  IssuerProfileResponse,
  IssuedFiscalDocumentSpecialFieldOptionResponse,
  PrepareFiscalDocumentRequest,
  PrepareFiscalDocumentResponse,
  PendingBillingItemResponse,
  PendingCancellationAuthorizationsResponse,
  RemoveBillingDocumentItemRequest,
  RemoveBillingDocumentItemResponse,
  RespondCancellationAuthorizationRequest,
  RespondCancellationAuthorizationResponse,
  RefreshFiscalDocumentStatusResponse,
  SendFiscalDocumentEmailRequest,
  SendFiscalDocumentEmailResponse,
  UpdateBillingDocumentOrderAssociationResponse,
  StampFiscalDocumentRequest,
  StampFiscalDocumentResponse
} from '../models/fiscal-documents.models';

@Injectable({ providedIn: 'root' })
export class FiscalDocumentsApiService {
  private readonly http = inject(HttpClient);

  getActiveIssuer(): Observable<IssuerProfileResponse> {
    return this.http.get<IssuerProfileResponse>(buildApiUrl('/fiscal/issuer-profile/active'));
  }

  searchReceivers(query: string): Observable<FiscalReceiverSearchResponse[]> {
    return this.http.get<FiscalReceiverSearchResponse[]>(buildApiUrl(`/fiscal/receivers/search?q=${encodeURIComponent(query)}`));
  }

  getBillingDocumentById(billingDocumentId: number): Observable<BillingDocumentLookupResponse> {
    return this.http.get<BillingDocumentLookupResponse>(buildApiUrl(`/billing-documents/${billingDocumentId}`));
  }

  searchBillingDocuments(query: string): Observable<BillingDocumentLookupResponse[]> {
    return this.http.get<BillingDocumentLookupResponse[]>(buildApiUrl(`/billing-documents/search?q=${encodeURIComponent(query)}`));
  }

  searchIssued(filters: IssuedFiscalDocumentFilters): Observable<IssuedFiscalDocumentListResponse> {
    const query = new URLSearchParams();
    query.set('page', `${filters.page}`);
    query.set('pageSize', `${filters.pageSize}`);

    setOptionalQuery(query, 'fromDate', filters.fromDate);
    setOptionalQuery(query, 'toDate', filters.toDate);
    setOptionalQuery(query, 'receiverRfc', filters.receiverRfc);
    setOptionalQuery(query, 'receiverName', filters.receiverName);
    setOptionalQuery(query, 'uuid', filters.uuid);
    setOptionalQuery(query, 'series', filters.series);
    setOptionalQuery(query, 'folio', filters.folio);
    setOptionalQuery(query, 'status', filters.status);
    setOptionalQuery(query, 'query', filters.query);
    setOptionalQuery(query, 'specialFieldCode', filters.specialFieldCode);
    setOptionalQuery(query, 'specialFieldValue', filters.specialFieldValue);

    return this.http.get<IssuedFiscalDocumentListResponse>(buildApiUrl(`/fiscal-documents/issued?${query.toString()}`));
  }

  getIssuedSpecialFieldOptions(): Observable<IssuedFiscalDocumentSpecialFieldOptionResponse[]> {
    return this.http.get<IssuedFiscalDocumentSpecialFieldOptionResponse[]>(buildApiUrl('/fiscal-documents/issued/special-fields'));
  }

  prepareFiscalDocument(billingDocumentId: number, request: PrepareFiscalDocumentRequest): Observable<PrepareFiscalDocumentResponse> {
    return this.http.post<PrepareFiscalDocumentResponse>(buildApiUrl(`/billing-documents/${billingDocumentId}/fiscal-documents`), request);
  }

  addSalesOrderToBillingDocument(billingDocumentId: number, salesOrderId: number): Observable<UpdateBillingDocumentOrderAssociationResponse> {
    return this.http.post<UpdateBillingDocumentOrderAssociationResponse>(
      buildApiUrl(`/billing-documents/${billingDocumentId}/sales-orders/${salesOrderId}`),
      {});
  }

  removeSalesOrderFromBillingDocument(billingDocumentId: number, salesOrderId: number): Observable<UpdateBillingDocumentOrderAssociationResponse> {
    return this.http.delete<UpdateBillingDocumentOrderAssociationResponse>(
      buildApiUrl(`/billing-documents/${billingDocumentId}/sales-orders/${salesOrderId}`));
  }

  removeBillingDocumentItem(billingDocumentId: number, billingDocumentItemId: number, request: RemoveBillingDocumentItemRequest): Observable<RemoveBillingDocumentItemResponse> {
    return this.http.post<RemoveBillingDocumentItemResponse>(
      buildApiUrl(`/billing-documents/${billingDocumentId}/items/${billingDocumentItemId}/remove`),
      request);
  }

  listPendingBillingItems(): Observable<PendingBillingItemResponse[]> {
    return this.http.get<PendingBillingItemResponse[]>(buildApiUrl('/billing-documents/pending-items'));
  }

  assignPendingBillingItems(billingDocumentId: number, request: AssignPendingBillingItemsRequest): Observable<AssignPendingBillingItemsResponse> {
    return this.http.post<AssignPendingBillingItemsResponse>(
      buildApiUrl(`/billing-documents/${billingDocumentId}/pending-items/assign`),
      request);
  }

  getFiscalDocumentById(fiscalDocumentId: number): Observable<FiscalDocumentResponse> {
    return this.http.get<FiscalDocumentResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}`));
  }

  stampFiscalDocument(fiscalDocumentId: number, request: StampFiscalDocumentRequest): Observable<StampFiscalDocumentResponse> {
    return this.http.post<StampFiscalDocumentResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp`), request);
  }

  getStamp(fiscalDocumentId: number): Observable<FiscalStampResponse> {
    return this.http.get<FiscalStampResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp`));
  }

  queryRemoteStamp(fiscalDocumentId: number): Observable<QueryRemoteFiscalStampResponse> {
    return this.http.post<QueryRemoteFiscalStampResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp/remote-query`), {});
  }

  getStampXml(fiscalDocumentId: number): Observable<string> {
    return this.http.get(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp/xml`), { responseType: 'text' });
  }

  getStampXmlFile(fiscalDocumentId: number): Observable<Blob> {
    return this.http.get(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp/xml`), { responseType: 'blob' });
  }

  getStampPdf(fiscalDocumentId: number): Observable<Blob> {
    return this.http.get(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp/pdf`), { responseType: 'blob' });
  }

  getEmailDraft(fiscalDocumentId: number): Observable<FiscalDocumentEmailDraftResponse> {
    return this.http.get<FiscalDocumentEmailDraftResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/email-draft`));
  }

  sendByEmail(fiscalDocumentId: number, request: SendFiscalDocumentEmailRequest): Observable<SendFiscalDocumentEmailResponse> {
    return this.http.post<SendFiscalDocumentEmailResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/email`), request);
  }

  cancelFiscalDocument(fiscalDocumentId: number, request: CancelFiscalDocumentRequest): Observable<CancelFiscalDocumentResponse> {
    return this.http.post<CancelFiscalDocumentResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/cancel`), request);
  }

  getCancellation(fiscalDocumentId: number): Observable<FiscalCancellationResponse> {
    return this.http.get<FiscalCancellationResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/cancellation`));
  }

  listPendingCancellationAuthorizations(): Observable<PendingCancellationAuthorizationsResponse> {
    return this.http.get<PendingCancellationAuthorizationsResponse>(buildApiUrl('/fiscal-documents/cancellation-authorizations/pending'));
  }

  respondCancellationAuthorization(request: RespondCancellationAuthorizationRequest): Observable<RespondCancellationAuthorizationResponse> {
    return this.http.post<RespondCancellationAuthorizationResponse>(
      buildApiUrl('/fiscal-documents/cancellation-authorizations/respond'),
      request);
  }

  refreshStatus(fiscalDocumentId: number): Observable<RefreshFiscalDocumentStatusResponse> {
    return this.http.post<RefreshFiscalDocumentStatusResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/refresh-status`), {});
  }
}

function setOptionalQuery(query: URLSearchParams, key: string, value?: string | null): void {
  if (!value || !value.trim()) {
    return;
  }

  query.set(key, value.trim());
}
