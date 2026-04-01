import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  CancelExternalRepBaseDocumentPaymentComplementRequest,
  CancelExternalRepBaseDocumentPaymentComplementResponse,
  CancelInternalRepBaseDocumentPaymentComplementRequest,
  CancelInternalRepBaseDocumentPaymentComplementResponse,
  CancelPaymentComplementResponse,
  ExternalRepBaseDocumentFilters,
  ExternalRepBaseDocumentDetailResponse,
  ExternalRepBaseDocumentImportResponse,
  ExternalRepBaseDocumentListResponse,
  InternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentFilters,
  InternalRepBaseDocumentListResponse,
  PrepareInternalRepBaseDocumentPaymentComplementRequest,
  PrepareInternalRepBaseDocumentPaymentComplementResponse,
  PrepareExternalRepBaseDocumentPaymentComplementRequest,
  PrepareExternalRepBaseDocumentPaymentComplementResponse,
  RepBaseDocumentBulkRefreshRequest,
  RepBaseDocumentBulkRefreshResponse,
  RegisterExternalRepBaseDocumentPaymentRequest,
  RegisterExternalRepBaseDocumentPaymentResponse,
  RegisterInternalRepBaseDocumentPaymentRequest,
  RegisterInternalRepBaseDocumentPaymentResponse,
  RepBaseDocumentFilters,
  RepBaseDocumentListResponse,
  StampExternalRepBaseDocumentPaymentComplementRequest,
  StampExternalRepBaseDocumentPaymentComplementResponse,
  StampInternalRepBaseDocumentPaymentComplementRequest,
  StampInternalRepBaseDocumentPaymentComplementResponse,
  PaymentComplementCancellationResponse,
  PaymentComplementDocumentResponse,
  PaymentComplementStampResponse,
  RefreshExternalRepBaseDocumentPaymentComplementStatusRequest,
  RefreshExternalRepBaseDocumentPaymentComplementStatusResponse,
  RefreshInternalRepBaseDocumentPaymentComplementStatusRequest,
  RefreshInternalRepBaseDocumentPaymentComplementStatusResponse,
  RefreshPaymentComplementStatusResponse,
  StampPaymentComplementResponse
} from '../models/payment-complements.models';

@Injectable({ providedIn: 'root' })
export class PaymentComplementsApiService {
  private readonly http = inject(HttpClient);

  getByPaymentId(paymentId: number): Observable<PaymentComplementDocumentResponse> {
    return this.http.get<PaymentComplementDocumentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/payment-complement`));
  }

  searchInternalBaseDocuments(filters: InternalRepBaseDocumentFilters): Observable<InternalRepBaseDocumentListResponse> {
    const query = new URLSearchParams();
    query.set('page', `${filters.page}`);
    query.set('pageSize', `${filters.pageSize}`);

    setOptionalQuery(query, 'fromDate', filters.fromDate);
    setOptionalQuery(query, 'toDate', filters.toDate);
    setOptionalQuery(query, 'receiverRfc', filters.receiverRfc);
    setOptionalQuery(query, 'query', filters.query);
    setOptionalBooleanQuery(query, 'eligible', filters.eligible);
    setOptionalBooleanQuery(query, 'blocked', filters.blocked);
    setOptionalBooleanQuery(query, 'withOutstandingBalance', filters.withOutstandingBalance);
    setOptionalBooleanQuery(query, 'hasRepEmitted', filters.hasRepEmitted);
    setOptionalQuery(query, 'alertCode', filters.alertCode);
    setOptionalQuery(query, 'severity', filters.severity);
    setOptionalQuery(query, 'nextRecommendedAction', filters.nextRecommendedAction);
    setOptionalQuery(query, 'quickView', filters.quickView);

    return this.http.get<InternalRepBaseDocumentListResponse>(buildApiUrl(`/payment-complements/base-documents/internal?${query.toString()}`));
  }

  getInternalBaseDocumentByFiscalDocumentId(fiscalDocumentId: number): Observable<InternalRepBaseDocumentDetailResponse> {
    return this.http.get<InternalRepBaseDocumentDetailResponse>(buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}`));
  }

  bulkRefreshInternalBaseDocuments(request: RepBaseDocumentBulkRefreshRequest): Observable<RepBaseDocumentBulkRefreshResponse> {
    return this.http.post<RepBaseDocumentBulkRefreshResponse>(
      buildApiUrl('/payment-complements/base-documents/internal/refresh-rep-status/bulk'),
      request
    );
  }

  searchExternalBaseDocuments(filters: ExternalRepBaseDocumentFilters): Observable<ExternalRepBaseDocumentListResponse> {
    const query = new URLSearchParams();
    query.set('page', `${filters.page}`);
    query.set('pageSize', `${filters.pageSize}`);

    setOptionalQuery(query, 'fromDate', filters.fromDate);
    setOptionalQuery(query, 'toDate', filters.toDate);
    setOptionalQuery(query, 'receiverRfc', filters.receiverRfc);
    setOptionalQuery(query, 'query', filters.query);
    setOptionalQuery(query, 'validationStatus', filters.validationStatus);
    setOptionalBooleanQuery(query, 'eligible', filters.eligible);
    setOptionalBooleanQuery(query, 'blocked', filters.blocked);
    setOptionalQuery(query, 'alertCode', filters.alertCode);
    setOptionalQuery(query, 'severity', filters.severity);
    setOptionalQuery(query, 'nextRecommendedAction', filters.nextRecommendedAction);
    setOptionalQuery(query, 'quickView', filters.quickView);

    return this.http.get<ExternalRepBaseDocumentListResponse>(buildApiUrl(`/payment-complements/base-documents/external?${query.toString()}`));
  }

  bulkRefreshExternalBaseDocuments(request: RepBaseDocumentBulkRefreshRequest): Observable<RepBaseDocumentBulkRefreshResponse> {
    return this.http.post<RepBaseDocumentBulkRefreshResponse>(
      buildApiUrl('/payment-complements/base-documents/external/refresh-rep-status/bulk'),
      request
    );
  }

  searchBaseDocuments(filters: RepBaseDocumentFilters): Observable<RepBaseDocumentListResponse> {
    const query = new URLSearchParams();
    query.set('page', `${filters.page}`);
    query.set('pageSize', `${filters.pageSize}`);

    setOptionalQuery(query, 'fromDate', filters.fromDate);
    setOptionalQuery(query, 'toDate', filters.toDate);
    setOptionalQuery(query, 'receiverRfc', filters.receiverRfc);
    setOptionalQuery(query, 'query', filters.query);
    setOptionalQuery(query, 'sourceType', filters.sourceType);
    setOptionalQuery(query, 'validationStatus', filters.validationStatus);
    setOptionalBooleanQuery(query, 'eligible', filters.eligible);
    setOptionalBooleanQuery(query, 'blocked', filters.blocked);
    setOptionalQuery(query, 'alertCode', filters.alertCode);
    setOptionalQuery(query, 'severity', filters.severity);
    setOptionalQuery(query, 'nextRecommendedAction', filters.nextRecommendedAction);
    setOptionalQuery(query, 'quickView', filters.quickView);

    return this.http.get<RepBaseDocumentListResponse>(buildApiUrl(`/payment-complements/base-documents?${query.toString()}`));
  }

  bulkRefreshBaseDocuments(request: RepBaseDocumentBulkRefreshRequest): Observable<RepBaseDocumentBulkRefreshResponse> {
    return this.http.post<RepBaseDocumentBulkRefreshResponse>(
      buildApiUrl('/payment-complements/base-documents/refresh-rep-status/bulk'),
      request
    );
  }

  registerInternalBaseDocumentPayment(
    fiscalDocumentId: number,
    request: RegisterInternalRepBaseDocumentPaymentRequest
  ): Observable<RegisterInternalRepBaseDocumentPaymentResponse> {
    return this.http.post<RegisterInternalRepBaseDocumentPaymentResponse>(
      buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}/payments`),
      request
    );
  }

  prepareInternalBaseDocumentPaymentComplement(
    fiscalDocumentId: number,
    request: PrepareInternalRepBaseDocumentPaymentComplementRequest
  ): Observable<PrepareInternalRepBaseDocumentPaymentComplementResponse> {
    return this.http.post<PrepareInternalRepBaseDocumentPaymentComplementResponse>(
      buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}/prepare`),
      request
    );
  }

  stampInternalBaseDocumentPaymentComplement(
    fiscalDocumentId: number,
    request: StampInternalRepBaseDocumentPaymentComplementRequest
  ): Observable<StampInternalRepBaseDocumentPaymentComplementResponse> {
    return this.http.post<StampInternalRepBaseDocumentPaymentComplementResponse>(
      buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}/stamp`),
      request
    );
  }

  refreshInternalBaseDocumentPaymentComplementStatus(
    fiscalDocumentId: number,
    request: RefreshInternalRepBaseDocumentPaymentComplementStatusRequest
  ): Observable<RefreshInternalRepBaseDocumentPaymentComplementStatusResponse> {
    return this.http.post<RefreshInternalRepBaseDocumentPaymentComplementStatusResponse>(
      buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}/refresh-rep-status`),
      request
    );
  }

  cancelInternalBaseDocumentPaymentComplement(
    fiscalDocumentId: number,
    request: CancelInternalRepBaseDocumentPaymentComplementRequest
  ): Observable<CancelInternalRepBaseDocumentPaymentComplementResponse> {
    return this.http.post<CancelInternalRepBaseDocumentPaymentComplementResponse>(
      buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}/cancel-rep`),
      request
    );
  }

  registerExternalBaseDocumentPayment(
    externalRepBaseDocumentId: number,
    request: RegisterExternalRepBaseDocumentPaymentRequest
  ): Observable<RegisterExternalRepBaseDocumentPaymentResponse> {
    return this.http.post<RegisterExternalRepBaseDocumentPaymentResponse>(
      buildApiUrl(`/payment-complements/base-documents/external/${externalRepBaseDocumentId}/payments`),
      request
    );
  }

  prepareExternalBaseDocumentPaymentComplement(
    externalRepBaseDocumentId: number,
    request: PrepareExternalRepBaseDocumentPaymentComplementRequest
  ): Observable<PrepareExternalRepBaseDocumentPaymentComplementResponse> {
    return this.http.post<PrepareExternalRepBaseDocumentPaymentComplementResponse>(
      buildApiUrl(`/payment-complements/base-documents/external/${externalRepBaseDocumentId}/prepare`),
      request
    );
  }

  stampExternalBaseDocumentPaymentComplement(
    externalRepBaseDocumentId: number,
    request: StampExternalRepBaseDocumentPaymentComplementRequest
  ): Observable<StampExternalRepBaseDocumentPaymentComplementResponse> {
    return this.http.post<StampExternalRepBaseDocumentPaymentComplementResponse>(
      buildApiUrl(`/payment-complements/base-documents/external/${externalRepBaseDocumentId}/stamp`),
      request
    );
  }

  refreshExternalBaseDocumentPaymentComplementStatus(
    externalRepBaseDocumentId: number,
    request: RefreshExternalRepBaseDocumentPaymentComplementStatusRequest
  ): Observable<RefreshExternalRepBaseDocumentPaymentComplementStatusResponse> {
    return this.http.post<RefreshExternalRepBaseDocumentPaymentComplementStatusResponse>(
      buildApiUrl(`/payment-complements/base-documents/external/${externalRepBaseDocumentId}/refresh-rep-status`),
      request
    );
  }

  cancelExternalBaseDocumentPaymentComplement(
    externalRepBaseDocumentId: number,
    request: CancelExternalRepBaseDocumentPaymentComplementRequest
  ): Observable<CancelExternalRepBaseDocumentPaymentComplementResponse> {
    return this.http.post<CancelExternalRepBaseDocumentPaymentComplementResponse>(
      buildApiUrl(`/payment-complements/base-documents/external/${externalRepBaseDocumentId}/cancel-rep`),
      request
    );
  }

  importExternalBaseDocumentXml(file: File): Observable<ExternalRepBaseDocumentImportResponse> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<ExternalRepBaseDocumentImportResponse>(
      buildApiUrl('/payment-complements/external-base-documents/import-xml'),
      form
    );
  }

  getExternalBaseDocumentById(externalRepBaseDocumentId: number): Observable<ExternalRepBaseDocumentDetailResponse> {
    return this.http.get<ExternalRepBaseDocumentDetailResponse>(
      buildApiUrl(`/payment-complements/external-base-documents/${externalRepBaseDocumentId}`)
    );
  }

  stamp(paymentComplementId: number): Observable<StampPaymentComplementResponse> {
    return this.http.post<StampPaymentComplementResponse>(buildApiUrl(`/payment-complements/${paymentComplementId}/stamp`), { retryRejected: false });
  }

  getStamp(paymentComplementId: number): Observable<PaymentComplementStampResponse> {
    return this.http.get<PaymentComplementStampResponse>(buildApiUrl(`/payment-complements/${paymentComplementId}/stamp`));
  }

  getStampXml(paymentComplementId: number): Observable<string> {
    return this.http.get(buildApiUrl(`/payment-complements/${paymentComplementId}/stamp/xml`), { responseType: 'text' });
  }

  cancel(paymentComplementId: number): Observable<CancelPaymentComplementResponse> {
    return this.http.post<CancelPaymentComplementResponse>(buildApiUrl(`/payment-complements/${paymentComplementId}/cancel`), { cancellationReasonCode: '02' });
  }

  getCancellation(paymentComplementId: number): Observable<PaymentComplementCancellationResponse> {
    return this.http.get<PaymentComplementCancellationResponse>(buildApiUrl(`/payment-complements/${paymentComplementId}/cancellation`));
  }

  refreshStatus(paymentComplementId: number): Observable<RefreshPaymentComplementStatusResponse> {
    return this.http.post<RefreshPaymentComplementStatusResponse>(buildApiUrl(`/payment-complements/${paymentComplementId}/refresh-status`), {});
  }
}

function setOptionalQuery(query: URLSearchParams, key: string, value?: string | null): void {
  if (!value || !value.trim()) {
    return;
  }

  query.set(key, value.trim());
}

function setOptionalBooleanQuery(query: URLSearchParams, key: string, value?: boolean | null): void {
  if (value === null || value === undefined) {
    return;
  }

  query.set(key, `${value}`);
}
