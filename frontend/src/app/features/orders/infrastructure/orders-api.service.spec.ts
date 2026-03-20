import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { OrdersApiService } from './orders-api.service';

describe('OrdersApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [OrdersApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('posts to the legacy import endpoint', () => {
    const service = TestBed.inject(OrdersApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.importLegacyOrder('LEG-123').subscribe();

    const req = httpTesting.expectOne('/api/orders/LEG-123/import');
    expect(req.request.method).toBe('POST');
    httpTesting.verify();
  });
});
