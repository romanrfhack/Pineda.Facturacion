import { inject } from '@angular/core';
import { CanMatchFn, Route, Router, UrlSegment } from '@angular/router';
import { AppRole } from './models';
import { PermissionService } from './permission.service';
import { SessionService } from './session.service';

export function roleGuard(roles: AppRole[]): CanMatchFn {
  return (_route: Route, _segments: UrlSegment[]) => {
    const permissionService = inject(PermissionService);
    const sessionService = inject(SessionService);
    const router = inject(Router);

    if (permissionService.hasAnyRole(roles)) {
      return true;
    }

    return router.createUrlTree([sessionService.getDefaultAppRoute()]);
  };
}
