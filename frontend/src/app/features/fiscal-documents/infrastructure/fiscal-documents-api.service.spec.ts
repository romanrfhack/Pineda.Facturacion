import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FiscalDocumentsApiService } from './fiscal-documents-api.service';
import { SUPPRESS_GLOBAL_ERROR_TOAST } from '../../../core/http/api-error-context.tokens';

describe('FiscalDocumentsApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FiscalDocumentsApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('posts fiscal-document preparation to the billing-document endpoint', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.prepareFiscalDocument(30, {
      fiscalReceiverId: 9,
      paymentMethodSat: 'PPD',
      paymentFormSat: '99',
      isCreditSale: true,
      creditDays: 7
    }).subscribe();

    const req = httpTesting.expectOne('/api/billing-documents/30/fiscal-documents');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.fiscalReceiverId).toBe(9);
    httpTesting.verify();
  });

  it('searches only active fiscal receivers for the operational flow', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchReceivers('BBB').subscribe();

    const req = httpTesting.expectOne('/api/fiscal/receivers/search?q=BBB&activeOnly=true');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('gets fiscal stamp xml as plain text', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getStampXml(40).subscribe((xml) => {
      expect(xml).toContain('<cfdi:Comprobante');
    });

    const req = httpTesting.expectOne('/api/fiscal-documents/40/stamp/xml');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('text');
    req.flush('<cfdi:Comprobante />');
    httpTesting.verify();
  });

  it('gets fiscal stamp pdf as blob', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getStampPdf(40).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/stamp/pdf');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['%PDF-1.4'], { type: 'application/pdf' }));
    httpTesting.verify();
  });

  it('gets fiscal document email draft', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getEmailDraft(40).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/email-draft');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('posts fiscal document email request', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.sendByEmail(40, {
      recipients: ['cliente@example.com'],
      subject: 'CFDI timbrado',
      body: 'Adjuntamos CFDI.'
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/email');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.recipients).toEqual(['cliente@example.com']);
    httpTesting.verify();
  });

  it('posts CFDI cancellation with local timeout options and suppressed global toast', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.cancelFiscalDocument(
      40,
      {
        cancellationReasonCode: '03',
      },
      {
        timeoutMs: 75_000,
        suppressGlobalErrorToast: true,
      },
    ).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/cancel');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ cancellationReasonCode: '03' });
    expect(req.request.context.get(SUPPRESS_GLOBAL_ERROR_TOAST)).toBe(true);
    req.flush({
      outcome: 'Cancelled',
      isSuccess: true,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'Cancelled',
      cancellationStatus: 'Cancelled',
    });
    httpTesting.verify();
  });

  it('posts the stamp-and-email orchestration request', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.stampAndEmailFiscalDocument(40, {
      retryRejected: false
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/stamp-and-email');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.retryRejected).toBe(false);
    httpTesting.verify();
  });

  it('posts the reprepare request for an existing fiscal snapshot', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.reprepareFiscalDocument(40).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/reprepare');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    httpTesting.verify();
  });

  it('posts the fiscal-profile override request for a persisted fiscal line', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.updateFiscalDocumentItemFiscalProfile(15, {
      satProductServiceCode: '40161513',
      satUnitCode: 'E48',
      taxObjectCode: '02',
      vatRate: 0.16,
      unitText: 'SERVICIO'
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/items/15/fiscal-profile');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      satProductServiceCode: '40161513',
      satUnitCode: 'E48',
      taxObjectCode: '02',
      vatRate: 0.16,
      unitText: 'SERVICIO'
    });
    httpTesting.verify();
  });

  it('gets billing document context by id', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getBillingDocumentById(30).subscribe();

    const req = httpTesting.expectOne('/api/billing-documents/30');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('gets pending cancellation authorizations as a secondary request without global error toast', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.listPendingCancellationAuthorizations().subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/cancellation-authorizations/pending');
    expect(req.request.method).toBe('GET');
    expect(req.request.context.get(SUPPRESS_GLOBAL_ERROR_TOAST)).toBe(true);
    httpTesting.verify();
  });

  it('searches billing documents by query', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchBillingDocuments('LEG-1001').subscribe();

    const req = httpTesting.expectOne('/api/billing-documents/search?q=LEG-1001');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('searches grouped billing documents by query', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchBillingDocumentsGrouped('1723').subscribe();

    const req = httpTesting.expectOne('/api/billing-documents/search/grouped?q=1723&takePerGroup=5');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('searches issued CFDI with paged filters', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchIssued({
      page: 2,
      pageSize: 10,
      fromDate: '2026-03-01',
      toDate: '2026-03-24',
      receiverRfc: 'BBB010101BBB',
      uuid: 'UUID-1',
      specialFieldCode: 'AGENTE',
      specialFieldValue: 'Juan',
      status: 'Stamped'
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/issued?page=2&pageSize=10&fromDate=2026-03-01&toDate=2026-03-24&receiverRfc=BBB010101BBB&uuid=UUID-1&status=Stamped&specialFieldCode=AGENTE&specialFieldValue=Juan');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('gets fiscal stamp xml file as blob', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getStampXmlFile(40).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/stamp/xml');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['<cfdi:Comprobante />'], { type: 'application/xml' }));
    httpTesting.verify();
  });

  it('posts remote CFDI lookup to the support endpoint', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.queryRemoteStamp(40).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/stamp/remote-query');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    httpTesting.verify();
  });

  it('posts fiscal-document special-field synchronization', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.syncFiscalDocumentSpecialFields(40, {
      specialFields: [
        { fieldCode: 'PERIODICIDAD', value: '01' },
        { fieldCode: 'MESES', value: '03' },
        { fieldCode: 'AÑO', value: '2026' }
      ]
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal-documents/40/special-fields/sync');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      specialFields: [
        { fieldCode: 'PERIODICIDAD', value: '01' },
        { fieldCode: 'MESES', value: '03' },
        { fieldCode: 'AÑO', value: '2026' }
      ]
    });
    httpTesting.verify();
  });
});
