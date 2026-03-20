import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { SessionService } from './session.service';
import { AuthApiService } from './auth-api.service';
import { TokenStorageService } from './token-storage.service';
import { FeedbackService } from '../ui/feedback.service';
import { AppRole } from './models';

describe('SessionService', () => {
  const authApi = {
    login: vi.fn(),
    getCurrentUser: vi.fn()
  };

  const router = {
    navigate: vi.fn()
  };

  beforeEach(() => {
    localStorage.clear();
    authApi.login.mockReset();
    authApi.getCurrentUser.mockReset();
    router.navigate.mockReset();

    TestBed.configureTestingModule({
      providers: [
        SessionService,
        TokenStorageService,
        FeedbackService,
        { provide: AuthApiService, useValue: authApi },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('stores token and current user after a successful login', async () => {
    authApi.login.mockReturnValue(of({
      outcome: 'Authenticated',
      isSuccess: true,
      token: 'jwt-token',
      user: {
        id: 1,
        username: 'admin',
        displayName: 'Admin',
        roles: [AppRole.Admin],
        isAuthenticated: true
      }
    }));

    const service = TestBed.inject(SessionService);
    const response = await service.login({ username: 'admin', password: 'secret' });

    expect(response.isSuccess).toBe(true);
    expect(service.token()).toBe('jwt-token');
    expect(service.currentUser().username).toBe('admin');
    expect(service.roles()).toEqual([AppRole.Admin]);
  });

  it('maps login 401 to an invalid-credentials response', async () => {
    authApi.login.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));

    const service = TestBed.inject(SessionService);
    const response = await service.login({ username: 'admin', password: 'wrong' });

    expect(response.isSuccess).toBe(false);
    expect(response.outcome).toBe('InvalidCredentials');
  });
});
