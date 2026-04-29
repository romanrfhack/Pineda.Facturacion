import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AppRole } from './models';
import { PermissionService } from './permission.service';
import { roleGuard } from './role.guard';
import { SessionService } from './session.service';

describe('roleGuard', () => {
  const permissionService = {
    hasAnyRole: vi.fn()
  };

  const sessionService = {
    getDefaultAppRoute: vi.fn()
  };

  const router = {
    createUrlTree: vi.fn((commands: string[]) => `redirect:${commands.join('/')}`)
  };

  beforeEach(() => {
    permissionService.hasAnyRole.mockReset();
    sessionService.getDefaultAppRoute.mockReset();
    router.createUrlTree.mockClear();

    TestBed.configureTestingModule({
      providers: [
        { provide: PermissionService, useValue: permissionService },
        { provide: SessionService, useValue: sessionService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('allows users with at least one required role', () => {
    permissionService.hasAnyRole.mockReturnValue(true);

    const result = TestBed.runInInjectionContext(() =>
      roleGuard([AppRole.Admin])({} as never, []));

    expect(result).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects auditors to their permitted fallback instead of /app/orders', () => {
    permissionService.hasAnyRole.mockReturnValue(false);
    sessionService.getDefaultAppRoute.mockReturnValue('/app/audit');

    const result = TestBed.runInInjectionContext(() =>
      roleGuard([AppRole.Admin])({} as never, []));

    expect(sessionService.getDefaultAppRoute).toHaveBeenCalled();
    expect(router.createUrlTree).toHaveBeenCalledWith(['/app/audit']);
    expect(result).toBe('redirect:/app/audit');
  });

  it('redirects unauthorized users away from prohibited routes', () => {
    permissionService.hasAnyRole.mockReturnValue(false);
    sessionService.getDefaultAppRoute.mockReturnValue('/login');

    const result = TestBed.runInInjectionContext(() =>
      roleGuard([AppRole.FiscalSupervisor])({} as never, []));

    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    expect(result).toBe('redirect:/login');
    expect(result).not.toBe(true);
  });
});
