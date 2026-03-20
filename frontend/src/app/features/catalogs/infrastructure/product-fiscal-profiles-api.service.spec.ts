import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductFiscalProfilesApiService } from './product-fiscal-profiles-api.service';

describe('ProductFiscalProfilesApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ProductFiscalProfilesApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('searches product fiscal profiles', () => {
    const service = TestBed.inject(ProductFiscalProfilesApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.search('SKU').subscribe();

    const req = httpTesting.expectOne('/api/fiscal/product-fiscal-profiles/search?q=SKU');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('puts product fiscal profile updates', () => {
    const service = TestBed.inject(ProductFiscalProfilesApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.update(10, {
      internalCode: 'SKU-1',
      description: 'Product',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'Pieza',
      isActive: true
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal/product-fiscal-profiles/10');
    expect(req.request.method).toBe('PUT');
    httpTesting.verify();
  });
});
