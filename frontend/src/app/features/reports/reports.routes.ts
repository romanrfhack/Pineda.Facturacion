import { Routes } from '@angular/router';

export const REPORT_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'stamped-legacy-notes'
  },
  {
    path: 'stamped-legacy-notes',
    loadComponent: () => import('./pages/stamped-legacy-notes-report-page.component').then((m) => m.StampedLegacyNotesReportPageComponent)
  }
];
