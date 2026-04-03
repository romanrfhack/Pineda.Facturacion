import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivablePortfolioResponse,
  AccountsReceivableReceiverWorkspaceResponse,
  AccountsReceivablePaymentsResponse,
  AccountsReceivablePaymentResponse,
  ApplyAccountsReceivablePaymentRequest,
  ApplyAccountsReceivablePaymentResponse,
  CollectionCommitmentsResponse,
  CollectionNotesResponse,
  CreateAccountsReceivableInvoiceRequest,
  CreateAccountsReceivableInvoiceResponse,
  CreateCollectionCommitmentRequest,
  CreateCollectionCommitmentResponse,
  CreateCollectionNoteRequest,
  CreateCollectionNoteResponse,
  CreateAccountsReceivablePaymentRequest,
  CreateAccountsReceivablePaymentResponse,
  SearchAccountsReceivablePortfolioRequest,
  SearchAccountsReceivablePaymentsRequest,
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

  getInvoiceById(accountsReceivableInvoiceId: number): Observable<AccountsReceivableInvoiceResponse> {
    return this.http.get<AccountsReceivableInvoiceResponse>(buildApiUrl(`/accounts-receivable/invoices/${accountsReceivableInvoiceId}`));
  }

  searchPortfolio(request: SearchAccountsReceivablePortfolioRequest = {}): Observable<AccountsReceivablePortfolioResponse> {
    let params = new HttpParams();

    if (request.fiscalReceiverId != null) {
      params = params.set('fiscalReceiverId', request.fiscalReceiverId);
    }
    if (request.receiverQuery) {
      params = params.set('receiverQuery', request.receiverQuery);
    }
    if (request.status) {
      params = params.set('status', request.status);
    }
    if (request.dueDateFrom) {
      params = params.set('dueDateFrom', request.dueDateFrom);
    }
    if (request.dueDateTo) {
      params = params.set('dueDateTo', request.dueDateTo);
    }
    if (request.hasPendingBalance != null) {
      params = params.set('hasPendingBalance', request.hasPendingBalance);
    }
    if (request.overdueOnly != null) {
      params = params.set('overdueOnly', request.overdueOnly);
    }
    if (request.dueSoonOnly != null) {
      params = params.set('dueSoonOnly', request.dueSoonOnly);
    }
    if (request.hasPendingCommitment != null) {
      params = params.set('hasPendingCommitment', request.hasPendingCommitment);
    }
    if (request.followUpPending != null) {
      params = params.set('followUpPending', request.followUpPending);
    }

    return this.http.get<AccountsReceivablePortfolioResponse>(buildApiUrl('/accounts-receivable/invoices'), { params });
  }

  getReceiverWorkspace(fiscalReceiverId: number): Observable<AccountsReceivableReceiverWorkspaceResponse> {
    return this.http.get<AccountsReceivableReceiverWorkspaceResponse>(buildApiUrl(`/accounts-receivable/receivers/${fiscalReceiverId}/workspace`));
  }

  listCollectionCommitments(accountsReceivableInvoiceId: number): Observable<CollectionCommitmentsResponse> {
    return this.http.get<CollectionCommitmentsResponse>(buildApiUrl(`/accounts-receivable/invoices/${accountsReceivableInvoiceId}/collection-commitments`));
  }

  createCollectionCommitment(accountsReceivableInvoiceId: number, request: CreateCollectionCommitmentRequest): Observable<CreateCollectionCommitmentResponse> {
    return this.http.post<CreateCollectionCommitmentResponse>(buildApiUrl(`/accounts-receivable/invoices/${accountsReceivableInvoiceId}/collection-commitments`), request);
  }

  listCollectionNotes(accountsReceivableInvoiceId: number): Observable<CollectionNotesResponse> {
    return this.http.get<CollectionNotesResponse>(buildApiUrl(`/accounts-receivable/invoices/${accountsReceivableInvoiceId}/collection-notes`));
  }

  createCollectionNote(accountsReceivableInvoiceId: number, request: CreateCollectionNoteRequest): Observable<CreateCollectionNoteResponse> {
    return this.http.post<CreateCollectionNoteResponse>(buildApiUrl(`/accounts-receivable/invoices/${accountsReceivableInvoiceId}/collection-notes`), request);
  }

  createPayment(request: CreateAccountsReceivablePaymentRequest): Observable<CreateAccountsReceivablePaymentResponse> {
    return this.http.post<CreateAccountsReceivablePaymentResponse>(buildApiUrl('/accounts-receivable/payments'), request);
  }

  getPaymentById(paymentId: number): Observable<AccountsReceivablePaymentResponse> {
    return this.http.get<AccountsReceivablePaymentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}`));
  }

  searchPayments(request: SearchAccountsReceivablePaymentsRequest = {}): Observable<AccountsReceivablePaymentsResponse> {
    let params = new HttpParams();

    if (request.fiscalReceiverId != null) {
      params = params.set('fiscalReceiverId', request.fiscalReceiverId);
    }
    if (request.operationalStatus) {
      params = params.set('operationalStatus', request.operationalStatus);
    }
    if (request.receivedFrom) {
      params = params.set('receivedFrom', request.receivedFrom);
    }
    if (request.receivedTo) {
      params = params.set('receivedTo', request.receivedTo);
    }
    if (request.hasUnappliedAmount != null) {
      params = params.set('hasUnappliedAmount', request.hasUnappliedAmount);
    }
    if (request.linkedFiscalDocumentId != null) {
      params = params.set('linkedFiscalDocumentId', request.linkedFiscalDocumentId);
    }

    return this.http.get<AccountsReceivablePaymentsResponse>(buildApiUrl('/accounts-receivable/payments'), { params });
  }

  applyPayment(paymentId: number, request: ApplyAccountsReceivablePaymentRequest): Observable<ApplyAccountsReceivablePaymentResponse> {
    return this.http.post<ApplyAccountsReceivablePaymentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/apply`), request);
  }

  preparePaymentComplement(paymentId: number, request: PreparePaymentComplementRequest = {}): Observable<PreparePaymentComplementResponse> {
    return this.http.post<PreparePaymentComplementResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/payment-complements`), request);
  }

  getPaymentComplementByPaymentId(paymentId: number): Observable<PaymentComplementDocumentResponse> {
    return this.http.get<PaymentComplementDocumentResponse>(buildApiUrl(`/accounts-receivable/payments/${paymentId}/payment-complements`));
  }
}
