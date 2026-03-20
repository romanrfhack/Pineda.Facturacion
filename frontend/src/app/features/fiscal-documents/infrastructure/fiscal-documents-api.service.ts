import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  CancelFiscalDocumentRequest,
  CancelFiscalDocumentResponse,
  FiscalCancellationResponse,
  FiscalDocumentResponse,
  FiscalReceiverSearchResponse,
  FiscalStampResponse,
  IssuerProfileResponse,
  PrepareFiscalDocumentRequest,
  PrepareFiscalDocumentResponse,
  RefreshFiscalDocumentStatusResponse,
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

  prepareFiscalDocument(billingDocumentId: number, request: PrepareFiscalDocumentRequest): Observable<PrepareFiscalDocumentResponse> {
    return this.http.post<PrepareFiscalDocumentResponse>(buildApiUrl(`/billing-documents/${billingDocumentId}/fiscal-documents`), request);
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

  getStampXml(fiscalDocumentId: number): Observable<string> {
    return this.http.get(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/stamp/xml`), { responseType: 'text' });
  }

  cancelFiscalDocument(fiscalDocumentId: number, request: CancelFiscalDocumentRequest): Observable<CancelFiscalDocumentResponse> {
    return this.http.post<CancelFiscalDocumentResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/cancel`), request);
  }

  getCancellation(fiscalDocumentId: number): Observable<FiscalCancellationResponse> {
    return this.http.get<FiscalCancellationResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/cancellation`));
  }

  refreshStatus(fiscalDocumentId: number): Observable<RefreshFiscalDocumentStatusResponse> {
    return this.http.post<RefreshFiscalDocumentStatusResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/refresh-status`), {});
  }
}
