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
});
