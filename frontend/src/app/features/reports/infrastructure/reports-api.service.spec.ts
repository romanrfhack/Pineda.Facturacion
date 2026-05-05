import { HttpResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReportsApiService } from './reports-api.service';

describe('ReportsApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ReportsApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('searches stamped legacy notes with current filters', () => {
    const service = TestBed.inject(ReportsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.searchStampedLegacyNotes({
      fromDate: '2026-05-01',
      toDate: '2026-05-04',
      page: 2,
      pageSize: 50,
      receiverSearch: 'Cliente',
      uuid: 'UUID',
      series: 'A',
      folio: '100',
      legacyOrderId: '1171335',
      legacyOrderNumber: 'REF'
    }).subscribe();

    const req = httpTesting.expectOne((request) =>
      request.url === '/api/reports/stamped-legacy-notes'
      && request.params.get('fromDate') === '2026-05-01'
      && request.params.get('toDate') === '2026-05-04'
      && request.params.get('page') === '2'
      && request.params.get('pageSize') === '50'
      && request.params.get('receiverSearch') === 'Cliente'
      && request.params.get('uuid') === 'UUID'
      && request.params.get('series') === 'A'
      && request.params.get('folio') === '100'
      && request.params.get('legacyOrderId') === '1171335'
      && request.params.get('legacyOrderNumber') === 'REF');
    expect(req.request.method).toBe('GET');
    req.flush({ page: 2, pageSize: 50, totalCount: 0, totalPages: 0, items: [] });
    httpTesting.verify();
  });

  it('exports stamped legacy notes without pagination params', () => {
    const service = TestBed.inject(ReportsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.exportStampedLegacyNotes({
      fromDate: '2026-05-01',
      toDate: '2026-05-04',
      page: 3,
      pageSize: 25,
      legacyOrderId: '1171335'
    }).subscribe((response: HttpResponse<Blob>) => {
      expect(response.body).toBeTruthy();
    });

    const req = httpTesting.expectOne((request) =>
      request.url === '/api/reports/stamped-legacy-notes/export'
      && request.params.get('fromDate') === '2026-05-01'
      && request.params.get('toDate') === '2026-05-04'
      && request.params.get('legacyOrderId') === '1171335'
      && !request.params.has('page')
      && !request.params.has('pageSize'));
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['xlsx'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }));
    httpTesting.verify();
  });
});
