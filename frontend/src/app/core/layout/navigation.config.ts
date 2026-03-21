import { AppRole } from '../auth/models';

export interface NavigationItem {
  label: string;
  route: string;
  roles: AppRole[];
}

export const NAVIGATION_ITEMS: NavigationItem[] = [
  {
    label: 'Órdenes',
    route: '/app/orders',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Documentos fiscales',
    route: '/app/fiscal-documents',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Cuentas por cobrar',
    route: '/app/accounts-receivable',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Complementos de pago',
    route: '/app/payment-complements',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Catálogos',
    route: '/app/catalogs',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Auditoría',
    route: '/app/audit',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.Auditor]
  }
];
