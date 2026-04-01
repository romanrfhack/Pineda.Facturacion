import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PaymentComplementsApiService } from './payment-complements-api.service';

describe('PaymentComplementsApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PaymentComplementsApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('gets payment-complement stamp xml as plain text', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getStampXml(60).subscribe((xml) => {
      expect(xml).toContain('<cfdi:Comprobante');
    });

    const req = httpTesting.expectOne('/api/payment-complements/60/stamp/xml');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('text');
    req.flush('<cfdi:Comprobante />');
    httpTesting.verify();
  });

  it('searches internal rep base documents with filters', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchInternalBaseDocuments({
      page: 2,
      pageSize: 10,
      fromDate: '2026-04-01',
      toDate: '2026-04-30',
      receiverRfc: 'BBB010101BBB',
      query: 'UUID-REP-1',
      eligible: true,
      blocked: false,
      withOutstandingBalance: true,
      hasRepEmitted: false
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents/internal?page=2&pageSize=10&fromDate=2026-04-01&toDate=2026-04-30&receiverRfc=BBB010101BBB&query=UUID-REP-1&eligible=true&blocked=false&withOutstandingBalance=true&hasRepEmitted=false');
    expect(req.request.method).toBe('GET');
    req.flush({ page: 2, pageSize: 10, totalCount: 0, totalPages: 0, items: [] });
    httpTesting.verify();
  });

  it('registers and applies a payment from the internal base-document context', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.registerInternalBaseDocumentPayment(501, {
      paymentDate: '2026-04-01',
      paymentFormSat: '03',
      amount: 40,
      reference: 'TRANS-123',
      notes: 'Pago parcial'
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents/internal/501/payments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      paymentDate: '2026-04-01',
      paymentFormSat: '03',
      amount: 40,
      reference: 'TRANS-123',
      notes: 'Pago parcial'
    });
    req.flush({
      outcome: 'RegisteredAndApplied',
      isSuccess: true,
      warningMessages: [],
      fiscalDocumentId: 501,
      accountsReceivableInvoiceId: 201,
      accountsReceivablePaymentId: 9002,
      appliedAmount: 40,
      remainingBalance: 36,
      remainingPaymentAmount: 0,
      applications: []
    });
    httpTesting.verify();
  });
});
