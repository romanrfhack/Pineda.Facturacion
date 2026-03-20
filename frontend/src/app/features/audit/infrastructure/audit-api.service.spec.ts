import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditApiService } from './audit-api.service';

describe('AuditApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuditApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('queries the audit endpoint with filters', () => {
    const service = TestBed.inject(AuditApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.list({
      page: 1,
      pageSize: 25,
      actorUsername: 'admin',
      actionType: 'FiscalDocument.Stamp'
    }).subscribe();

    const req = httpTesting.expectOne((request) =>
      request.url === '/api/audit-events'
      && request.params.get('page') === '1'
      && request.params.get('pageSize') === '25'
      && request.params.get('actorUsername') === 'admin'
      && request.params.get('actionType') === 'FiscalDocument.Stamp');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });
});
