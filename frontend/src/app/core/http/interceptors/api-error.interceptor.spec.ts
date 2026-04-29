import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { SessionService } from '../../auth/session.service';
import { buildApiUrl } from '../../config/api-url';
import { FeedbackService } from '../../ui/feedback.service';
import { apiErrorInterceptor } from './api-error.interceptor';

describe('apiErrorInterceptor', () => {
  const sessionService = {
    handleUnauthorized: vi.fn()
  };

  const feedbackService = {
    show: vi.fn()
  };

  beforeEach(() => {
    sessionService.handleUnauthorized.mockReset();
    sessionService.handleUnauthorized.mockResolvedValue(undefined);
    feedbackService.show.mockReset();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: SessionService, useValue: sessionService },
        { provide: FeedbackService, useValue: feedbackService }
      ]
    });
  });

  it('delegates protected 401 cleanup to SessionService', () => {
    const http = TestBed.inject(HttpClient);
    const httpTesting = TestBed.inject(HttpTestingController);

    http.get('/api/protected').subscribe({ error: () => undefined });
    const req = httpTesting.expectOne('/api/protected');
    req.flush({ errorMessage: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(sessionService.handleUnauthorized).toHaveBeenCalledTimes(1);
    httpTesting.verify();
  });

  it('does not treat login 401 as an expired session', () => {
    const http = TestBed.inject(HttpClient);
    const httpTesting = TestBed.inject(HttpTestingController);
    const loginUrl = buildApiUrl('/auth/login');

    http.post(loginUrl, { username: 'admin', password: 'wrong' }).subscribe({ error: () => undefined });
    const req = httpTesting.expectOne(loginUrl);
    req.flush({ errorMessage: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });

    expect(sessionService.handleUnauthorized).not.toHaveBeenCalled();
    expect(feedbackService.show).not.toHaveBeenCalled();
    httpTesting.verify();
  });

  it('keeps propagating the original error after interceptor side effects', async () => {
    const http = TestBed.inject(HttpClient);
    const httpTesting = TestBed.inject(HttpTestingController);

    const request = firstValueFrom(http.get('/api/server-error'));
    const req = httpTesting.expectOne('/api/server-error');
    req.flush({ errorMessage: 'Boom' }, { status: 500, statusText: 'Server Error' });

    await expect(request).rejects.toMatchObject({
      status: 500,
      error: { errorMessage: 'Boom' }
    });
    expect(feedbackService.show).toHaveBeenCalledTimes(1);
    httpTesting.verify();
  });
});
