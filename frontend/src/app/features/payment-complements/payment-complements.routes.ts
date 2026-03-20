import { Routes } from '@angular/router';

export const PAYMENT_COMPLEMENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/payment-complements-page.component').then((m) => m.PaymentComplementsPageComponent)
  }
];
