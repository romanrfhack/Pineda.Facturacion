import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { IssuerProfileApiService } from '../infrastructure/issuer-profile-api.service';
import { IssuerProfile, UpsertIssuerProfileRequest } from '../models/catalogs.models';

const MAX_LOGO_FILE_SIZE_BYTES = 1_048_576;
const ALLOWED_LOGO_CONTENT_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp']);

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
            <p><strong>Logotipo:</strong> {{ hasLogoConfigured() ? 'Capturado' : 'Sin cargar' }}</p>
          </div>

          @if (issuer()) {
            <p class="helper">Los campos de referencia relacionados con secretos nunca se muestran. Vuelve a capturar las referencias explícitamente al actualizar el perfil del emisor.</p>
          }

          <form class="form-grid" (ngSubmit)="save()">
            <label><span>Razón social</span><input [(ngModel)]="draft.legalName" name="legalName" [disabled]="readOnly()" required /></label>
            <label><span>RFC</span><input [(ngModel)]="draft.rfc" name="rfc" [disabled]="readOnly()" required /></label>
            <label><span>Código de régimen fiscal</span><input [(ngModel)]="draft.fiscalRegimeCode" name="fiscalRegimeCode" [disabled]="readOnly()" required /></label>
            <label><span>Código postal</span><input [(ngModel)]="draft.postalCode" name="postalCode" [disabled]="readOnly()" required /></label>
            <label><span>Versión CFDI</span><input [(ngModel)]="draft.cfdiVersion" name="cfdiVersion" [disabled]="readOnly()" required /></label>
            <label><span>Ambiente PAC</span><input [(ngModel)]="draft.pacEnvironment" name="pacEnvironment" [disabled]="readOnly()" required /></label>
            <label><span>Referencia de certificado</span><input [(ngModel)]="draft.certificateReference" name="certificateReference" [disabled]="readOnly()" required /></label>
            <label><span>Referencia de llave privada</span><input [(ngModel)]="draft.privateKeyReference" name="privateKeyReference" [disabled]="readOnly()" required /></label>
            <label><span>Referencia de contraseña</span><input [(ngModel)]="draft.privateKeyPasswordReference" name="privateKeyPasswordReference" [disabled]="readOnly()" required /></label>

            <section class="logo-card">
              <div class="logo-copy">
                <span class="logo-title">Logotipo del emisor</span>
                <p class="helper">Formatos permitidos: PNG, JPG, JPEG o WEBP. Tamaño máximo: 1 MB.</p>
                @if (removeLogoRequested()) {
                  <p class="helper">El logotipo se eliminará al guardar el perfil.</p>
                } @else if (logoFileName()) {
                  <p class="helper">Archivo seleccionado: {{ logoFileName() }}</p>
                }
              </div>

              <div class="logo-preview-shell">
                @if (loadingLogo()) {
                  <p class="helper">Cargando logotipo...</p>
                } @else if (logoPreviewUrl()) {
                  <img class="logo-preview" [src]="logoPreviewUrl() ?? ''" alt="Vista previa del logotipo del emisor" />
                } @else {
                  <div class="logo-empty">Sin logotipo cargado</div>
                }
              </div>

              <div class="logo-actions">
                <label class="file-button" [class.disabled]="saving() || readOnly()">
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    [disabled]="saving() || readOnly()"
                    (change)="onLogoSelected($event)" />
                  <span>{{ hasLogoConfigured() ? 'Reemplazar logotipo' : 'Subir logotipo' }}</span>
                </label>

                @if (hasLogoConfigured()) {
                  <button type="button" class="secondary" (click)="removeLogo()" [disabled]="saving() || readOnly()">Quitar logotipo</button>
                }
              </div>

              @if (logoError()) {
                <p class="error">{{ logoError() }}</p>
              }
            </section>

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
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; display:grid; gap:1rem; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { color:#5f6b76; margin:0; }
    .indicator-grid, .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    .secondary { background:#e8ecef; color:#182533; }
    .logo-card { grid-column:1 / -1; display:grid; gap:1rem; padding:1rem; border:1px dashed #c9d1da; border-radius:1rem; background:#fcfbf7; }
    .logo-title { font-weight:700; color:#182533; }
    .logo-copy { display:grid; gap:0.35rem; }
    .logo-preview-shell { min-height:140px; display:flex; align-items:center; justify-content:center; border:1px solid #d8d1c2; border-radius:0.85rem; background:#fff; padding:0.75rem; }
    .logo-preview { max-width:100%; max-height:160px; object-fit:contain; }
    .logo-empty { color:#5f6b76; text-align:center; }
    .logo-actions { display:flex; flex-wrap:wrap; gap:0.75rem; }
    .file-button { display:inline-flex; align-items:center; justify-content:center; border-radius:0.8rem; background:#182533; color:#fff; padding:0.75rem 1rem; cursor:pointer; }
    .file-button input { display:none; }
    .file-button.disabled { opacity:0.6; cursor:not-allowed; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IssuerProfilePageComponent implements OnDestroy {
  private readonly api = inject(IssuerProfileApiService);
  private readonly feedbackService = inject(FeedbackService);
  private readonly permissionService = inject(PermissionService);
  private previewObjectUrl: string | null = null;

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly loadingLogo = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly logoError = signal<string | null>(null);
  protected readonly issuer = signal<IssuerProfile | null>(null);
  protected readonly logoPreviewUrl = signal<string | null>(null);
  protected readonly logoFileName = signal<string | null>(null);
  protected readonly selectedLogoFile = signal<File | null>(null);
  protected readonly removeLogoRequested = signal(false);

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

  ngOnDestroy(): void {
    this.revokePreviewUrl();
  }

  protected readOnly(): boolean {
    return !this.permissionService.canWriteMasterData();
  }

  protected hasLogoConfigured(): boolean {
    return !!this.logoPreviewUrl() || !!this.selectedLogoFile() || (!!this.issuer()?.hasLogo && !this.removeLogoRequested());
  }

  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;
    if (input) {
      input.value = '';
    }

    if (!file) {
      return;
    }

    const validationError = validateLogoFile(file);
    if (validationError) {
      this.logoError.set(validationError);
      return;
    }

    this.logoError.set(null);
    this.removeLogoRequested.set(false);
    this.selectedLogoFile.set(file);
    this.logoFileName.set(file.name);
    this.setPreviewObjectUrl(URL.createObjectURL(file));
  }

  protected removeLogo(): void {
    this.logoError.set(null);
    this.selectedLogoFile.set(null);
    this.logoFileName.set(null);
    this.removeLogoRequested.set(true);
    this.setPreviewObjectUrl(null);
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
    let issuerId = current?.id ?? null;
    let profileSaved = false;

    try {
      const mutation = current
        ? await firstValueFrom(this.api.update(current.id, payload))
        : await firstValueFrom(this.api.create(payload));

      issuerId = mutation.id ?? issuerId;
      profileSaved = true;

      if (issuerId && this.selectedLogoFile()) {
        await firstValueFrom(this.api.uploadLogo(issuerId, this.selectedLogoFile()!));
      } else if (issuerId && this.removeLogoRequested() && current?.hasLogo) {
        await firstValueFrom(this.api.removeLogo(issuerId));
      }

      this.feedbackService.show('success', current ? 'Perfil del emisor actualizado.' : 'Perfil del emisor creado.');
      await this.load();
    } catch (error) {
      if (profileSaved && issuerId && !this.issuer()) {
        this.issuer.set({
          id: issuerId,
          legalName: payload.legalName,
          rfc: payload.rfc,
          fiscalRegimeCode: payload.fiscalRegimeCode,
          postalCode: payload.postalCode,
          cfdiVersion: payload.cfdiVersion,
          hasCertificateReference: payload.certificateReference.length > 0,
          hasPrivateKeyReference: payload.privateKeyReference.length > 0,
          hasPrivateKeyPasswordReference: payload.privateKeyPasswordReference.length > 0,
          hasLogo: false,
          logoFileName: null,
          logoUpdatedAtUtc: null,
          pacEnvironment: payload.pacEnvironment,
          isActive: payload.isActive,
          createdAtUtc: new Date().toISOString(),
          updatedAtUtc: new Date().toISOString()
        });
      }

      this.errorMessage.set(
        extractApiErrorMessage(
          error,
          profileSaved
            ? 'El perfil se guardó, pero no fue posible actualizar el logotipo.'
            : 'No fue posible guardar el perfil del emisor.'
        )
      );
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.loadingLogo.set(false);
    this.errorMessage.set(null);
    this.logoError.set(null);

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

      this.selectedLogoFile.set(null);
      this.removeLogoRequested.set(false);
      this.logoFileName.set(issuer.logoFileName ?? null);

      if (issuer.hasLogo) {
        await this.loadLogoPreview(issuer.id, issuer.logoFileName ?? 'logo');
      } else {
        this.setPreviewObjectUrl(null);
      }
    } catch (error) {
      this.issuer.set(null);
      this.setPreviewObjectUrl(null);
      this.logoFileName.set(null);
      this.selectedLogoFile.set(null);
      this.removeLogoRequested.set(false);
      this.errorMessage.set(extractApiErrorMessage(error, 'El perfil activo del emisor aún no está disponible.'));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadLogoPreview(issuerId: number, fileName: string): Promise<void> {
    this.loadingLogo.set(true);
    try {
      const blob = await firstValueFrom(this.api.getLogo(issuerId));
      this.logoError.set(null);
      this.logoFileName.set(fileName);
      this.setPreviewObjectUrl(URL.createObjectURL(blob));
    } catch (error) {
      this.setPreviewObjectUrl(null);
      this.logoError.set(extractApiErrorMessage(error, 'No fue posible cargar el logotipo del emisor.'));
    } finally {
      this.loadingLogo.set(false);
    }
  }

  private setPreviewObjectUrl(url: string | null): void {
    this.revokePreviewUrl();
    this.previewObjectUrl = url;
    this.logoPreviewUrl.set(url);
  }

  private revokePreviewUrl(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
  }
}

function validateLogoFile(file: File): string | null {
  if (!ALLOWED_LOGO_CONTENT_TYPES.has(file.type)) {
    return 'Solo se permiten imágenes PNG, JPG, JPEG o WEBP.';
  }

  if (file.size <= 0) {
    return 'El archivo del logotipo no puede estar vacío.';
  }

  if (file.size > MAX_LOGO_FILE_SIZE_BYTES) {
    return 'El logotipo supera el tamaño máximo permitido de 1 MB.';
  }

  return null;
}
