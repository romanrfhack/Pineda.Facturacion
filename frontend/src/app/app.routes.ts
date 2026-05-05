import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';
import { AppRole } from './core/auth/models';
import { SessionService } from './core/auth/session.service';

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
        redirectTo: () => inject(SessionService).getDefaultAppRoute()
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
        path: 'issued-cfdis',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor])],
        loadChildren: () => import('./features/issued-cfdis/issued-cfdis.routes').then((m) => m.ISSUED_CFDI_ROUTES)
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
      },
      {
        path: 'reports',
        canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.Auditor])],
        loadChildren: () => import('./features/reports/reports.routes').then((m) => m.REPORT_ROUTES)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];
