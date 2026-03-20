import { Routes } from '@angular/router';

export const FISCAL_DOCUMENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/fiscal-document-operations-page.component').then((m) => m.FiscalDocumentOperationsPageComponent)
  },
  {
    path: ':id',
    loadComponent: () => import('./pages/fiscal-document-operations-page.component').then((m) => m.FiscalDocumentOperationsPageComponent)
  }
];
