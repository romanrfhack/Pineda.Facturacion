import { AppRole } from '../../core/auth/models';

const DISPLAY_LABELS: Record<string, string> = {
  Admin: 'Administrador',
  FiscalSupervisor: 'Supervisor fiscal',
  FiscalOperator: 'Operador fiscal',
  Auditor: 'Auditor',
  Authenticated: 'Autenticado',
  Anonymous: 'Anónimo',
  Draft: 'Borrador',
  ReadyForStamping: 'Listo para timbrar',
  StampingRequested: 'Timbrado solicitado',
  Stamped: 'Timbrado',
  Succeeded: 'Exitoso',
  StampingRejected: 'Timbrado rechazado',
  CancellationRequested: 'Cancelación solicitada',
  Cancelled: 'Cancelado',
  CancellationRejected: 'Cancelación rechazada',
  Requested: 'Solicitado',
  Rejected: 'Rechazado',
  Unavailable: 'No disponible',
  Open: 'Abierto',
  PartiallyPaid: 'Parcialmente pagado',
  Paid: 'Pagado',
  Overpaid: 'Sobrepagado',
  Valid: 'Válido',
  Validated: 'Validado',
  Invalid: 'Inválido',
  Ignored: 'Ignorado',
  Create: 'Crear',
  Update: 'Actualizar',
  Conflict: 'Conflicto',
  NeedsEnrichment: 'Requiere complemento',
  AlreadyApplied: 'Ya aplicado',
  Failed: 'Falló',
  Skipped: 'Omitido',
  Created: 'Creado',
  Updated: 'Actualizado',
  Imported: 'Importado',
  Applied: 'Aplicado',
  Refreshed: 'Actualizado',
  NotFound: 'No encontrado',
  ValidationFailed: 'Validación fallida',
  ProviderRejected: 'Rechazado por el PAC',
  ProviderUnavailable: 'PAC no disponible',
  InvalidCredentials: 'Credenciales inválidas',
  CreateOnly: 'Solo crear',
  CreateAndUpdate: 'Crear y actualizar',
  Pending: 'Pendiente',
  Missing: 'Faltante',
  Present: 'Capturada',
  Active: 'Activo',
  Inactive: 'Inactivo',
  Eligible: 'Elegible',
  Blocked: 'Bloqueado',
  Ineligible: 'No elegible',
  Yes: 'Sí',
  No: 'No',
  None: 'Ninguno',
  Unknown: 'Desconocido'
};

export function getDisplayLabel(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  return DISPLAY_LABELS[value] ?? humanizeIdentifier(value);
}

export function getRoleDisplayLabel(role: AppRole | string | null | undefined): string {
  return getDisplayLabel(role ?? '');
}

function humanizeIdentifier(value: string): string {
  return value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[._-]+/g, ' ')
    .trim();
}
