import { Routes } from '@angular/router';

export const FISCAL_DOCUMENT_ROUTES: Routes = [
  {
    path: 'open',
    loadComponent: () =>
      import('./pages/pending-fiscal-documents-page.component').then(
        (m) => m.PendingFiscalDocumentsPageComponent,
      ),
  },
  {
    path: '',
    loadComponent: () =>
      import('./pages/fiscal-document-operations-page.component').then(
        (m) => m.FiscalDocumentOperationsPageComponent,
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./pages/fiscal-document-operations-page.component').then(
        (m) => m.FiscalDocumentOperationsPageComponent,
      ),
  },
];
