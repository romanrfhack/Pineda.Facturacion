import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { FiscalDocumentsApiService } from './fiscal-documents-api.service';

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

  it('gets billing document context by id', () => {
    const service = TestBed.inject(FiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getBillingDocumentById(30).subscribe();

    const req = httpTesting.expectOne('/api/billing-documents/30');
    expect(req.request.method).toBe('GET');
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
});
