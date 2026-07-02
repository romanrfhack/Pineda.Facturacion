import { ChangeDetectionStrategy, Component, OnChanges, SimpleChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { FiscalReceiverSatCatalogService } from '../application/fiscal-receiver-sat-catalog.service';
import { FiscalReceiver, FiscalReceiverSatCatalog, FiscalReceiverSatCatalogOption, FiscalReceiverSpecialFieldDefinition, UpsertFiscalReceiverRequest } from '../models/catalogs.models';
import {
  findInvalidEmailRecipients,
  isValidEmailRecipient,
  joinEmailRecipients,
  parseEmailRecipients,
  splitEmailRecipients,
} from '../../../shared/utils/email-recipients';

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
        <span>Régimen fiscal del receptor</span>
        <select [ngModel]="draft.fiscalRegimeCode" name="fiscalRegimeCode" [disabled]="readOnly() || !catalogReady()" required (ngModelChange)="updateFiscalRegime($event)">
          <option value="">Selecciona un régimen fiscal</option>
          @for (option of fiscalRegimeOptions(); track option.code) {
            <option [value]="option.code">{{ option.description }}</option>
          }
        </select>
        @if (!catalogReady()) {
          <small class="helper">Cargando catálogo SAT...</small>
        }
      </label>

      <label>
        <span>Uso CFDI predeterminado</span>
        <select [(ngModel)]="draft.cfdiUseCodeDefault" name="cfdiUseCodeDefault" [disabled]="readOnly() || !catalogReady() || !draft.fiscalRegimeCode.trim()" required>
          <option value="">{{ draft.fiscalRegimeCode.trim() ? 'Selecciona un uso CFDI' : 'Primero selecciona régimen fiscal' }}</option>
          @for (option of cfdiUseOptions(); track option.code) {
            <option [value]="option.code">{{ option.description }}</option>
          }
        </select>
        @if (draft.fiscalRegimeCode.trim() && !cfdiUseOptions().length) {
          <small class="helper">No hay usos CFDI compatibles disponibles para el régimen seleccionado.</small>
        }
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

      <section class="email-field wide-field">
        <div class="email-field-header">
          <span>Correo(s)</span>
          <small class="helper">Para varios correos, agrégalos uno por uno o pégalos separados con punto y coma (;).</small>
        </div>

        <div class="email-entry-row">
          <textarea
            id="emailRecipientsInput"
            [(ngModel)]="emailInput"
            name="emailInput"
            data-testid="email-recipient-input"
            rows="3"
            [disabled]="readOnly()"
            placeholder="correo@cliente.com"
          ></textarea>

          @if (!readOnly()) {
            <button
              type="button"
              class="secondary-action email-add-button"
              data-testid="add-email-recipient-button"
              (click)="addEmailRecipients()"
            >
              Agregar correo
            </button>
          }
        </div>

        @if (invalidEmailRecipients.length > 0) {
          <p class="helper warning">Hay correos inválidos pendientes. Quita o corrige estos valores antes de guardar.</p>
        }

        @if (emailRecipients.length || invalidEmailRecipients.length) {
          <div class="email-list">
            @for (recipient of emailRecipients; track trackEmailRecipient('valid', $index, recipient); let index = $index) {
              <article class="email-chip" data-testid="email-recipient-chip">
                <span class="email-chip-text">{{ recipient }}</span>

                @if (!readOnly()) {
                  <button type="button" class="link danger" data-testid="remove-email-recipient-button" (click)="removeEmailRecipient(index)">
                    Quitar
                  </button>
                }
              </article>
            }

            @for (recipient of invalidEmailRecipients; track trackEmailRecipient('invalid', $index, recipient); let index = $index) {
              <article class="email-chip invalid" data-testid="invalid-email-recipient-chip">
                <div class="email-chip-content">
                  <span class="email-chip-text">{{ recipient }}</span>
                  <small>Inválido</small>
                </div>

                @if (!readOnly()) {
                  <button
                    type="button"
                    class="link danger"
                    data-testid="remove-invalid-email-recipient-button"
                    (click)="removeInvalidEmailRecipient(index)"
                  >
                    Quitar
                  </button>
                }
              </article>
            }
          </div>
        } @else {
          <p class="helper">Sin correos registrados.</p>
        }
      </section>

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

      @if (displayErrorMessage(); as currentErrorMessage) {
        <p class="error">{{ currentErrorMessage }}</p>
      }

      <button type="submit" [disabled]="readOnly() || submitting()">{{ submitting() ? 'Guardando...' : submitLabel() }}</button>
    </form>
  `,
  styles: [`
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    .wide-field { grid-column:1 / -1; }
    input, button, select, textarea { font:inherit; }
    input, select, textarea {
      border:1px solid #c9d1da;
      border-radius:0.8rem;
      padding:0.75rem 0.9rem;
      width: 100%;
      min-width: 0;
      box-sizing: border-box;
    }
    textarea { resize:vertical; min-height:6.5rem; }
    select option {
      white-space: normal;
      word-break: break-word;
    }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
    .email-field { display:grid; gap:0.75rem; align-self:stretch; }
    .email-field-header { display:grid; gap:0.35rem; }
    .email-entry-row { display:grid; grid-template-columns:minmax(0, 1fr) auto; gap:0.75rem; align-items:end; }
    .email-add-button { min-width:fit-content; }
    .email-list { display:grid; gap:0.6rem; }
    .email-chip {
      display:flex;
      align-items:center;
      justify-content:space-between;
      gap:0.75rem;
      padding:0.75rem 0.9rem;
      border:1px solid #d8d1c2;
      border-radius:0.9rem;
      background:#fcfbf8;
    }
    .email-chip.invalid { border-color:#e5b8b8; background:#fff6f6; }
    .email-chip-content { display:grid; gap:0.15rem; }
    .email-chip-text { overflow-wrap:anywhere; }
    .special-fields { grid-column:1 / -1; display:grid; gap:0.75rem; padding-top:0.25rem; }
    .special-fields-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .special-fields-header p, .helper { margin:0; color:#5f6b76; }
    .helper.warning { color:#8a5a00; }
    .email-chip.invalid small { color:#7a2020; font-weight:600; }
    .special-field-list { display:grid; gap:0.75rem; }
    .special-field-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:0.9rem; background:#fcfbf8; display:grid; gap:0.75rem; }
    .special-field-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; align-items:end; }
    .secondary-action { background:#d8c49b; color:#182533; }
    .link { background:transparent; color:#182533; padding:0; justify-self:flex-start; }
    .link.danger { color:#7a2020; }
    @media (max-width: 640px) {
      .email-entry-row { grid-template-columns:1fr; }
      .email-add-button { width:100%; }
      .email-chip { align-items:flex-start; flex-direction:column; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalReceiverFormComponent implements OnChanges {
  private static readonly emailMaxLength = 200;

  private readonly fiscalReceiverSatCatalogService = inject(FiscalReceiverSatCatalogService);

  readonly receiver = input<FiscalReceiver | null>(null);
  readonly initialValue = input<UpsertFiscalReceiverRequest | null>(null);
  readonly submitLabel = input('Guardar receptor');
  readonly readOnly = input(false);
  readonly submitting = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly submitted = output<UpsertFiscalReceiverRequest>();

  protected readonly satCatalog = toSignal(this.fiscalReceiverSatCatalogService.getCatalog(), { initialValue: null });
  protected readonly localErrorMessage = signal<string | null>(null);
  protected readonly emailStateMessage = signal<string | null>(null);
  protected draft: UpsertFiscalReceiverRequest = emptyReceiver();
  protected emailInput = '';
  protected emailRecipients: string[] = [];
  protected invalidEmailRecipients: string[] = [];
  private hydratingDraft = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['receiver'] && !changes['initialValue']) {
      return;
    }

    const receiver = this.receiver();
    this.localErrorMessage.set(null);
    this.emailStateMessage.set(null);
    this.hydratingDraft = true;
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
    this.syncEmailRecipients(this.draft.email);
    this.hydratingDraft = false;
  }

  protected submitForm(): void {
    const pendingEmailInput = this.emailInput.trim();
    const normalizedEmail = joinEmailRecipients(this.emailRecipients);

    if (pendingEmailInput) {
      const pendingInvalidRecipients = findInvalidEmailRecipients(pendingEmailInput);
      this.localErrorMessage.set(
        pendingInvalidRecipients.length > 0
          ? `Correo inválido: ${pendingInvalidRecipients.join(', ')}. Corrige el correo antes de agregarlo.`
          : 'Agrega o limpia el contenido del campo Correo(s) antes de guardar.',
      );
      return;
    }

    if (this.invalidEmailRecipients.length > 0) {
      this.localErrorMessage.set(
        `Correo inválido: ${this.invalidEmailRecipients.join(', ')}. Corrige o quita esos valores antes de guardar.`,
      );
      return;
    }

    if (normalizedEmail.length > FiscalReceiverFormComponent.emailMaxLength) {
      this.localErrorMessage.set(
        `El campo Correo(s) permite máximo ${FiscalReceiverFormComponent.emailMaxLength} caracteres.`,
      );
      return;
    }

    this.localErrorMessage.set(null);
    this.syncDraftEmail();
    this.submitted.emit({
      ...this.draft,
      rfc: this.draft.rfc.trim(),
      legalName: this.draft.legalName.trim(),
      fiscalRegimeCode: this.draft.fiscalRegimeCode.trim(),
      cfdiUseCodeDefault: this.draft.cfdiUseCodeDefault.trim(),
      postalCode: this.draft.postalCode.trim(),
      countryCode: this.draft.countryCode?.trim() || null,
      foreignTaxRegistration: this.draft.foreignTaxRegistration?.trim() || null,
      email: normalizedEmail || null,
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

  protected addEmailRecipients(): void {
    const rawInput = this.emailInput.trim();
    if (!rawInput) {
      return;
    }

    const invalidRecipients = findInvalidEmailRecipients(rawInput);
    if (invalidRecipients.length > 0) {
      this.localErrorMessage.set(
        `Correo inválido: ${invalidRecipients.join(', ')}. Corrige el correo antes de agregarlo.`,
      );
      return;
    }

    const nextRecipients = mergeEmailRecipients(this.emailRecipients, parseEmailRecipients(rawInput));
    const normalizedEmail = joinEmailRecipients(nextRecipients);

    if (normalizedEmail.length > FiscalReceiverFormComponent.emailMaxLength) {
      this.localErrorMessage.set(
        `El campo Correo(s) permite máximo ${FiscalReceiverFormComponent.emailMaxLength} caracteres.`,
      );
      return;
    }

    this.emailRecipients = nextRecipients;
    this.syncDraftEmail();
    this.emailInput = '';
    this.localErrorMessage.set(null);
  }

  protected removeEmailRecipient(index: number): void {
    this.emailRecipients = this.emailRecipients.filter((_, currentIndex) => currentIndex !== index);
    this.syncDraftEmail();
    this.localErrorMessage.set(null);
  }

  protected removeInvalidEmailRecipient(index: number): void {
    this.invalidEmailRecipients = this.invalidEmailRecipients.filter((_, currentIndex) => currentIndex !== index);
    this.refreshEmailStateMessage();
    this.localErrorMessage.set(null);
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

  protected trackEmailRecipient(type: 'valid' | 'invalid', index: number, recipient: string): string {
    return `${type}-${recipient}-${index}`;
  }

  protected catalogReady(): boolean {
    return this.satCatalog() !== null;
  }

  protected fiscalRegimeOptions(): FiscalReceiverSatCatalogOption[] {
    const catalog = this.satCatalog();
    const options = catalog?.regimenFiscal ?? [];
    return includeLegacyOption(options, this.draft.fiscalRegimeCode, 'Régimen legacy no encontrado en catálogo');
  }

  protected cfdiUseOptions(): FiscalReceiverSatCatalogOption[] {
    const catalog = this.satCatalog();
    const selectedFiscalRegimeCode = this.draft.fiscalRegimeCode.trim().toUpperCase();
    const currentCfdiUseCode = this.draft.cfdiUseCodeDefault.trim().toUpperCase();

    if (!catalog) {
      return [];
    }

    const allowedByRegime = catalog.byRegimenFiscal.find((regime) => regime.code === selectedFiscalRegimeCode)?.allowedUsoCfdi ?? [];
    return includeLegacyOption(allowedByRegime, currentCfdiUseCode, 'Uso CFDI legacy no encontrado o incompatible');
  }

  protected updateFiscalRegime(value: string): void {
    this.draft.fiscalRegimeCode = value;

    if (this.hydratingDraft) {
      return;
    }

    const selectedFiscalRegimeCode = value.trim().toUpperCase();
    const selectedCfdiUseCode = this.draft.cfdiUseCodeDefault.trim().toUpperCase();
    if (!selectedFiscalRegimeCode || !selectedCfdiUseCode) {
      return;
    }

    const catalog = this.satCatalog();
    const isCompatible = catalog?.byRegimenFiscal
      .find((regime) => regime.code === selectedFiscalRegimeCode)
      ?.allowedUsoCfdi
      .some((usage) => usage.code === selectedCfdiUseCode) ?? false;

    if (!isCompatible) {
      this.draft.cfdiUseCodeDefault = '';
    }
  }

  protected displayErrorMessage(): string | null {
    return this.localErrorMessage() ?? this.emailStateMessage() ?? this.errorMessage();
  }

  private syncEmailRecipients(rawEmail: string | null | undefined): void {
    const validRecipients: string[] = [];
    const invalidRecipients: string[] = [];
    const seenValidRecipients = new Set<string>();

    splitEmailRecipients(rawEmail).forEach((recipient) => {
      const trimmedRecipient = recipient.trim();
      if (!trimmedRecipient) {
        return;
      }

      if (!isValidEmailRecipient(trimmedRecipient)) {
        invalidRecipients.push(trimmedRecipient);
        return;
      }

      const key = trimmedRecipient.toLowerCase();
      if (seenValidRecipients.has(key)) {
        return;
      }

      seenValidRecipients.add(key);
      validRecipients.push(trimmedRecipient);
    });

    this.emailInput = '';
    this.emailRecipients = validRecipients;
    this.invalidEmailRecipients = invalidRecipients;
    this.syncDraftEmail();
    this.refreshEmailStateMessage();
  }

  private syncDraftEmail(): void {
    this.draft.email = this.emailRecipients.length > 0 ? joinEmailRecipients(this.emailRecipients) : '';
  }

  private refreshEmailStateMessage(): void {
    this.emailStateMessage.set(
      this.invalidEmailRecipients.length > 0
        ? `Se detectaron correos inválidos en este receptor: ${this.invalidEmailRecipients.join(', ')}. Corrige o quita esos valores antes de guardar.`
        : null,
    );
  }
}

function includeLegacyOption(
  options: readonly FiscalReceiverSatCatalogOption[],
  currentCode: string | null | undefined,
  fallbackLabel: string): FiscalReceiverSatCatalogOption[]
{
  const normalizedCurrentCode = currentCode?.trim().toUpperCase();
  if (!normalizedCurrentCode) {
    return [...options];
  }

  if (options.some((option) => option.code === normalizedCurrentCode)) {
    return [...options];
  }

  return [
    {
      code: normalizedCurrentCode,
      description: `${normalizedCurrentCode} - ${fallbackLabel}`
    },
    ...options
  ];
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

function mergeEmailRecipients(currentRecipients: readonly string[], incomingRecipients: readonly string[]): string[] {
  const seenRecipients = new Set(currentRecipients.map((recipient) => recipient.toLowerCase()));
  const mergedRecipients = [...currentRecipients];

  incomingRecipients.forEach((recipient) => {
    const key = recipient.toLowerCase();
    if (seenRecipients.has(key)) {
      return;
    }

    seenRecipients.add(key);
    mergedRecipients.push(recipient);
  });

  return mergedRecipients;
}
