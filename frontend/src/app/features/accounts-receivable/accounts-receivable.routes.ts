import { Routes } from '@angular/router';

export const ACCOUNTS_RECEIVABLE_ROUTES: Routes = [
  {
    path: 'new-payment',
    loadComponent: () =>
      import('./pages/accounts-receivable-new-payment-page.component').then(
        (m) => m.AccountsReceivableNewPaymentPageComponent,
      ),
  },
  {
    path: '',
    loadComponent: () =>
      import('./pages/accounts-receivable-page.component').then(
        (m) => m.AccountsReceivablePageComponent,
      ),
  },
];
