import { Routes } from '@angular/router';

export const ISSUED_CFDI_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/issued-cfdis-page.component').then((m) => m.IssuedCfdisPageComponent)
  }
];
