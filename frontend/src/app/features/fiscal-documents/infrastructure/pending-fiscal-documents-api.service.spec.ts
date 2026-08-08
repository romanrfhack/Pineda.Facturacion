import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PendingFiscalDocumentsApiService } from './pending-fiscal-documents-api.service';

describe('PendingFiscalDocumentsApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PendingFiscalDocumentsApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  it('searches the pending-stamping inbox with paging and operational filters', () => {
    const service = TestBed.inject(PendingFiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.search({
      page: 2,
      pageSize: 10,
      query: ' Cliente Uno ',
      workStatus: 'RequiresAttention',
      sort: 'OldestFirst',
    }).subscribe();

    const request = httpTesting.expectOne(
      '/api/fiscal-documents/pending-stamping?page=2&pageSize=10&workStatus=RequiresAttention&sort=OldestFirst&query=Cliente+Uno',
    );
    expect(request.request.method).toBe('GET');
    request.flush({
      page: 2,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
      items: [],
    });
    httpTesting.verify();
  });

  it('omits an empty free-text query', () => {
    const service = TestBed.inject(PendingFiscalDocumentsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.search({
      page: 1,
      pageSize: 25,
      query: '   ',
      workStatus: 'All',
      sort: 'LastActivityDesc',
    }).subscribe();

    const request = httpTesting.expectOne(
      '/api/fiscal-documents/pending-stamping?page=1&pageSize=25&workStatus=All&sort=LastActivityDesc',
    );
    expect(request.request.method).toBe('GET');
    request.flush({
      page: 1,
      pageSize: 25,
      totalCount: 0,
      totalPages: 0,
      items: [],
    });
    httpTesting.verify();
  });
});
