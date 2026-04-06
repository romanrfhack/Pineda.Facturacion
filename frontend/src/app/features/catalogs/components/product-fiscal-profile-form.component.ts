import { ChangeDetectionStrategy, Component, OnChanges, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ProductFiscalProfile, SatProductServiceSearchItem, UpsertProductFiscalProfileRequest } from '../models/catalogs.models';
import { SatProductServicesApiService } from '../infrastructure/sat-product-services-api.service';

@Component({
  selector: 'app-product-fiscal-profile-form',
  imports: [FormsModule],
  template: `
    <form class="form-grid" (ngSubmit)="submitForm()">
      <label>
        <span>Código interno</span>
        <input [(ngModel)]="draft.internalCode" name="internalCode" required />
      </label>

      <label>
        <span>Descripción</span>
        <input [(ngModel)]="draft.description" name="description" required />
      </label>

      <section class="sat-search-card">
        <div class="sat-search-header">
          <div>
            <span class="label-title">Producto/servicio SAT</span>
            <small class="helper">Busca por código o descripción y selecciona una opción del catálogo local.</small>
          </div>

          <div class="sat-search-actions">
            @if (!manualSatEntry()) {
              <button type="button" class="secondary small" (click)="enableManualSatEntry()" [disabled]="readOnly() || submitting()">
                Capturar código manualmente
              </button>
            } @else {
              <button type="button" class="secondary small" (click)="disableManualSatEntry()" [disabled]="readOnly() || submitting()">
                Volver a búsqueda asistida
              </button>
            }

            <button type="button" class="secondary small" (click)="useGenericFallback()" [disabled]="readOnly() || submitting()">
              Usar 01010101 explícitamente
            </button>
          </div>
        </div>

        @if (!manualSatEntry()) {
          <label>
            <span>Búsqueda SAT</span>
            <input
              [ngModel]="satProductSearchQuery()"
              (ngModelChange)="onSatProductSearchChange($event)"
              name="satProductSearchQuery"
              autocomplete="off"
              placeholder="Ej. 40161513 o filtro de aceite"
            />
          </label>

          @if (showSatProductSuggestions()) {
            <section class="suggestions" aria-label="Sugerencias SAT de producto o servicio">
              @if (searchingSatProductServices()) {
                <p class="helper">Buscando catálogo SAT...</p>
              } @else if (satProductSearchError()) {
                <p class="error">{{ satProductSearchError() }}</p>
              } @else if (!satProductServiceResults().length) {
                <p class="helper">Sin coincidencias en el catálogo local.</p>
              } @else {
                <ul>
                  @for (option of satProductServiceResults(); track option.code) {
                    <li>
                      <button type="button" class="suggestion-button" (click)="selectSatProductService(option)">
                        <strong>{{ option.code }}</strong>
                        <span>{{ option.description }}</span>
                        <small>Coincidencia {{ option.matchKind }}</small>
                      </button>
                    </li>
                  }
                </ul>
              }
            </section>
          }

          @if (selectedSatProductService(); as selectedSatProductService) {
            <p class="selected-sat-product">
              Seleccionado:
              <strong>{{ selectedSatProductService.code }}</strong>
              <span>{{ selectedSatProductService.description }}</span>
            </p>
          } @else if (draft.satProductServiceCode.trim()) {
            <p class="selected-sat-product">
              Código capturado:
              <strong>{{ draft.satProductServiceCode.trim() }}</strong>
              <span>Sin descripción resuelta todavía en la búsqueda asistida.</span>
            </p>
          } @else {
            <p class="warning">Pendiente de seleccionar un producto/servicio SAT.</p>
          }
        } @else {
          <label>
            <span>Código SAT producto/servicio</span>
            <input
              [ngModel]="draft.satProductServiceCode"
              (ngModelChange)="onManualSatProductServiceCodeChange($event)"
              name="satProductServiceCode"
              required
            />
          </label>
          <p class="warning">Modo manual activo. Usa esta opción solo cuando no puedas resolver el código con la búsqueda asistida.</p>
        }

        <p class="helper">El código genérico 01010101 ya no se asigna automáticamente; debes elegirlo de forma explícita.</p>
        @if (satProductValidationMessage()) {
          <p class="error">{{ satProductValidationMessage() }}</p>
        }
      </section>

      <label>
        <span>Código SAT de unidad</span>
        <input [(ngModel)]="draft.satUnitCode" name="satUnitCode" required />
      </label>

      <label>
        <span>Código de objeto de impuesto</span>
        <input [(ngModel)]="draft.taxObjectCode" name="taxObjectCode" required />
      </label>

      <label>
        <span>Tasa de IVA</span>
        <input [(ngModel)]="draft.vatRate" name="vatRate" type="number" min="0" step="0.0001" required />
      </label>

      <label>
        <span>Texto de unidad predeterminado</span>
        <input [(ngModel)]="draft.defaultUnitText" name="defaultUnitText" />
      </label>

      <label class="checkbox">
        <input [(ngModel)]="draft.isActive" name="isActive" type="checkbox" />
        <span>Activo</span>
      </label>

      @if (errorMessage()) {
        <p class="error">{{ errorMessage() }}</p>
      }

      <button type="submit" [disabled]="readOnly() || submitting() || !hasSatProductServiceCode()">{{ submitting() ? 'Guardando...' : submitLabel() }}</button>
    </form>
  `,
  styles: [`
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .sat-search-card { display:grid; gap:0.75rem; grid-column:1 / -1; padding:1rem; border:1px solid #d8d1c2; border-radius:1rem; background:#fcfaf5; }
    .sat-search-header { display:flex; gap:0.75rem; justify-content:space-between; align-items:flex-start; flex-wrap:wrap; }
    .sat-search-actions { display:flex; gap:0.5rem; flex-wrap:wrap; }
    .label-title { display:block; font-weight:600; }
    .suggestions { border:1px solid #d8d1c2; border-radius:0.8rem; background:#fff; padding:0.5rem; }
    .suggestions ul { list-style:none; margin:0; padding:0; display:grid; gap:0.35rem; }
    .suggestion-button { width:100%; text-align:left; border:1px solid #ece5d7; border-radius:0.75rem; background:#fff; color:#182533; padding:0.75rem; display:grid; gap:0.2rem; }
    .selected-sat-product { margin:0; display:grid; gap:0.15rem; color:#243240; }
    .warning { margin:0; color:#8a5a00; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#e7ddc7; color:#182533; }
    button.small { padding:0.55rem 0.8rem; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .helper { color:#5f6b76; margin:0; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductFiscalProfileFormComponent implements OnChanges {
  private readonly satProductServicesApi = inject(SatProductServicesApiService);

  readonly profile = input<ProductFiscalProfile | null>(null);
  readonly initialValue = input<UpsertProductFiscalProfileRequest | null>(null);
  readonly submitLabel = input('Guardar perfil fiscal de producto');
  readonly readOnly = input(false);
  readonly submitting = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly submitted = output<UpsertProductFiscalProfileRequest>();

  protected draft: UpsertProductFiscalProfileRequest = emptyProductProfile();
  protected readonly satProductSearchQuery = signal('');
  protected readonly satProductServiceResults = signal<SatProductServiceSearchItem[]>([]);
  protected readonly searchingSatProductServices = signal(false);
  protected readonly satProductSearchError = signal<string | null>(null);
  protected readonly showSatProductSuggestions = signal(false);
  protected readonly manualSatEntry = signal(false);
  protected readonly selectedSatProductService = signal<SatProductServiceSearchItem | null>(null);
  protected readonly satProductValidationMessage = signal<string | null>(null);
  private lastProfileSignature: string | null = null;
  private lastInitialValueSignature: string | null = null;

  ngOnChanges(): void {
    const profile = this.profile();
    const profileSignature = profile ? JSON.stringify(profile) : null;
    const initialValueSignature = this.initialValue() ? JSON.stringify(this.initialValue()) : null;

    if (profileSignature === this.lastProfileSignature && initialValueSignature === this.lastInitialValueSignature) {
      return;
    }

    this.lastProfileSignature = profileSignature;
    this.lastInitialValueSignature = initialValueSignature;
    this.draft = profile
      ? {
          internalCode: profile.internalCode,
          description: profile.description,
          satProductServiceCode: profile.satProductServiceCode,
          satUnitCode: profile.satUnitCode,
          taxObjectCode: profile.taxObjectCode,
          vatRate: profile.vatRate,
          defaultUnitText: profile.defaultUnitText,
          isActive: profile.isActive
        }
      : {
          ...emptyProductProfile(),
          ...(this.initialValue() ?? {})
        };

    this.manualSatEntry.set(false);
    this.showSatProductSuggestions.set(false);
    this.satProductSearchError.set(null);
    this.satProductServiceResults.set([]);
    this.selectedSatProductService.set(this.buildSelectedStateFromDraft());
    this.satProductSearchQuery.set(this.buildInitialSearchQuery());
    this.syncSatProductValidation(false);
  }

  protected submitForm(): void {
    if (this.readOnly() || this.submitting()) {
      return;
    }

    if (!this.hasSatProductServiceCode()) {
      this.syncSatProductValidation(true);
      return;
    }

    this.submitted.emit({
      ...this.draft,
      internalCode: this.draft.internalCode.trim(),
      description: this.draft.description.trim(),
      satProductServiceCode: this.draft.satProductServiceCode.trim(),
      satUnitCode: this.draft.satUnitCode.trim(),
      taxObjectCode: this.draft.taxObjectCode.trim(),
      defaultUnitText: this.draft.defaultUnitText?.trim() || null
    });
  }

  protected async onSatProductSearchChange(value: string): Promise<void> {
    this.satProductSearchQuery.set(value);
    this.satProductSearchError.set(null);
    this.selectedSatProductService.set(null);
    this.draft = {
      ...this.draft,
      satProductServiceCode: ''
    };
    this.syncSatProductValidation(false);

    const query = value.trim();
    if (query.length < 2) {
      this.satProductServiceResults.set([]);
      this.showSatProductSuggestions.set(false);
      return;
    }

    this.searchingSatProductServices.set(true);
    this.showSatProductSuggestions.set(true);

    try {
      const results = await firstValueFrom(this.satProductServicesApi.search(query, 8));
      this.satProductServiceResults.set(results);
    } catch {
      this.satProductServiceResults.set([]);
      this.satProductSearchError.set('No fue posible consultar el catálogo SAT local.');
    } finally {
      this.searchingSatProductServices.set(false);
    }
  }

  protected selectSatProductService(option: SatProductServiceSearchItem): void {
    this.draft = {
      ...this.draft,
      satProductServiceCode: option.code
    };
    this.selectedSatProductService.set(option);
    this.satProductSearchQuery.set(option.displayText);
    this.satProductServiceResults.set([]);
    this.showSatProductSuggestions.set(false);
    this.manualSatEntry.set(false);
    this.syncSatProductValidation(false);
  }

  protected enableManualSatEntry(): void {
    this.manualSatEntry.set(true);
    this.showSatProductSuggestions.set(false);
  }

  protected disableManualSatEntry(): void {
    this.manualSatEntry.set(false);
    this.syncSatProductValidation(false);
  }

  protected useGenericFallback(): void {
    this.selectSatProductService({
      code: '01010101',
      description: 'No existe en el catálogo',
      displayText: '01010101 — No existe en el catálogo',
      matchKind: 'exactCode'
    });
  }

  protected onManualSatProductServiceCodeChange(value: string): void {
    this.draft = {
      ...this.draft,
      satProductServiceCode: value
    };
    this.selectedSatProductService.set(this.buildSelectedStateFromDraft());
    this.syncSatProductValidation(false);
  }

  protected hasSatProductServiceCode(): boolean {
    return this.draft.satProductServiceCode.trim().length > 0;
  }

  private buildSelectedStateFromDraft(): SatProductServiceSearchItem | null {
    const code = this.draft.satProductServiceCode.trim();
    if (!code) {
      return null;
    }

    if (code === '01010101') {
      return {
        code,
        description: 'No existe en el catálogo',
        displayText: '01010101 — No existe en el catálogo',
        matchKind: 'exactCode'
      };
    }

    return {
      code,
      description: 'Código SAT capturado',
      displayText: code,
      matchKind: 'exactCode'
    };
  }

  private buildInitialSearchQuery(): string {
    const selected = this.selectedSatProductService();
    if (selected) {
      return selected.displayText;
    }

    return this.draft.satProductServiceCode.trim();
  }

  private syncSatProductValidation(forceMessage: boolean): void {
    if (this.hasSatProductServiceCode()) {
      this.satProductValidationMessage.set(null);
      return;
    }

    if (forceMessage) {
      this.satProductValidationMessage.set('Debes seleccionar o capturar un producto/servicio SAT antes de guardar.');
    }
  }
}

function emptyProductProfile(): UpsertProductFiscalProfileRequest {
  return {
    internalCode: '',
    description: '',
    satProductServiceCode: '',
    satUnitCode: '',
    taxObjectCode: '',
    vatRate: 0,
    defaultUnitText: '',
    isActive: true
  };
}
