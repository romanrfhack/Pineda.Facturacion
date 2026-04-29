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
    url: '/app/orders',
    navigate: vi.fn()
  };

  beforeEach(() => {
    localStorage.clear();
    authApi.login.mockReset();
    authApi.getCurrentUser.mockReset();
    router.url = '/app/orders';
    router.navigate.mockReset();
    router.navigate.mockResolvedValue(true);

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

  it('clears token and current user after an unauthorized response', async () => {
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
    const tokenStorage = TestBed.inject(TokenStorageService);

    await service.login({ username: 'admin', password: 'secret' });
    await service.handleUnauthorized();

    expect(tokenStorage.getToken()).toBeNull();
    expect(service.token()).toBeNull();
    expect(service.currentUser().isAuthenticated).toBe(false);
    expect(service.currentUser().username).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('uses an auditor-safe default route', () => {
    authApi.login.mockReturnValue(of({
      outcome: 'Authenticated',
      isSuccess: true,
      token: 'jwt-token',
      user: {
        id: 2,
        username: 'auditor',
        displayName: 'Auditor',
        roles: [AppRole.Auditor],
        isAuthenticated: true
      }
    }));

    const service = TestBed.inject(SessionService);
    service.currentUser.set({
      id: 2,
      username: 'auditor',
      displayName: 'Auditor',
      roles: [AppRole.Auditor],
      isAuthenticated: true
    });

    expect(service.getDefaultAppRoute()).toBe('/app/audit');
  });
});
