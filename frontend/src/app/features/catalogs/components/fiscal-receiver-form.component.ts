import { ChangeDetectionStrategy, Component, OnChanges, SimpleChanges, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FiscalReceiver, FiscalReceiverSpecialFieldDefinition, UpsertFiscalReceiverRequest } from '../models/catalogs.models';

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

      <section class="special-fields">
        <div class="special-fields-header">
          <div>
            <strong>Campos especiales de facturación</strong>
            <p>Define qué datos adicionales debe capturar este receptor al facturar.</p>
          </div>
          @if (!readOnly()) {
            <button type="button" class="secondary-action" (click)="addSpecialField()">Agregar campo</button>
          }
        </div>

        @if (!draft.specialFields.length) {
          <p class="helper">Este receptor no tiene campos especiales configurados.</p>
        } @else {
          <div class="special-field-list">
            @for (field of draft.specialFields; track trackSpecialField($index, field); let index = $index) {
              <section class="special-field-card">
                <div class="special-field-grid">
                  <label>
                    <span>Clave interna</span>
                    <input [(ngModel)]="field.code" [name]="'field-code-' + index" [disabled]="readOnly()" required />
                  </label>

                  <label>
                    <span>Nombre visible</span>
                    <input [(ngModel)]="field.label" [name]="'field-label-' + index" [disabled]="readOnly()" required />
                  </label>

                  <label>
                    <span>Tipo de dato</span>
                    <select [(ngModel)]="field.dataType" [name]="'field-data-type-' + index" [disabled]="readOnly()">
                      <option value="text">Texto</option>
                      <option value="number">Número</option>
                      <option value="date">Fecha</option>
                    </select>
                  </label>

                  <label>
                    <span>Longitud máxima</span>
                    <input [(ngModel)]="field.maxLength" [name]="'field-max-length-' + index" [disabled]="readOnly()" type="number" min="1" />
                  </label>

                  <label>
                    <span>Orden</span>
                    <input [(ngModel)]="field.displayOrder" [name]="'field-order-' + index" [disabled]="readOnly()" type="number" min="1" />
                  </label>

                  <label>
                    <span>Texto de ayuda</span>
                    <input [(ngModel)]="field.helpText" [name]="'field-help-text-' + index" [disabled]="readOnly()" />
                  </label>

                  <label class="checkbox">
                    <input [(ngModel)]="field.isRequired" [name]="'field-required-' + index" [disabled]="readOnly()" type="checkbox" />
                    <span>Requerido</span>
                  </label>

                  <label class="checkbox">
                    <input [(ngModel)]="field.isActive" [name]="'field-active-' + index" [disabled]="readOnly()" type="checkbox" />
                    <span>Activo</span>
                  </label>
                </div>

                @if (!readOnly()) {
                  <button type="button" class="link danger" (click)="removeSpecialField(index)">Quitar campo</button>
                }
              </section>
            }
          </div>
        }
      </section>

      @if (errorMessage()) {
        <p class="error">{{ errorMessage() }}</p>
      }

      <button type="submit" [disabled]="readOnly() || submitting()">{{ submitting() ? 'Guardando...' : submitLabel() }}</button>
    </form>
  `,
  styles: [`
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, button, select { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
    .special-fields { grid-column:1 / -1; display:grid; gap:0.75rem; padding-top:0.25rem; }
    .special-fields-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .special-fields-header p, .helper { margin:0; color:#5f6b76; }
    .special-field-list { display:grid; gap:0.75rem; }
    .special-field-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:0.9rem; background:#fcfbf8; display:grid; gap:0.75rem; }
    .special-field-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; align-items:end; }
    .secondary-action { background:#d8c49b; color:#182533; }
    .link { background:transparent; color:#182533; padding:0; justify-self:flex-start; }
    .link.danger { color:#7a2020; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalReceiverFormComponent implements OnChanges {
  readonly receiver = input<FiscalReceiver | null>(null);
  readonly initialValue = input<UpsertFiscalReceiverRequest | null>(null);
  readonly submitLabel = input('Guardar receptor');
  readonly readOnly = input(false);
  readonly submitting = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly submitted = output<UpsertFiscalReceiverRequest>();

  protected draft: UpsertFiscalReceiverRequest = emptyReceiver();

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['receiver'] && !changes['initialValue']) {
      return;
    }

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
          isActive: receiver.isActive,
          specialFields: (receiver.specialFields ?? []).map(cloneSpecialField)
      }
      : cloneDraft(this.initialValue() ?? emptyReceiver());
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
      searchAlias: this.draft.searchAlias?.trim() || null,
      specialFields: this.draft.specialFields.map((field, index) => ({
        ...field,
        code: field.code.trim(),
        label: field.label.trim(),
        dataType: field.dataType?.trim() || 'text',
        helpText: field.helpText?.trim() || null,
        maxLength: field.maxLength ? Number(field.maxLength) : null,
        displayOrder: field.displayOrder || index + 1
      }))
    });
  }

  protected addSpecialField(): void {
    this.draft = {
      ...this.draft,
      specialFields: [...this.draft.specialFields, emptySpecialField(this.draft.specialFields.length + 1)]
    };
  }

  protected removeSpecialField(index: number): void {
    this.draft = {
      ...this.draft,
      specialFields: this.draft.specialFields.filter((_, currentIndex) => currentIndex !== index)
    };
  }

  protected trackSpecialField(index: number, field: FiscalReceiverSpecialFieldDefinition): string {
    return field.code || `new-${index}`;
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
    isActive: true,
    specialFields: []
  };
}

function cloneDraft(draft: UpsertFiscalReceiverRequest): UpsertFiscalReceiverRequest {
  return {
    rfc: draft.rfc,
    legalName: draft.legalName,
    fiscalRegimeCode: draft.fiscalRegimeCode,
    cfdiUseCodeDefault: draft.cfdiUseCodeDefault,
    postalCode: draft.postalCode,
    countryCode: draft.countryCode ?? null,
    foreignTaxRegistration: draft.foreignTaxRegistration ?? null,
    email: draft.email ?? null,
    phone: draft.phone ?? null,
    searchAlias: draft.searchAlias ?? null,
    isActive: draft.isActive,
    specialFields: (draft.specialFields ?? []).map(cloneSpecialField)
  };
}

function emptySpecialField(displayOrder: number): FiscalReceiverSpecialFieldDefinition {
  return {
    code: '',
    label: '',
    dataType: 'text',
    maxLength: null,
    helpText: '',
    isRequired: false,
    isActive: true,
    displayOrder
  };
}

function cloneSpecialField(field: FiscalReceiverSpecialFieldDefinition): FiscalReceiverSpecialFieldDefinition {
  return {
    id: field.id ?? null,
    fiscalReceiverId: field.fiscalReceiverId ?? null,
    code: field.code,
    label: field.label,
    dataType: field.dataType,
    maxLength: field.maxLength ?? null,
    helpText: field.helpText ?? null,
    isRequired: field.isRequired,
    isActive: field.isActive,
    displayOrder: field.displayOrder
  };
}
