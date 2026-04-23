import {
  ChangeDetectionStrategy,
  Component,
  OnChanges,
  OnDestroy,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import {
  ProductFiscalProfile,
  ProductFiscalProfileRecoverySuggestion,
  SatProductServiceSearchItem,
  UpsertProductFiscalProfileRequest,
} from '../models/catalogs.models';
import { SatProductServicesApiService } from '../infrastructure/sat-product-services-api.service';

@Component({
  selector: 'app-product-fiscal-profile-form',
  imports: [DecimalPipe, FormsModule],
  template: `
    <form class="form-grid" (ngSubmit)="submitForm()">
      @if (showIdentityFields()) {
        <label>
          <span>Código interno</span>
          <input [(ngModel)]="draft.internalCode" name="internalCode" required />
        </label>

        <label>
          <span>Descripción</span>
          <input [(ngModel)]="draft.description" name="description" required />
        </label>
      }

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

            @if (allowExplicitGeneric()) {
              <button type="button" class="secondary small" (click)="useGenericFallback()" [disabled]="readOnly() || submitting()">
                Usar 01010101 explícitamente
              </button>
            }
          </div>
        </div>

        @if (recoverySuggestions().length) {
          <section class="recovery-suggestions">
            <div>
              <span class="label-title">Sugerencias determinísticas</span>
              <small class="helper">
                Sólo se preseleccionan automáticamente opciones exactas o históricas. Las coincidencias por descripción requieren confirmación explícita.
              </small>
            </div>

            <ul>
              @for (suggestion of recoverySuggestions(); track suggestion.satProductServiceCode + '-' + suggestion.source) {
                <li>
                  <button
                    type="button"
                    class="suggestion-button"
                    (click)="applyRecoverySuggestion(suggestion)"
                    [disabled]="readOnly() || submitting()"
                  >
                    <strong>{{ suggestion.satProductServiceCode }}</strong>
                    <span>{{ suggestion.satProductServiceDescription || 'Sin descripción resuelta' }}</span>
                    <small>
                      {{ suggestion.reason }}
                      · score {{ suggestion.score | number:'1.2-2' }}
                      · fuente {{ suggestion.source }}
                      @if (suggestion.requiresExplicitConfirmation) {
                        · requiere confirmación
                      }
                    </small>
                  </button>
                </li>
              }
            </ul>
          </section>
        }

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
            <small class="helper">Busca con al menos 3 caracteres. La consulta se dispara con un debounce corto.</small>
          </label>

          @if (showSatProductSuggestions()) {
            <section class="suggestions" aria-label="Sugerencias SAT de producto o servicio">
              @if (searchingSatProductServices()) {
                <p class="helper">Buscando catálogo SAT...</p>
              } @else if (satProductSearchQuery().trim().length > 0 && satProductSearchQuery().trim().length < minSearchChars) {
                <p class="helper">Captura al menos {{ minSearchChars }} caracteres para consultar el catálogo SAT local.</p>
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
                        <small>
                          Coincidencia {{ option.matchKind }}
                          @if (option.score != null) {
                            · score {{ option.score | number:'1.2-2' }}
                          }
                        </small>
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

        @if (allowExplicitGeneric()) {
          <p class="helper">El código genérico 01010101 ya no se asigna automáticamente; debes elegirlo de forma explícita.</p>
        }
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
        <span>{{ unitTextLabel() }}</span>
        <input [(ngModel)]="draft.defaultUnitText" name="defaultUnitText" />
      </label>

      @if (showActiveField()) {
        <label class="checkbox">
          <input [(ngModel)]="draft.isActive" name="isActive" type="checkbox" />
          <span>Activo</span>
        </label>
      }

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
    .recovery-suggestions { display:grid; gap:0.75rem; border:1px solid #e6dcc7; border-radius:0.85rem; background:#fffdf8; padding:0.85rem; }
    .recovery-suggestions ul { list-style:none; margin:0; padding:0; display:grid; gap:0.35rem; }
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
export class ProductFiscalProfileFormComponent implements OnChanges, OnDestroy {
  private static readonly searchDebounceMs = 350;
  private readonly satProductServicesApi = inject(SatProductServicesApiService);

  readonly profile = input<ProductFiscalProfile | null>(null);
  readonly initialValue = input<UpsertProductFiscalProfileRequest | null>(null);
  readonly recoverySuggestions = input<ProductFiscalProfileRecoverySuggestion[]>([]);
  readonly submitLabel = input('Guardar perfil fiscal de producto');
  readonly readOnly = input(false);
  readonly submitting = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly allowExplicitGeneric = input(true);
  readonly showIdentityFields = input(true);
  readonly showActiveField = input(true);
  readonly unitTextLabel = input('Texto de unidad predeterminado');
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
  protected readonly minSearchChars = 3;
  private lastProfileSignature: string | null = null;
  private lastInitialValueSignature: string | null = null;
  private satProductSearchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  private satProductSearchToken = 0;

  ngOnDestroy(): void {
    this.clearSatProductSearchDebounce();
  }

  ngOnChanges(): void {
    const profile = this.profile();
    const profileSignature = profile ? JSON.stringify(profile) : null;
    const initialValueSignature = this.initialValue() ? JSON.stringify(this.initialValue()) : null;

    if (profileSignature === this.lastProfileSignature && initialValueSignature === this.lastInitialValueSignature) {
      return;
    }

    this.lastProfileSignature = profileSignature;
    this.lastInitialValueSignature = initialValueSignature;
    this.clearSatProductSearchDebounce();
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

  protected onSatProductSearchChange(value: string): void {
    this.satProductSearchQuery.set(value);
    this.satProductSearchError.set(null);
    this.selectedSatProductService.set(null);
    this.clearSatProductSearchDebounce();
    this.satProductSearchToken += 1;
    this.draft = {
      ...this.draft,
      satProductServiceCode: ''
    };
    this.syncSatProductValidation(false);

    const query = value.trim();
    if (query.length < this.minSearchChars) {
      this.searchingSatProductServices.set(false);
      this.satProductServiceResults.set([]);
      this.showSatProductSuggestions.set(query.length > 0);
      return;
    }

    this.showSatProductSuggestions.set(true);
    const searchToken = this.satProductSearchToken;
    this.satProductSearchDebounceHandle = setTimeout(async () => {
      this.searchingSatProductServices.set(true);

      try {
        const results = await firstValueFrom(this.satProductServicesApi.searchBestEffort(query, 12));
        if (searchToken !== this.satProductSearchToken) {
          return;
        }

        this.satProductServiceResults.set(results);
      } catch {
        if (searchToken !== this.satProductSearchToken) {
          return;
        }

        this.satProductServiceResults.set([]);
        this.satProductSearchError.set('No fue posible consultar el catálogo SAT local.');
      } finally {
        if (searchToken === this.satProductSearchToken) {
          this.searchingSatProductServices.set(false);
        }
      }
    }, ProductFiscalProfileFormComponent.searchDebounceMs);
  }

  protected selectSatProductService(option: SatProductServiceSearchItem): void {
    this.clearSatProductSearchDebounce();
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

  protected applyRecoverySuggestion(suggestion: ProductFiscalProfileRecoverySuggestion): void {
    this.draft = {
      ...this.draft,
      satProductServiceCode: suggestion.satProductServiceCode,
      satUnitCode: suggestion.satUnitCode,
      taxObjectCode: suggestion.taxObjectCode,
      vatRate: suggestion.vatRate,
      defaultUnitText: suggestion.defaultUnitText || suggestion.satUnitDescription || this.draft.defaultUnitText,
    };
    this.selectSatProductService({
      code: suggestion.satProductServiceCode,
      description: suggestion.satProductServiceDescription || 'Sugerencia aplicada',
      displayText: buildDisplayText(
        suggestion.satProductServiceCode,
        suggestion.satProductServiceDescription || undefined,
      ),
      matchKind: suggestion.matchKind,
      score: suggestion.score,
    });
  }

  protected enableManualSatEntry(): void {
    this.clearSatProductSearchDebounce();
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

    const recoverySuggestion = this.recoverySuggestions().find(
      (suggestion) => suggestion.satProductServiceCode === code,
    );
    if (recoverySuggestion) {
      return {
        code,
        description: recoverySuggestion.satProductServiceDescription || 'Código SAT sugerido',
        displayText: buildDisplayText(code, recoverySuggestion.satProductServiceDescription || undefined),
        matchKind: recoverySuggestion.matchKind,
        score: recoverySuggestion.score,
      };
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

  private clearSatProductSearchDebounce(): void {
    if (this.satProductSearchDebounceHandle) {
      clearTimeout(this.satProductSearchDebounceHandle);
      this.satProductSearchDebounceHandle = null;
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

function buildDisplayText(code: string, description?: string): string {
  return description?.trim() ? `${code} — ${description.trim()}` : code;
}
