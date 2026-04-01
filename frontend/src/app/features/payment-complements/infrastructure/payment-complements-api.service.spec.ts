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

  it('prepares a payment complement from the internal base-document context', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.prepareInternalBaseDocumentPaymentComplement(501, {
      accountsReceivablePaymentId: 9002
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents/internal/501/prepare');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ accountsReceivablePaymentId: 9002 });
    req.flush({
      outcome: 'Prepared',
      isSuccess: true,
      warningMessages: [],
      fiscalDocumentId: 501,
      accountsReceivablePaymentId: 9002,
      paymentComplementDocumentId: 7002,
      status: 'ReadyForStamping',
      relatedDocumentCount: 1
    });
    httpTesting.verify();
  });

  it('stamps a payment complement from the internal base-document context', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.stampInternalBaseDocumentPaymentComplement(501, {
      paymentComplementDocumentId: 7002,
      retryRejected: false
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents/internal/501/stamp');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ paymentComplementDocumentId: 7002, retryRejected: false });
    req.flush({
      outcome: 'Stamped',
      isSuccess: true,
      warningMessages: [],
      fiscalDocumentId: 501,
      accountsReceivablePaymentId: 9002,
      paymentComplementDocumentId: 7002,
      status: 'Stamped',
      paymentComplementStampId: 8002,
      stampUuid: 'UUID-PC-2',
      stampedAtUtc: '2026-04-05T12:00:00Z',
      xmlAvailable: true
    });
    httpTesting.verify();
  });

  it('uploads an external xml invoice as form-data', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const file = new File(['<cfdi:Comprobante />'], 'external.xml', { type: 'application/xml' });

    service.importExternalBaseDocumentXml(file).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/external-base-documents/import-xml');
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    req.flush({
      outcome: 'Accepted',
      isSuccess: true,
      externalRepBaseDocumentId: 123,
      validationStatus: 'Accepted',
      reasonCode: 'Accepted',
      reasonMessage: 'Factura importada.'
    });
    httpTesting.verify();
  });

  it('gets imported external rep base document detail', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getExternalBaseDocumentById(123).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/external-base-documents/123');
    expect(req.request.method).toBe('GET');
    req.flush({
      summary: {
        externalRepBaseDocumentId: 123,
        uuid: 'UUID-EXT-1',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'EXT',
        folio: '1001',
        issuedAtUtc: '2026-04-01T10:00:00Z',
        issuerRfc: 'AAA010101AAA',
        receiverRfc: 'BBB010101BBB',
        receiverLegalName: 'Cliente externo',
        currencyCode: 'MXN',
        exchangeRate: 1,
        subtotal: 100,
        total: 116,
        paidTotal: 0,
        outstandingBalance: 116,
        paymentMethodSat: 'PPD',
        paymentFormSat: '99',
        validationStatus: 'Accepted',
        reasonCode: 'Accepted',
        reasonMessage: 'Factura importada.',
        satStatus: 'Active',
        sourceFileName: 'external.xml',
        xmlHash: 'HASH-1',
        importedAtUtc: '2026-04-01T11:00:00Z',
        operationalStatus: 'ReadyForPayment',
        isEligible: true,
        isBlocked: false,
        primaryReasonCode: 'ReadyForPayment',
        primaryReasonMessage: 'Lista para registrar pago.',
        availableActions: ['ViewDetail', 'RegisterPayment'],
        registeredPaymentCount: 0,
        paymentComplementCount: 0,
        stampedPaymentComplementCount: 0
      },
      paymentHistory: [],
      paymentApplications: [],
      issuedReps: []
    });
    httpTesting.verify();
  });

  it('registers and applies a payment from the external base-document context', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.registerExternalBaseDocumentPayment(123, {
      paymentDate: '2026-04-01',
      paymentFormSat: '03',
      amount: 50,
      reference: 'TRX-EXT-1',
      notes: 'Pago externo'
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents/external/123/payments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      paymentDate: '2026-04-01',
      paymentFormSat: '03',
      amount: 50,
      reference: 'TRX-EXT-1',
      notes: 'Pago externo'
    });
    req.flush({
      outcome: 'RegisteredAndApplied',
      isSuccess: true,
      warningMessages: [],
      externalRepBaseDocumentId: 123,
      accountsReceivableInvoiceId: 401,
      accountsReceivablePaymentId: 901,
      appliedAmount: 50,
      remainingBalance: 66,
      remainingPaymentAmount: 0,
      applications: []
    });
    httpTesting.verify();
  });

  it('prepares and stamps a payment complement from the external base-document context', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.prepareExternalBaseDocumentPaymentComplement(123, {}).subscribe();
    const prepareReq = httpTesting.expectOne('/api/payment-complements/base-documents/external/123/prepare');
    expect(prepareReq.request.method).toBe('POST');
    expect(prepareReq.request.body).toEqual({});
    prepareReq.flush({
      outcome: 'Prepared',
      isSuccess: true,
      warningMessages: [],
      externalRepBaseDocumentId: 123,
      accountsReceivablePaymentId: 901,
      paymentComplementDocumentId: 701,
      status: 'ReadyForStamping',
      relatedDocumentCount: 1
    });

    service.stampExternalBaseDocumentPaymentComplement(123, {}).subscribe();
    const stampReq = httpTesting.expectOne('/api/payment-complements/base-documents/external/123/stamp');
    expect(stampReq.request.method).toBe('POST');
    expect(stampReq.request.body).toEqual({});
    stampReq.flush({
      outcome: 'Stamped',
      isSuccess: true,
      warningMessages: [],
      externalRepBaseDocumentId: 123,
      accountsReceivablePaymentId: 901,
      paymentComplementDocumentId: 701,
      paymentComplementStampId: 801,
      stampUuid: 'UUID-REP-EXT-1',
      stampedAtUtc: '2026-04-01T13:00:00Z',
      xmlAvailable: true
    });

    httpTesting.verify();
  });

  it('searches external rep base documents with filters', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchExternalBaseDocuments({
      page: 1,
      pageSize: 25,
      fromDate: '2026-04-01',
      toDate: '2026-04-30',
      receiverRfc: 'BBB010101BBB',
      query: 'UUID-EXT-1',
      validationStatus: 'Accepted',
      eligible: true,
      blocked: false
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents/external?page=1&pageSize=25&fromDate=2026-04-01&toDate=2026-04-30&receiverRfc=BBB010101BBB&query=UUID-EXT-1&validationStatus=Accepted&eligible=true&blocked=false');
    expect(req.request.method).toBe('GET');
    req.flush({ page: 1, pageSize: 25, totalCount: 0, totalPages: 0, items: [] });
    httpTesting.verify();
  });

  it('searches unified rep base documents with filters', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchBaseDocuments({
      page: 1,
      pageSize: 25,
      fromDate: '2026-04-01',
      toDate: '2026-04-30',
      receiverRfc: 'BBB010101BBB',
      query: 'UUID-REP-1',
      sourceType: 'External',
      validationStatus: 'Accepted',
      eligible: true,
      blocked: false
    }).subscribe();

    const req = httpTesting.expectOne('/api/payment-complements/base-documents?page=1&pageSize=25&fromDate=2026-04-01&toDate=2026-04-30&receiverRfc=BBB010101BBB&query=UUID-REP-1&sourceType=External&validationStatus=Accepted&eligible=true&blocked=false');
    expect(req.request.method).toBe('GET');
    req.flush({ page: 1, pageSize: 25, totalCount: 0, totalPages: 0, items: [] });
    httpTesting.verify();
  });
});
