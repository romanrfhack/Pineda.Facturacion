import { signal } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionService } from '../../auth/session.service';
import { authTokenInterceptor } from './auth-token.interceptor';

describe('authTokenInterceptor', () => {
  const token = signal<string | null>(null);

  beforeEach(() => {
    token.set(null);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authTokenInterceptor])),
        provideHttpClientTesting(),
        {
          provide: SessionService,
          useValue: { token }
        }
      ]
    });
  });

  it('uses the session token signal as the authorization source', () => {
    const http = TestBed.inject(HttpClient);
    const httpTesting = TestBed.inject(HttpTestingController);
    localStorage.setItem('pf.auth.token', 'stale-storage-token');
    token.set('session-token');

    http.get('/api/protected').subscribe();
    const req = httpTesting.expectOne('/api/protected');

    expect(req.request.headers.get('Authorization')).toBe('Bearer session-token');
    req.flush({});
    httpTesting.verify();
  });

  it('does not add Authorization when the session token is empty', () => {
    const http = TestBed.inject(HttpClient);
    const httpTesting = TestBed.inject(HttpTestingController);

    http.get('/api/public').subscribe();
    const req = httpTesting.expectOne('/api/public');

    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    httpTesting.verify();
  });
});
