import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { PermissionService } from './permission.service';
import { SessionService } from './session.service';
import { AppRole } from './models';

describe('PermissionService', () => {
  const roles = signal<AppRole[]>([]);

  beforeEach(() => {
    roles.set([]);

    TestBed.configureTestingModule({
      providers: [
        PermissionService,
        {
          provide: SessionService,
          useValue: {
            roles
          }
        }
      ]
    });
  });

  it('allows stamping for supervisors', () => {
    roles.set([AppRole.FiscalSupervisor]);
    const service = TestBed.inject(PermissionService);

    expect(service.canStampFiscal()).toBe(true);
    expect(service.canCancelFiscal()).toBe(true);
  });

  it('keeps auditors read-only', () => {
    roles.set([AppRole.Auditor]);
    const service = TestBed.inject(PermissionService);

    expect(service.isReadOnlyAuditor()).toBe(true);
    expect(service.canManagePayments()).toBe(false);
    expect(service.canReadAudit()).toBe(true);
  });
});
