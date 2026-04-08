import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FiscalImportsApiService } from './fiscal-imports-api.service';

describe('FiscalImportsApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FiscalImportsApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('maps receiver apply-all requests to the backend enum payload', () => {
    const service = TestBed.inject(FiscalImportsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.applyReceiverBatch(12, {
      applyMode: 'CreateOnly',
      stopOnFirstError: false
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal/imports/receivers/batches/12/apply');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      applyMode: 0,
      stopOnFirstError: false
    });
    httpTesting.verify();
  });

  it('maps specific product rows to the backend enum payload', () => {
    const service = TestBed.inject(FiscalImportsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.applyProductBatch(18, {
      applyMode: 'CreateAndUpdate',
      selectedRowNumbers: [1, 7, 8],
      stopOnFirstError: true
    }).subscribe();

    const req = httpTesting.expectOne('/api/fiscal/imports/products/batches/18/apply');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      applyMode: 1,
      selectedRowNumbers: [1, 7, 8],
      stopOnFirstError: true
    });
    httpTesting.verify();
  });

  it('uploads the official SAT catalog as multipart form data without manual version or file name fields', () => {
    const service = TestBed.inject(FiscalImportsApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const file = new File(['excel'], 'catalogos_sat.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });

    service.importOfficialSatCatalog(file, 'sha256:preview').subscribe();

    const req = httpTesting.expectOne('/api/fiscal/imports/sat/official');
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);

    const form = req.request.body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('sourceChecksum')).toBe('sha256:preview');
    expect(form.has('sourceVersion')).toBe(false);
    expect(form.has('sourceFileName')).toBe(false);
    httpTesting.verify();
  });
});
