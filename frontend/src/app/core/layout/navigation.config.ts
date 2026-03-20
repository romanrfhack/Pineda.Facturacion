import { AppRole } from '../auth/models';

export interface NavigationItem {
  label: string;
  route: string;
  roles: AppRole[];
}

export const NAVIGATION_ITEMS: NavigationItem[] = [
  {
    label: 'Orders',
    route: '/app/orders',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Fiscal Documents',
    route: '/app/fiscal-documents',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Accounts Receivable',
    route: '/app/accounts-receivable',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Payment Complements',
    route: '/app/payment-complements',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Catalogs',
    route: '/app/catalogs',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Audit',
    route: '/app/audit',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.Auditor]
  }
];
