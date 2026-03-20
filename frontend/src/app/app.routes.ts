import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';
import { AppRole } from './core/auth/models';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'login'
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/pages/login-page.component').then((m) => m.LoginPageComponent)
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/app-shell.component').then((m) => m.AppShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'orders'
      },
      {
        path: 'orders',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator])],
        loadChildren: () => import('./features/orders/orders.routes').then((m) => m.ORDER_ROUTES)
      },
      {
        path: 'fiscal-documents',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor])],
        loadChildren: () => import('./features/fiscal-documents/fiscal-documents.routes').then((m) => m.FISCAL_DOCUMENT_ROUTES)
      },
      {
        path: 'accounts-receivable',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor])],
        loadChildren: () => import('./features/accounts-receivable/accounts-receivable.routes').then((m) => m.ACCOUNTS_RECEIVABLE_ROUTES)
      },
      {
        path: 'payment-complements',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor])],
        loadChildren: () => import('./features/payment-complements/payment-complements.routes').then((m) => m.PAYMENT_COMPLEMENT_ROUTES)
      },
      {
        path: 'catalogs',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor])],
        loadChildren: () => import('./features/catalogs/catalogs.routes').then((m) => m.CATALOG_ROUTES)
      },
      {
        path: 'audit',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.Auditor])],
        loadChildren: () => import('./features/audit/audit.routes').then((m) => m.AUDIT_ROUTES)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];
