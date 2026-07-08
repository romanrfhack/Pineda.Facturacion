import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AccountsReceivableApiService } from './accounts-receivable-api.service';

describe('AccountsReceivableApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AccountsReceivableApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('uses the plural route to prepare a payment complement from a payment', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.preparePaymentComplement(7).subscribe();

    const req = httpTesting.expectOne('/api/accounts-receivable/payments/7/payment-complements');
    expect(req.request.method).toBe('POST');
    req.flush({
      outcome: 'Created',
      isSuccess: true,
      accountsReceivablePaymentId: 7,
      paymentComplementId: 70,
      status: 'ReadyForStamping'
    });
    httpTesting.verify();
  });

  it('uses the plural route to read a payment complement by payment id', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getPaymentComplementByPaymentId(7).subscribe();

    const req = httpTesting.expectOne('/api/accounts-receivable/payments/7/payment-complements');
    expect(req.request.method).toBe('GET');
    req.flush({
      id: 70,
      accountsReceivablePaymentId: 7,
      status: 'ReadyForStamping',
      providerName: null,
      cfdiVersion: '4.0',
      documentType: 'P',
      appliesToIncomePpdInvoices: true,
      eligibilitySummary: 'OK',
      issuedAtUtc: '2026-04-03T00:00:00Z',
      paymentDateUtc: '2026-04-03T00:00:00Z',
      currencyCode: 'MXN',
      totalPaymentsAmount: 2722,
      issuerProfileId: 1,
      fiscalReceiverId: 77,
      issuerRfc: 'AAA010101AAA',
      issuerLegalName: 'Issuer',
      issuerFiscalRegimeCode: '601',
      issuerPostalCode: '01000',
      receiverRfc: 'BBB010101BBB',
      receiverLegalName: 'Receiver',
      receiverFiscalRegimeCode: '601',
      receiverPostalCode: '01000',
      receiverCountryCode: 'MEX',
      receiverForeignTaxRegistration: null,
      pacEnvironment: 'Sandbox',
      hasCertificateReference: true,
      hasPrivateKeyReference: true,
      hasPrivateKeyPasswordReference: true,
      relatedDocuments: []
    });
    httpTesting.verify();
  });

  it('uses the unapplied disposition route to confirm customer credit balance', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.setPaymentUnappliedDisposition(7, { unappliedDisposition: 'CustomerCreditBalance' }).subscribe();

    const req = httpTesting.expectOne('/api/accounts-receivable/payments/7/unapplied-disposition');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ unappliedDisposition: 'CustomerCreditBalance' });
    req.flush({
      outcome: 'Updated',
      isSuccess: true,
      accountsReceivablePaymentId: 7
    });
    httpTesting.verify();
  });

  it('uses the payment reassign applications route with the requested distribution', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const request = {
      reason: 'Corrección solicitada por cobranza',
      applications: [
        { accountsReceivableInvoiceId: 10, appliedAmount: 700 },
        { accountsReceivableInvoiceId: 11, appliedAmount: 300 },
      ],
    };

    service.reassignPaymentApplications(7, request).subscribe();

    const req = httpTesting.expectOne('/api/accounts-receivable/payments/7/reassign-applications');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      outcome: 'Reassigned',
      isSuccess: true,
      accountsReceivablePaymentId: 7,
      previousAppliedAmount: 1000,
      newAppliedAmount: 1000,
      remainingPaymentAmount: 0,
      payment: null,
      previousApplications: [],
      newApplications: [],
      affectedInvoiceIds: [10, 11],
    });
    httpTesting.verify();
  });

  it('uses payment mutation routes to update and delete a payment', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.updatePaymentAmount(7, { amount: 125.5 }).subscribe();

    let req = httpTesting.expectOne('/api/accounts-receivable/payments/7/amount');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ amount: 125.5 });
    req.flush({
      outcome: 'Updated',
      isSuccess: true,
      accountsReceivablePaymentId: 7,
      previousAmount: 100,
      updatedAmount: 125.5,
      payment: {
        id: 7,
        paymentDateUtc: '2026-04-03T00:00:00Z',
        paymentFormSat: '03',
        currencyCode: 'MXN',
        amount: 125.5,
        appliedTotal: 0,
        remainingAmount: 125.5,
        customerCreditBalanceAmount: 0,
        receivedFromFiscalReceiverId: 77,
        operationalStatus: 'CapturedUnapplied',
        repStatus: 'NoApplications',
        readyToPrepareRep: false,
        unappliedDisposition: 'PendingAllocation',
        repReservedAmount: 0,
        repFiscalizedAmount: 0,
        applicationsCount: 0,
        createdAtUtc: '2026-04-03T00:00:00Z',
        updatedAtUtc: '2026-04-03T00:00:00Z',
        applications: [],
      },
    });

    service.deletePayment(7).subscribe();

    req = httpTesting.expectOne('/api/accounts-receivable/payments/7');
    expect(req.request.method).toBe('DELETE');
    req.flush({
      outcome: 'Deleted',
      isSuccess: true,
      accountsReceivablePaymentId: 7,
      deletedAmount: 125.5,
      receivedFromFiscalReceiverId: 77,
    });

    httpTesting.verify();
  });

  it('preserves the paged invoice response shape when querying pending invoices for a receiver', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    let response: { items: Array<{ accountsReceivableInvoiceId: number }> } | undefined;

    service
      .searchPortfolio({
        fiscalReceiverId: 868,
        hasPendingBalance: true,
      })
      .subscribe((value) => {
        response = value;
      });

    const req = httpTesting.expectOne(
      '/api/accounts-receivable/invoices?fiscalReceiverId=868&hasPendingBalance=true',
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      items: [{ accountsReceivableInvoiceId: 11 }],
    });

    expect(response).toEqual({
      items: [{ accountsReceivableInvoiceId: 11 }],
    });
    httpTesting.verify();
  });

  it('uses receiver summary routes for candidates, preview and send', () => {
    const service = TestBed.inject(AccountsReceivableApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const request = {
      receiverId: '77',
      invoiceIds: [10],
      scope: 'manual' as const,
      to: ['cliente@example.com'],
      cc: [],
      bcc: [],
      subject: 'Resumen de adeudos pendientes - Cliente',
      message: 'Mensaje',
      format: 'html_with_pdf' as const,
      includeOptions: {
        invoiceTable: true,
        totalsByCurrency: true,
        highlightOverdue: true,
        paymentInstructions: true,
        receiverFiscalData: true,
        issuerData: true,
        invoiceLinks: true,
      },
    };

    service.getReceivablesSummaryCandidates(77).subscribe();
    let req = httpTesting.expectOne('/api/accounts-receivable/receivers/77/summary-candidates');
    expect(req.request.method).toBe('GET');
    req.flush({
      receiver: { id: 77, legalName: 'Cliente', rfc: 'AAA010101AAA' },
      issuer: { id: 1, legalName: 'Emisor', rfc: 'III010101III' },
      defaultTo: ['cliente@example.com'],
      defaultSubject: 'Resumen',
      defaultMessage: 'Mensaje',
      invoices: [],
    });

    service.previewReceivablesSummary(77, request).subscribe();
    req = httpTesting.expectOne('/api/accounts-receivable/receivers/77/summary-preview');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ outcome: 'Found', success: true, html: '<p>ok</p>' });

    service.sendReceivablesSummary(77, request).subscribe();
    req = httpTesting.expectOne('/api/accounts-receivable/receivers/77/send-summary');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ success: true, outcome: 'Sent', sentAt: '2026-04-29T12:00:00Z', historyId: '1', attachedPdf: true });

    httpTesting.verify();
  });
});
