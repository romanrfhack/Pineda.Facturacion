import { inject } from '@angular/core';
import { CanMatchFn, Route, Router, UrlSegment } from '@angular/router';
import { AppRole } from './models';
import { PermissionService } from './permission.service';

export function roleGuard(roles: AppRole[]): CanMatchFn {
  return (_route: Route, _segments: UrlSegment[]) => {
    const permissionService = inject(PermissionService);
    const router = inject(Router);

    if (permissionService.hasAnyRole(roles)) {
      return true;
    }

    return router.createUrlTree(['/app/orders']);
  };
}
