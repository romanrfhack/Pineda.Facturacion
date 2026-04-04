import { AppRole } from '../auth/models';

export interface NavigationItem {
  label: string;
  iconText: string;
  route: string;
  roles: AppRole[];
}

export const NAVIGATION_ITEMS: NavigationItem[] = [
  {
    label: 'Órdenes',
    iconText: 'OR',
    route: '/app/orders',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Documentos fiscales',
    iconText: 'FD',
    route: '/app/fiscal-documents',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'CFDI emitidos',
    iconText: 'CF',
    route: '/app/issued-cfdis',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Cuentas por cobrar',
    iconText: 'CC',
    route: '/app/accounts-receivable',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Complementos de pago',
    iconText: 'RP',
    route: '/app/payment-complements',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Catálogos',
    iconText: 'CT',
    route: '/app/catalogs',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]
  },
  {
    label: 'Auditoría',
    iconText: 'AU',
    route: '/app/audit',
    roles: [AppRole.Admin, AppRole.FiscalSupervisor, AppRole.Auditor]
  }
];
