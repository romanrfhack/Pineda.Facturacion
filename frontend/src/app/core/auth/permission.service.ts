import { Injectable, computed, inject } from '@angular/core';
import { AppRole } from './models';
import { SessionService } from './session.service';

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly sessionService = inject(SessionService);
  readonly roles = computed(() => this.sessionService.roles());

  hasAnyRole(roles: AppRole[]): boolean {
    return roles.some((role) => this.roles().includes(role));
  }

  canWriteMasterData(): boolean {
    return this.hasAnyRole([AppRole.Admin, AppRole.FiscalSupervisor]);
  }

  canReadCatalogs(): boolean {
    return this.hasAnyRole([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator, AppRole.Auditor]);
  }

  canApplyCatalogImports(): boolean {
    return this.canWriteMasterData();
  }

  canReadAudit(): boolean {
    return this.hasAnyRole([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.Auditor]);
  }

  canStampFiscal(): boolean {
    return this.hasAnyRole([AppRole.Admin, AppRole.FiscalSupervisor]);
  }

  canCancelFiscal(): boolean {
    return this.hasAnyRole([AppRole.Admin, AppRole.FiscalSupervisor]);
  }

  canManagePayments(): boolean {
    return this.hasAnyRole([AppRole.Admin, AppRole.FiscalSupervisor, AppRole.FiscalOperator]);
  }

  isReadOnlyAuditor(): boolean {
    return this.hasAnyRole([AppRole.Auditor]) && !this.canManagePayments() && !this.canStampFiscal();
  }
}
