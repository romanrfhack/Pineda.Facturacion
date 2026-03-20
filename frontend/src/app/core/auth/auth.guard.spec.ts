import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { SessionService } from './session.service';

describe('authGuard', () => {
  const sessionService = {
    isAuthenticated: vi.fn()
  };

  const router = {
    createUrlTree: vi.fn().mockReturnValue('redirect-login')
  };

  beforeEach(() => {
    sessionService.isAuthenticated.mockReset();
    router.createUrlTree.mockClear();

    TestBed.configureTestingModule({
      providers: [
        { provide: SessionService, useValue: sessionService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('allows authenticated users', () => {
    sessionService.isAuthenticated.mockReturnValue(true);

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('redirects anonymous users to login', () => {
    sessionService.isAuthenticated.mockReturnValue(false);

    const result = TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));

    expect(result).toBe('redirect-login');
  });
});
