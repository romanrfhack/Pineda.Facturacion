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
});
