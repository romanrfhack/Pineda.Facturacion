import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FiscalReceiversApiService } from './fiscal-receivers-api.service';

describe('FiscalReceiversApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FiscalReceiversApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('searches receivers by query', () => {
    const service = TestBed.inject(FiscalReceiversApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.search('AAA').subscribe();

    const req = httpTesting.expectOne('/api/fiscal/receivers/search?q=AAA');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('posts receiver creation', () => {
    const service = TestBed.inject(FiscalReceiversApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.create({
      rfc: 'AAA010101AAA',
      legalName: 'Receiver',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      postalCode: '01000',
      isActive: true
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal/receivers/');
    expect(req.request.method).toBe('POST');
    httpTesting.verify();
  });
});
