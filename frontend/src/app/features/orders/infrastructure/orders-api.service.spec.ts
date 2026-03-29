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

  it('queries the paged legacy orders endpoint', () => {
    const service = TestBed.inject(OrdersApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchLegacyOrders({
      fromDate: '2026-03-23',
      toDate: '2026-03-23',
      customerQuery: 'Cliente Uno',
      page: 1,
      pageSize: 10
    }).subscribe();

    const req = httpTesting.expectOne((request) =>
      request.url === '/api/orders/legacy'
      && request.params.get('fromDate') === '2026-03-23'
      && request.params.get('toDate') === '2026-03-23'
      && request.params.get('customerQuery') === 'Cliente Uno'
      && request.params.get('page') === '1'
      && request.params.get('pageSize') === '10');

    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });
});
