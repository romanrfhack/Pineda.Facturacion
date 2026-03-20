import { Routes } from '@angular/router';

export const ACCOUNTS_RECEIVABLE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/accounts-receivable-page.component').then((m) => m.AccountsReceivablePageComponent)
  }
];
