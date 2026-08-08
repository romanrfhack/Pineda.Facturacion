import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GLOBAL_LOADER_OPTIONS } from '../../../core/http/global-loader-context.tokens';
import { PaymentComplementsApiService } from './payment-complements-api.service';

describe('PaymentComplementsApiService loader contexts', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PaymentComplementsApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('enables contextual loaders for the main REP trays', () => {
    const service = TestBed.inject(PaymentComplementsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchInternalBaseDocuments({ page: 1, pageSize: 10 }).subscribe();
    let req = httpTesting.expectOne('/api/payment-complements/base-documents/internal?page=1&pageSize=10');
    expect(req.request.context.get(GLOBAL_LOADER_OPTIONS)).toMatchObject({
      message: 'Consultando documentos REP internos'
    });

    service.searchExternalBaseDocuments({ page: 1, pageSize: 10 }).subscribe();
    req = httpTesting.expectOne('/api/payment-complements/base-documents/external?page=1&pageSize=10');
    expect(req.request.context.get(GLOBAL_LOADER_OPTIONS)).toMatchObject({
      message: 'Consultando documentos REP externos'
    });

    service.searchBaseDocuments({ page: 1, pageSize: 10 }).subscribe();
    req = httpTesting.expectOne('/api/payment-complements/base-documents?page=1&pageSize=10');
    expect(req.request.context.get(GLOBAL_LOADER_OPTIONS)).toMatchObject({
      message: 'Consultando bandeja REP'
    });

    service.searchAttentionItems({ page: 1, pageSize: 10 }).subscribe();
    req = httpTesting.expectOne('/api/payment-complements/attention-items?page=1&pageSize=10');
    expect(req.request.context.get(GLOBAL_LOADER_OPTIONS)).toMatchObject({
      message: 'Consultando pendientes REP'
    });

    httpTesting.verify();
  });
});
