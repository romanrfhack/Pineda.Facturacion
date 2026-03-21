import { ChangeDetectionStrategy, Component, OnChanges, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FiscalReceiver, UpsertFiscalReceiverRequest } from '../models/catalogs.models';

@Component({
  selector: 'app-fiscal-receiver-form',
  imports: [FormsModule],
  template: `
    <form class="form-grid" (ngSubmit)="submitForm()">
      <label>
        <span>RFC</span>
        <input [(ngModel)]="draft.rfc" name="rfc" required />
      </label>

      <label>
        <span>Razón social</span>
        <input [(ngModel)]="draft.legalName" name="legalName" required />
      </label>

      <label>
        <span>Código de régimen fiscal</span>
        <input [(ngModel)]="draft.fiscalRegimeCode" name="fiscalRegimeCode" required />
      </label>

      <label>
        <span>Uso CFDI predeterminado</span>
        <input [(ngModel)]="draft.cfdiUseCodeDefault" name="cfdiUseCodeDefault" required />
      </label>

      <label>
        <span>Código postal</span>
        <input [(ngModel)]="draft.postalCode" name="postalCode" required />
      </label>

      <label>
        <span>Código de país</span>
        <input [(ngModel)]="draft.countryCode" name="countryCode" />
      </label>

      <label>
        <span>Registro fiscal extranjero</span>
        <input [(ngModel)]="draft.foreignTaxRegistration" name="foreignTaxRegistration" />
      </label>

      <label>
        <span>Email</span>
        <input [(ngModel)]="draft.email" name="email" type="email" />
      </label>

      <label>
        <span>Teléfono</span>
        <input [(ngModel)]="draft.phone" name="phone" />
      </label>

      <label>
        <span>Alias de búsqueda</span>
        <input [(ngModel)]="draft.searchAlias" name="searchAlias" />
      </label>

      <label class="checkbox">
        <input [(ngModel)]="draft.isActive" name="isActive" type="checkbox" />
        <span>Activo</span>
      </label>

      @if (errorMessage()) {
        <p class="error">{{ errorMessage() }}</p>
      }

      <button type="submit" [disabled]="readOnly()">{{ submitLabel() }}</button>
    </form>
  `,
  styles: [`
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalReceiverFormComponent implements OnChanges {
  readonly receiver = input<FiscalReceiver | null>(null);
  readonly submitLabel = input('Guardar receptor');
  readonly readOnly = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly submitted = output<UpsertFiscalReceiverRequest>();

  protected draft: UpsertFiscalReceiverRequest = emptyReceiver();

  ngOnChanges(): void {
    const receiver = this.receiver();
    this.draft = receiver
      ? {
          rfc: receiver.rfc,
          legalName: receiver.legalName,
          fiscalRegimeCode: receiver.fiscalRegimeCode,
          cfdiUseCodeDefault: receiver.cfdiUseCodeDefault,
          postalCode: receiver.postalCode,
          countryCode: receiver.countryCode,
          foreignTaxRegistration: receiver.foreignTaxRegistration,
          email: receiver.email,
          phone: receiver.phone,
          searchAlias: receiver.searchAlias,
          isActive: receiver.isActive
        }
      : emptyReceiver();
  }

  protected submitForm(): void {
    this.submitted.emit({
      ...this.draft,
      rfc: this.draft.rfc.trim(),
      legalName: this.draft.legalName.trim(),
      fiscalRegimeCode: this.draft.fiscalRegimeCode.trim(),
      cfdiUseCodeDefault: this.draft.cfdiUseCodeDefault.trim(),
      postalCode: this.draft.postalCode.trim(),
      countryCode: this.draft.countryCode?.trim() || null,
      foreignTaxRegistration: this.draft.foreignTaxRegistration?.trim() || null,
      email: this.draft.email?.trim() || null,
      phone: this.draft.phone?.trim() || null,
      searchAlias: this.draft.searchAlias?.trim() || null
    });
  }
}

function emptyReceiver(): UpsertFiscalReceiverRequest {
  return {
    rfc: '',
    legalName: '',
    fiscalRegimeCode: '',
    cfdiUseCodeDefault: '',
    postalCode: '',
    countryCode: 'MX',
    foreignTaxRegistration: '',
    email: '',
    phone: '',
    searchAlias: '',
    isActive: true
  };
}
