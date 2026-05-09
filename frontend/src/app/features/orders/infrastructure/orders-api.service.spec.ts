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
      legacyOrderId: '1175479',
      customerQuery: 'Cliente Uno',
      page: 1,
      pageSize: 10
    }).subscribe();

    const req = httpTesting.expectOne((request) =>
      request.url === '/api/orders/legacy'
      && request.params.get('fromDate') === '2026-03-23'
      && request.params.get('toDate') === '2026-03-23'
      && request.params.get('legacyOrderId') === '1175479'
      && request.params.get('customerQuery') === 'Cliente Uno'
      && request.params.get('page') === '1'
      && request.params.get('pageSize') === '10');

    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('omits date params when the period filter is neutral', () => {
    const service = TestBed.inject(OrdersApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchLegacyOrders({
      legacyOrderId: '1175479',
      customerQuery: 'Cliente Uno',
      page: 1,
      pageSize: 10
    }).subscribe();

    const req = httpTesting.expectOne((request) =>
      request.url === '/api/orders/legacy'
      && request.params.get('fromDate') === null
      && request.params.get('toDate') === null
      && request.params.get('legacyOrderId') === '1175479'
      && request.params.get('customerQuery') === 'Cliente Uno'
      && request.params.get('page') === '1'
      && request.params.get('pageSize') === '10');

    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('posts the bulk billing creation request with explicit ids or filters', () => {
    const service = TestBed.inject(OrdersApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.createBulkBillingDocument({
      documentType: 'I',
      selectionMode: 'Filtered',
      filters: {
        fromDate: '2026-03-23',
        toDate: '2026-03-23',
        customerQuery: 'Cliente Uno'
      }
    }).subscribe();

    const req = httpTesting.expectOne('/api/orders/billing-documents');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      documentType: 'I',
      selectionMode: 'Filtered',
      filters: {
        fromDate: '2026-03-23',
        toDate: '2026-03-23',
        customerQuery: 'Cliente Uno'
      }
    });
    httpTesting.verify();
  });

  it('uses the order debt summary preview and send routes', () => {
    const service = TestBed.inject(OrdersApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const request = {
      legacyOrderIds: ['LEG-1001'],
      receiverId: 77,
      to: ['cliente@example.com'],
      cc: [],
      bcc: [],
      subject: 'Resumen',
      message: 'Mensaje',
      format: 'html' as const,
      options: {
        includeOrderTable: true,
        includeTotals: true,
        includeReceiverFiscalData: true,
        includeIssuerData: true,
        includePaymentInstructions: true,
        includeBillingStatus: true
      }
    };

    service.previewOrderDebtSummary(request).subscribe();
    let req = httpTesting.expectOne('/api/orders/debt-summary/preview');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ outcome: 'Found', success: true });

    service.sendOrderDebtSummary(request).subscribe();
    req = httpTesting.expectOne('/api/orders/debt-summary/send');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    httpTesting.verify();
  });
});
