import { Routes } from '@angular/router';

export const ORDER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/orders-operations-page.component').then((m) => m.OrdersOperationsPageComponent)
  }
];
