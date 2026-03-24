import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { IssuerProfileApiService } from './issuer-profile-api.service';

describe('IssuerProfileApiService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [IssuerProfileApiService, provideHttpClient(), provideHttpClientTesting()]
    });
  });

  it('gets the active issuer profile', () => {
    const service = TestBed.inject(IssuerProfileApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getActive().subscribe();

    const req = httpTesting.expectOne('/api/fiscal/issuer-profile/active');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });

  it('uploads an issuer logo as multipart form data', () => {
    const service = TestBed.inject(IssuerProfileApiService);
    const httpTesting = TestBed.inject(HttpTestingController);
    const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'logo.png', { type: 'image/png' });

    service.uploadLogo(10, file).subscribe();

    const req = httpTesting.expectOne('/api/fiscal/issuer-profile/10/logo');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body instanceof FormData).toBe(true);
    httpTesting.verify();
  });

  it('gets an issuer logo as blob', () => {
    const service = TestBed.inject(IssuerProfileApiService);
    const httpTesting = TestBed.inject(HttpTestingController);

    service.getLogo(11).subscribe();

    const req = httpTesting.expectOne('/api/fiscal/issuer-profile/11/logo');
    expect(req.request.method).toBe('GET');
    httpTesting.verify();
  });
});
