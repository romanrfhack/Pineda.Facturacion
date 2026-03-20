import { Routes } from '@angular/router';

export const AUDIT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/audit-events-page.component').then((m) => m.AuditEventsPageComponent)
  }
];
