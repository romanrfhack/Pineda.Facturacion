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
});
