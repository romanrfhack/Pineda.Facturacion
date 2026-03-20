import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  CancelPaymentComplementResponse,
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
