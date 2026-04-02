import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivablePaymentResponse,
  ApplyAccountsReceivablePaymentRequest,
  ApplyAccountsReceivablePaymentResponse,
  CreateAccountsReceivableInvoiceRequest,
  CreateAccountsReceivableInvoiceResponse,
  CreateAccountsReceivablePaymentRequest,
  CreateAccountsReceivablePaymentResponse,
  PreparePaymentComplementRequest,
  PreparePaymentComplementResponse
} from '../models/accounts-receivable.models';
import { PaymentComplementDocumentResponse } from '../../payment-complements/models/payment-complements.models';

@Injectable({ providedIn: 'root' })
export class AccountsReceivableApiService {
  private readonly http = inject(HttpClient);

  createInvoiceFromFiscalDocument(fiscalDocumentId: number, request: CreateAccountsReceivableInvoiceRequest = {}): Observable<CreateAccountsReceivableInvoiceResponse> {
    return this.http.post<CreateAccountsReceivableInvoiceResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/accounts-receivable`), request);
  }

  ensureInvoiceForFiscalDocument(fiscalDocumentId: number): Observable<CreateAccountsReceivableInvoiceResponse> {
    return this.http.post<CreateAccountsReceivableInvoiceResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/accounts-receivable/ensure`), {});
  }

  getInvoiceByFiscalDocumentId(fiscalDocumentId: number): Observable<AccountsReceivableInvoiceResponse> {
    return this.http.get<AccountsReceivableInvoiceResponse>(buildApiUrl(`/fiscal-documents/${fiscalDocumentId}/accounts-receivable`));
  }

  createPayment(request: CreateAccountsReceivablePaymentRequest): Observable<CreateAccountsReceivablePaymentResponse> {
    return this.http.post<CreateAccountsReceivablePaymentResponse>(buildApiUrl('/accounts-receivable/payments'), request);
  }

  getPaymentById(paymentId: number): Observable<AccountsReceivablePaymentResponse> {
    return this.http.get<AccountsReceivablePaymentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}`));
  }

  applyPayment(paymentId: number, request: ApplyAccountsReceivablePaymentRequest): Observable<ApplyAccountsReceivablePaymentResponse> {
    return this.http.post<ApplyAccountsReceivablePaymentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/apply`), request);
  }

  preparePaymentComplement(paymentId: number, request: PreparePaymentComplementRequest = {}): Observable<PreparePaymentComplementResponse> {
    return this.http.post<PreparePaymentComplementResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/payment-complements`), request);
  }

  getPaymentComplementByPaymentId(paymentId: number): Observable<PaymentComplementDocumentResponse> {
    return this.http.get<PaymentComplementDocumentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/payment-complement`));
  }
}
