import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  CancelPaymentComplementResponse,
  ExternalRepBaseDocumentDetailResponse,
  ExternalRepBaseDocumentImportResponse,
  InternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentFilters,
  InternalRepBaseDocumentListResponse,
  PrepareInternalRepBaseDocumentPaymentComplementRequest,
  PrepareInternalRepBaseDocumentPaymentComplementResponse,
  RegisterInternalRepBaseDocumentPaymentRequest,
  RegisterInternalRepBaseDocumentPaymentResponse,
  StampInternalRepBaseDocumentPaymentComplementRequest,
  StampInternalRepBaseDocumentPaymentComplementResponse,
  PaymentComplementCancellationResponse,
  PaymentComplementDocumentResponse,
  PaymentComplementStampResponse,
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

    return this.http.get<InternalRepBaseDocumentListResponse>(buildApiUrl(`/payment-complements/base-documents/internal?${query.toString()}`));
  }

  getInternalBaseDocumentByFiscalDocumentId(fiscalDocumentId: number): Observable<InternalRepBaseDocumentDetailResponse> {
    return this.http.get<InternalRepBaseDocumentDetailResponse>(buildApiUrl(`/payment-complements/base-documents/internal/${fiscalDocumentId}`));
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
