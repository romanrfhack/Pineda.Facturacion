import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { IssuerProfileApiService } from '../infrastructure/issuer-profile-api.service';
import { IssuerProfile, UpsertIssuerProfileRequest } from '../models/catalogs.models';

@Component({
  selector: 'app-issuer-profile-page',
  imports: [FormsModule],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos / Perfil del emisor</p>
        <h2>Perfil activo del emisor</h2>
      </header>

      @if (loading()) {
        <p class="helper">Cargando perfil del emisor...</p>
      } @else {
        <section class="card">
          <div class="indicator-grid">
            <p><strong>Referencia de certificado:</strong> {{ draft.hasCertificateReference ? 'Capturada' : 'Faltante' }}</p>
            <p><strong>Referencia de llave privada:</strong> {{ draft.hasPrivateKeyReference ? 'Capturada' : 'Faltante' }}</p>
            <p><strong>Referencia de contraseña:</strong> {{ draft.hasPrivateKeyPasswordReference ? 'Capturada' : 'Faltante' }}</p>
          </div>

          @if (issuer()) {
            <p class="helper">Los campos de referencia relacionados con secretos nunca se muestran. Vuelve a capturar las referencias explícitamente al actualizar el perfil del emisor.</p>
          }

          <form class="form-grid" (ngSubmit)="save()">
            <label><span>Razón social</span><input [(ngModel)]="draft.legalName" name="legalName" [disabled]="readOnly()" required /></label>
            <label><span>RFC</span><input [(ngModel)]="draft.rfc" name="rfc" [disabled]="readOnly()" required /></label>
            <label><span>Código de régimen fiscal</span><input [(ngModel)]="draft.fiscalRegimeCode" name="fiscalRegimeCode" [disabled]="readOnly()" required /></label>
            <label><span>Código postal</span><input [(ngModel)]="draft.postalCode" name="postalCode" [disabled]="readOnly()" required /></label>
            <label><span>CFDI version</span><input [(ngModel)]="draft.cfdiVersion" name="cfdiVersion" [disabled]="readOnly()" required /></label>
            <label><span>Ambiente PAC</span><input [(ngModel)]="draft.pacEnvironment" name="pacEnvironment" [disabled]="readOnly()" required /></label>
            <label><span>Referencia de certificado</span><input [(ngModel)]="draft.certificateReference" name="certificateReference" [disabled]="readOnly()" required /></label>
            <label><span>Referencia de llave privada</span><input [(ngModel)]="draft.privateKeyReference" name="privateKeyReference" [disabled]="readOnly()" required /></label>
            <label><span>Referencia de contraseña</span><input [(ngModel)]="draft.privateKeyPasswordReference" name="privateKeyPasswordReference" [disabled]="readOnly()" required /></label>
            <label class="checkbox">
              <input [(ngModel)]="draft.isActive" name="isActive" type="checkbox" [disabled]="readOnly()" />
              <span>Activo</span>
            </label>

            @if (errorMessage()) {
              <p class="error">{{ errorMessage() }}</p>
            }

            <button type="submit" [disabled]="saving() || readOnly()">{{ issuer() ? 'Actualizar perfil del emisor' : 'Crear perfil del emisor' }}</button>
          </form>

          @if (readOnly()) {
            <p class="helper">Tu rol puede consultar el perfil del emisor, pero no actualizarlo.</p>
          }
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { color:#5f6b76; margin:0; }
    .indicator-grid, .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IssuerProfilePageComponent {
  private readonly api = inject(IssuerProfileApiService);
  private readonly feedbackService = inject(FeedbackService);
  private readonly permissionService = inject(PermissionService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly issuer = signal<IssuerProfile | null>(null);

  protected draft = {
    legalName: '',
    rfc: '',
    fiscalRegimeCode: '',
    postalCode: '',
    cfdiVersion: '',
    certificateReference: '',
    privateKeyReference: '',
    privateKeyPasswordReference: '',
    pacEnvironment: '',
    isActive: true,
    hasCertificateReference: false,
    hasPrivateKeyReference: false,
    hasPrivateKeyPasswordReference: false
  };

  constructor() {
    void this.load();
  }

  protected readOnly(): boolean {
    return !this.permissionService.canWriteMasterData();
  }

  protected async save(): Promise<void> {
    if (this.readOnly()) {
      return;
    }

    if (this.issuer() && !window.confirm('¿Actualizar el perfil activo del emisor?')) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      const payload: UpsertIssuerProfileRequest = {
        legalName: this.draft.legalName.trim(),
        rfc: this.draft.rfc.trim(),
        fiscalRegimeCode: this.draft.fiscalRegimeCode.trim(),
        postalCode: this.draft.postalCode.trim(),
        cfdiVersion: this.draft.cfdiVersion.trim(),
        certificateReference: this.draft.certificateReference.trim(),
        privateKeyReference: this.draft.privateKeyReference.trim(),
        privateKeyPasswordReference: this.draft.privateKeyPasswordReference.trim(),
        pacEnvironment: this.draft.pacEnvironment.trim(),
        isActive: this.draft.isActive
      };

      const current = this.issuer();
      if (current) {
        await firstValueFrom(this.api.update(current.id, payload));
      } else {
        await firstValueFrom(this.api.create(payload));
      }

      this.feedbackService.show('success', current ? 'Perfil del emisor actualizado.' : 'Perfil del emisor creado.');
      await this.load();
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const issuer = await firstValueFrom(this.api.getActive());
      this.issuer.set(issuer);
      this.draft = {
        legalName: issuer.legalName,
        rfc: issuer.rfc,
        fiscalRegimeCode: issuer.fiscalRegimeCode,
        postalCode: issuer.postalCode,
        cfdiVersion: issuer.cfdiVersion,
        certificateReference: '',
        privateKeyReference: '',
        privateKeyPasswordReference: '',
        pacEnvironment: issuer.pacEnvironment,
        isActive: issuer.isActive,
        hasCertificateReference: issuer.hasCertificateReference,
        hasPrivateKeyReference: issuer.hasPrivateKeyReference,
        hasPrivateKeyPasswordReference: issuer.hasPrivateKeyPasswordReference
      };
    } catch (error) {
      this.issuer.set(null);
      this.errorMessage.set(extractApiErrorMessage(error, 'El perfil activo del emisor aún no está disponible.'));
    } finally {
      this.loading.set(false);
    }
  }
}
