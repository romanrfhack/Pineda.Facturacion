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
});
