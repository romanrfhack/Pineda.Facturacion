import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import {
  BillingDocumentLookupResponse,
  FiscalCancellationResponse,
  FiscalDocumentResponse,
  FiscalReceiverSearchResponse,
  FiscalStampResponse,
  IssuerProfileResponse,
  PrepareFiscalDocumentRequest
} from '../models/fiscal-documents.models';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FiscalDocumentCardComponent } from '../components/fiscal-document-card.component';
import { FiscalStampEvidenceCardComponent } from '../components/fiscal-stamp-evidence-card.component';
import { FiscalCancellationCardComponent } from '../components/fiscal-cancellation-card.component';
import { FiscalStampEvidenceDetailComponent } from '../components/fiscal-stamp-evidence-detail.component';
import { XmlViewerPanelComponent } from '../../../shared/components/xml-viewer-panel.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { ProductFiscalProfileFormComponent } from '../../catalogs/components/product-fiscal-profile-form.component';
import { ProductFiscalProfilesApiService } from '../../catalogs/infrastructure/product-fiscal-profiles-api.service';
import { UpsertProductFiscalProfileRequest } from '../../catalogs/models/catalogs.models';
import { extractMissingProductFiscalProfileContext, MissingProductFiscalProfileContext } from '../application/missing-product-fiscal-profile';

@Component({
  selector: 'app-fiscal-document-operations-page',
  imports: [FormsModule, RouterLink, FiscalDocumentCardComponent, FiscalStampEvidenceCardComponent, FiscalCancellationCardComponent, FiscalStampEvidenceDetailComponent, XmlViewerPanelComponent, ProductFiscalProfileFormComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Operaciones de documento fiscal</p>
        <h2>Preparar, timbrar, consultar, cancelar y actualizar estatus</h2>
      </header>

      <section class="card">
        <h3>Seleccionar documento de facturación</h3>
        <p class="helper">Busca por id de documento, id de orden o id legado para continuar con el flujo fiscal.</p>

        <form class="context-search" (ngSubmit)="searchBillingDocuments()">
          <label class="search-label">
            <span>Búsqueda de documento</span>
            <div class="search-row">
              <input
                [(ngModel)]="billingDocumentQuery"
                name="billingDocumentQuery"
                placeholder="Id de documento, id de orden o id legado"
              />
              <button type="submit" class="secondary" [disabled]="loadingBillingDocumentSearch()">
                {{ loadingBillingDocumentSearch() ? 'Buscando...' : 'Buscar' }}
              </button>
            </div>
          </label>
        </form>

        @if (billingDocumentSearchError()) {
          <p class="error">{{ billingDocumentSearchError() }}</p>
        }

        @if (billingDocumentSearchResults().length) {
          <section class="context-results">
            @for (billingDocument of billingDocumentSearchResults(); track billingDocument.billingDocumentId) {
              <button type="button" class="context-result" (click)="selectBillingDocument(billingDocument)">
                <strong>Documento #{{ billingDocument.billingDocumentId }}</strong>
                <span>Orden {{ billingDocument.salesOrderId }} · Legado {{ billingDocument.legacyOrderId }}</span>
                <small>Estatus {{ getDisplayLabel(billingDocument.status) }} · {{ billingDocument.currencyCode }} {{ billingDocument.total }}</small>
              </button>
            }
          </section>
        } @else if (billingDocumentSearchTouched() && !loadingBillingDocumentSearch()) {
          <p class="helper">No se encontraron coincidencias.</p>
        }

        @if (billingDocumentContext(); as currentBillingDocument) {
          <section class="billing-context">
            <div>
              <p class="selected-title">Documento seleccionado</p>
              <strong>Documento #{{ currentBillingDocument.billingDocumentId }}</strong>
              <span>Orden {{ currentBillingDocument.salesOrderId }} · Legado {{ currentBillingDocument.legacyOrderId }}</span>
              <span>Estatus {{ getDisplayLabel(currentBillingDocument.status) }} · {{ currentBillingDocument.currencyCode }} {{ currentBillingDocument.total }}</span>
            </div>

            <div class="context-actions">
              @if (currentBillingDocument.fiscalDocumentId) {
                <button type="button" class="secondary" (click)="openExistingFiscalDocument(currentBillingDocument)">
                  Abrir documento fiscal existente
                </button>
              }
              <button type="button" class="secondary" (click)="clearBillingDocumentSelection()">Cambiar documento</button>
            </div>
          </section>
        }
      </section>

      @if (!fiscalDocument() && billingDocumentContext()) {
        <section class="card">
          <h3>Preparar documento fiscal</h3>
          <p class="helper">Id de documento de facturación: <strong>{{ billingDocumentId() }}</strong></p>

          <form class="form-grid" (ngSubmit)="prepare()">
            <section class="receiver-selector">
              <label>
                <span>Buscar receptor</span>
                <input
                  [ngModel]="receiverQuery()"
                  (ngModelChange)="onReceiverQueryChange($event)"
                  name="receiverQuery"
                  autocomplete="off"
                  placeholder="Escribe RFC o razón social"
                />
              </label>

              @if (showReceiverSuggestions()) {
                <section class="suggestions" aria-label="Sugerencias de receptores">
                  @if (searchingReceivers()) {
                    <p class="helper">Buscando receptores...</p>
                  } @else if (receiverSearchError()) {
                    <p class="error">{{ receiverSearchError() }}</p>
                  } @else if (!receiverResults().length) {
                    <p class="helper">Sin coincidencias.</p>
                  } @else {
                    <ul>
                      @for (receiver of receiverResults(); track receiverTrackBy($index, receiver)) {
                        <li>
                          <button type="button" class="suggestion-button" (click)="selectReceiver(receiver)">
                            <strong>{{ receiver.rfc }}</strong>
                            <span>{{ receiver.legalName }}</span>
                            <small>Código postal {{ receiver.postalCode }}</small>
                          </button>
                        </li>
                      }
                    </ul>
                  }
                </section>
              } @else if (!selectedReceiver()) {
                <p class="helper">Escribe al menos 2 caracteres para buscar por RFC o razón social.</p>
              }

              @if (selectedReceiver(); as currentReceiver) {
                <section class="selected-receiver">
                  <div>
                    <p class="selected-title">Receptor seleccionado</p>
                    <strong>{{ currentReceiver.rfc }} · {{ currentReceiver.legalName }}</strong>
                    <span>Código postal {{ currentReceiver.postalCode }} · Régimen {{ currentReceiver.fiscalRegimeCode }}</span>
                  </div>
                  <button type="button" class="secondary" (click)="clearSelectedReceiver()">Cambiar</button>
                </section>
              }
            </section>

            <label>
              <span>Emisor activo</span>
              <input [value]="activeIssuerLabel()" disabled />
            </label>

            <label>
              <span>Método de pago SAT</span>
              <input [(ngModel)]="paymentMethodSat" name="paymentMethodSat" required />
            </label>

            <label>
              <span>Forma de pago SAT</span>
              <input [(ngModel)]="paymentFormSat" name="paymentFormSat" required />
            </label>

            <label>
              <span>Condición de pago</span>
              <input [(ngModel)]="paymentCondition" name="paymentCondition" />
            </label>

            <label class="checkbox">
              <input [(ngModel)]="isCreditSale" name="isCreditSale" type="checkbox" />
              <span>Venta a crédito</span>
            </label>

            <label>
              <span>Días de crédito</span>
              <input [(ngModel)]="creditDays" name="creditDays" type="number" min="1" />
            </label>

            <button type="submit" [disabled]="loadingPrepare() || savingMissingProductProfile()"> {{ loadingPrepare() ? 'Preparando...' : 'Preparar documento fiscal' }} </button>
          </form>

          @if (missingProductFiscalProfile(); as missingProduct) {
            <section class="recovery-panel">
              <div class="recovery-summary">
                <div>
                  <p class="selected-title">Recuperación requerida</p>
                  <strong>Falta el perfil fiscal del producto {{ missingProduct.internalCode }}.</strong>
                  <span>Debes darlo de alta para continuar.</span>
                  @if (missingProduct.lineNumber) {
                    <span>Línea {{ missingProduct.lineNumber }} del documento de facturación.</span>
                  }
                </div>

                <div class="context-actions">
                  @if (permissionService.canWriteMasterData()) {
                    <button type="button" class="secondary" (click)="openMissingProductProfileForm()" [disabled]="savingMissingProductProfile()">
                      Agregar producto fiscal
                    </button>
                  }
                  <button type="button" class="secondary" (click)="closeMissingProductProfileForm()" [disabled]="savingMissingProductProfile()">
                    Cancelar
                  </button>
                </div>
              </div>

              @if (showMissingProductProfileForm()) {
                <section class="card nested-card">
                  <h4>Alta de perfil fiscal de producto</h4>
                  <app-product-fiscal-profile-form
                    [initialValue]="missingProduct.draft"
                    [submitLabel]="'Guardar y reintentar'"
                    [submitting]="savingMissingProductProfile()"
                    [errorMessage]="missingProductProfileError()"
                    (submitted)="saveMissingProductProfile($event)"
                  />
                </section>
              }
            </section>
          }
        </section>
      } @else if (!fiscalDocument()) {
        <section class="card">
          <h3>Selecciona un documento de facturación</h3>
          <p class="helper">Carga un documento de facturación para preparar su documento fiscal o abrir el documento fiscal ya existente.</p>
        </section>
      }

      @if (fiscalDocument(); as currentDocument) {
        <app-fiscal-document-card [document]="currentDocument" />

        <section class="card actions">
          <h3>Operaciones</h3>
          <div class="button-row">
            @if (permissionService.canStampFiscal()) {
              <button type="button" (click)="stamp()" [disabled]="loadingOperation() || currentDocument.status === 'Stamped'">Timbrar</button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button type="button" class="danger" (click)="cancel()" [disabled]="loadingOperation() || currentDocument.status !== 'Stamped'">Cancelar</button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button type="button" class="secondary" (click)="refreshStatus()" [disabled]="loadingOperation()">Actualizar estatus</button>
            }
            <a [routerLink]="['/app/accounts-receivable']" [queryParams]="{ fiscalDocumentId: currentDocument.id }">Abrir cuentas por cobrar y pagos</a>
          </div>

          @if (lastOperationMessage()) {
            <p class="helper">{{ lastOperationMessage() }}</p>
          }
        </section>
      }

      @if (stampEvidence(); as currentStamp) {
        <app-fiscal-stamp-evidence-card
          [stamp]="currentStamp"
          (detailsRequested)="toggleStampDetail()"
          (xmlRequested)="openStampXml()"
        />
        @if (showStampDetail()) {
          <app-fiscal-stamp-evidence-detail [stamp]="currentStamp" />
        }
      } @else if (fiscalDocument()) {
        <section class="card">
          <h3>Evidencia de timbrado</h3>
          <p class="helper">Aún no hay evidencia de timbrado disponible. Primero timbra el documento fiscal para consultar metadatos persistidos y XML.</p>
        </section>
      }

      @if (showStampXmlPanel()) {
        <app-xml-viewer-panel
          title="XML del documento fiscal"
          [loading]="loadingStampXml()"
          [xmlContent]="stampXmlContent()"
          [errorMessage]="stampXmlError()"
          (close)="closeStampXml()"
        />
      }

      @if (cancellation(); as currentCancellation) {
        <app-fiscal-cancellation-card [cancellation]="currentCancellation" />
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.3rem 0 0; }
    .helper { color:#5f6b76; }
    .context-search, .search-label { display:grid; gap:0.75rem; }
    .search-row { display:flex; gap:0.75rem; align-items:end; }
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:start; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .context-results { display:grid; gap:0.5rem; margin-top:1rem; }
    .context-result { width:100%; display:grid; gap:0.15rem; text-align:left; border:1px solid #ece5d7; border-radius:0.8rem; background:#fff; color:#182533; padding:0.75rem 0.9rem; cursor:pointer; }
    .context-result:hover { background:#f7f2e7; }
    .context-result small { color:#5f6b76; }
    .billing-context { display:flex; justify-content:space-between; gap:1rem; align-items:center; margin-top:1rem; border:1px solid #d8d1c2; border-radius:0.9rem; background:#fffaf0; padding:0.85rem 1rem; }
    .billing-context div { display:grid; gap:0.2rem; }
    .billing-context span { color:#5f6b76; }
    .context-actions { display:flex; flex-wrap:wrap; gap:0.75rem; justify-content:flex-end; }
    .receiver-selector { grid-column:1 / -1; display:grid; gap:0.75rem; }
    .suggestions { border:1px solid #d8d1c2; border-radius:0.9rem; background:#fcfbf8; padding:0.5rem; }
    .suggestions ul { list-style:none; margin:0; padding:0; display:grid; gap:0.35rem; }
    .suggestion-button { width:100%; display:grid; gap:0.15rem; text-align:left; border:1px solid #ece5d7; border-radius:0.8rem; background:#fff; color:#182533; padding:0.75rem 0.9rem; cursor:pointer; }
    .suggestion-button:hover { background:#f7f2e7; }
    .suggestion-button small { color:#5f6b76; }
    .selected-receiver { display:flex; justify-content:space-between; gap:1rem; align-items:center; border:1px solid #d8d1c2; border-radius:0.9rem; background:#fffaf0; padding:0.85rem 1rem; }
    .selected-receiver div { display:grid; gap:0.2rem; }
    .selected-receiver span { color:#5f6b76; }
    .recovery-panel { display:grid; gap:0.75rem; margin-top:1rem; border:1px solid #e6d7b4; border-radius:0.9rem; background:#fff8ea; padding:1rem; }
    .recovery-summary { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .recovery-summary div { display:grid; gap:0.2rem; }
    .recovery-summary span { color:#5f6b76; }
    .nested-card { padding:0; border:none; background:transparent; }
    .selected-title { margin:0; text-transform:uppercase; letter-spacing:0.08em; font-size:0.72rem; color:#8a6a32; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    .button-row { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    button, a { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.danger { background:#7a2020; }
    button:disabled { opacity:0.6; cursor:wait; }
    .error { margin:0; color:#7a2020; }
    @media (max-width: 720px) {
      .search-row { flex-direction:column; align-items:stretch; }
      .billing-context { flex-direction:column; align-items:stretch; }
      .selected-receiver { flex-direction:column; align-items:stretch; }
      .recovery-summary { flex-direction:column; align-items:stretch; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalDocumentOperationsPageComponent implements OnDestroy {
  private readonly api = inject(FiscalDocumentsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedbackService = inject(FeedbackService);
  private readonly productFiscalProfilesApi = inject(ProductFiscalProfilesApiService);
  protected readonly permissionService = inject(PermissionService);
  protected readonly getDisplayLabel = getDisplayLabel;

  protected readonly billingDocumentId = signal<number | null>(parseNumber(this.route.snapshot.queryParamMap.get('billingDocumentId')));
  protected readonly fiscalDocumentId = signal<number | null>(parseNumber(this.route.snapshot.paramMap.get('id')));
  protected readonly loadingPrepare = signal(false);
  protected readonly savingMissingProductProfile = signal(false);
  protected readonly loadingOperation = signal(false);
  protected readonly activeIssuer = signal<IssuerProfileResponse | null>(null);
  protected readonly billingDocumentContext = signal<BillingDocumentLookupResponse | null>(null);
  protected readonly loadingBillingDocumentSearch = signal(false);
  protected readonly billingDocumentSearchResults = signal<BillingDocumentLookupResponse[]>([]);
  protected readonly billingDocumentSearchError = signal<string | null>(null);
  protected readonly billingDocumentSearchTouched = signal(false);
  protected readonly receiverResults = signal<FiscalReceiverSearchResponse[]>([]);
  protected readonly selectedReceiver = signal<FiscalReceiverSearchResponse | null>(null);
  protected readonly fiscalDocument = signal<FiscalDocumentResponse | null>(null);
  protected readonly stampEvidence = signal<FiscalStampResponse | null>(null);
  protected readonly cancellation = signal<FiscalCancellationResponse | null>(null);
  protected readonly lastOperationMessage = signal<string | null>(null);
  protected readonly showStampDetail = signal(false);
  protected readonly showStampXmlPanel = signal(false);
  protected readonly loadingStampXml = signal(false);
  protected readonly stampXmlContent = signal<string | null>(null);
  protected readonly stampXmlError = signal<string | null>(null);
  protected readonly searchingReceivers = signal(false);
  protected readonly receiverSearchError = signal<string | null>(null);
  protected readonly receiverSearchTouched = signal(false);
  protected readonly missingProductFiscalProfile = signal<MissingProductFiscalProfileContext | null>(null);
  protected readonly showMissingProductProfileForm = signal(false);
  protected readonly missingProductProfileError = signal<string | null>(null);
  private readonly pendingPrepareRequest = signal<PrepareFiscalDocumentRequest | null>(null);

  protected readonly receiverQuery = signal('');
  protected billingDocumentQuery = '';
  protected selectedReceiverId: number | null = null;
  protected paymentMethodSat = 'PPD';
  protected paymentFormSat = '99';
  protected paymentCondition = 'CREDITO';
  protected isCreditSale = true;
  protected creditDays = 7;

  protected readonly activeIssuerLabel = computed(() => {
    const issuer = this.activeIssuer();
    return issuer ? `${issuer.rfc} · ${issuer.legalName}` : 'Cargando emisor activo...';
  });
  protected readonly showReceiverSuggestions = computed(() =>
    !this.selectedReceiver()
    && this.receiverQuery().trim().length >= 2
    && (this.searchingReceivers() || this.receiverResults().length > 0 || !!this.receiverSearchError() || this.receiverSearchTouched())
  );

  private receiverSearchTimer: number | null = null;

  constructor() {
    void this.loadIssuer();
    if (this.fiscalDocumentId()) {
      void this.loadFiscalDocument(this.fiscalDocumentId()!);
    } else if (this.billingDocumentId()) {
      void this.loadBillingDocumentContext(this.billingDocumentId()!);
    }
  }

  ngOnDestroy(): void {
    if (this.receiverSearchTimer) {
      window.clearTimeout(this.receiverSearchTimer);
    }
  }

  protected onReceiverQueryChange(value: string): void {
    this.receiverQuery.set(value);
    this.selectedReceiverId = null;
    this.selectedReceiver.set(null);
    this.receiverSearchError.set(null);
    this.receiverSearchTouched.set(false);

    if (this.receiverSearchTimer) {
      window.clearTimeout(this.receiverSearchTimer);
      this.receiverSearchTimer = null;
    }

    const trimmed = value.trim();
    if (trimmed.length < 2) {
      this.receiverResults.set([]);
      return;
    }

    this.receiverSearchTimer = window.setTimeout(() => {
      void this.searchReceivers(trimmed);
    }, 250);
  }

  protected selectReceiver(receiver: FiscalReceiverSearchResponse): void {
    this.selectedReceiver.set(receiver);
    this.selectedReceiverId = receiver.id;
    this.receiverQuery.set(`${receiver.rfc} · ${receiver.legalName}`);
    this.receiverResults.set([]);
    this.receiverSearchError.set(null);
  }

  protected clearSelectedReceiver(): void {
    this.selectedReceiver.set(null);
    this.selectedReceiverId = null;
    this.receiverQuery.set('');
    this.receiverResults.set([]);
    this.receiverSearchError.set(null);
    this.receiverSearchTouched.set(false);
  }

  protected receiverTrackBy(index: number, receiver: FiscalReceiverSearchResponse): number {
    return receiver.id;
  }

  protected async searchBillingDocuments(): Promise<void> {
    const query = this.billingDocumentQuery.trim();
    this.billingDocumentSearchTouched.set(true);
    this.billingDocumentSearchError.set(null);

    if (!query) {
      this.billingDocumentSearchResults.set([]);
      return;
    }

    this.loadingBillingDocumentSearch.set(true);
    try {
      this.billingDocumentSearchResults.set(await firstValueFrom(this.api.searchBillingDocuments(query)));
    } catch (error) {
      this.billingDocumentSearchResults.set([]);
      this.billingDocumentSearchError.set(extractApiErrorMessage(error, 'No fue posible buscar documentos de facturación.'));
    } finally {
      this.loadingBillingDocumentSearch.set(false);
    }
  }

  protected async selectBillingDocument(billingDocument: BillingDocumentLookupResponse): Promise<void> {
    this.billingDocumentSearchResults.set([]);
    this.billingDocumentSearchTouched.set(false);
    await this.loadBillingDocumentContext(billingDocument.billingDocumentId, true);
  }

  protected async openExistingFiscalDocument(billingDocument: BillingDocumentLookupResponse): Promise<void> {
    if (!billingDocument.fiscalDocumentId) {
      return;
    }

    await this.loadFiscalDocument(billingDocument.fiscalDocumentId, true);
  }

  protected async clearBillingDocumentSelection(): Promise<void> {
    this.clearMissingProductFiscalProfileState();
    this.billingDocumentContext.set(null);
    this.billingDocumentId.set(null);
    this.billingDocumentQuery = '';
    this.billingDocumentSearchResults.set([]);
    this.billingDocumentSearchTouched.set(false);
    this.fiscalDocument.set(null);
    this.stampEvidence.set(null);
    this.cancellation.set(null);
    this.fiscalDocumentId.set(null);
    await this.router.navigate(['/app/fiscal-documents'], { queryParams: {} });
  }

  private async searchReceivers(query: string): Promise<void> {
    this.searchingReceivers.set(true);
    this.receiverSearchError.set(null);

    try {
      const results = await firstValueFrom(this.api.searchReceivers(query));
      this.receiverResults.set(results.slice(0, 5));
      this.receiverSearchTouched.set(true);
    } catch (error) {
      this.receiverResults.set([]);
      this.receiverSearchTouched.set(true);
      this.receiverSearchError.set(extractApiErrorMessage(error, 'No fue posible buscar receptores.'));
    } finally {
      this.searchingReceivers.set(false);
    }
  }

  protected async prepare(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (!billingDocumentId || !this.selectedReceiverId) {
      this.feedbackService.show('error', 'Selecciona un receptor y abre esta página desde un documento de facturación.');
      return;
    }

    const request: PrepareFiscalDocumentRequest = {
      fiscalReceiverId: this.selectedReceiverId,
      issuerProfileId: this.activeIssuer()?.id ?? null,
      paymentMethodSat: this.paymentMethodSat,
      paymentFormSat: this.paymentFormSat,
      paymentCondition: this.paymentCondition,
      isCreditSale: this.isCreditSale,
      creditDays: this.creditDays
    };

    this.pendingPrepareRequest.set(request);
    await this.executePrepare(request);
  }

  protected openMissingProductProfileForm(): void {
    this.showMissingProductProfileForm.set(true);
    this.missingProductProfileError.set(null);
  }

  protected closeMissingProductProfileForm(): void {
    this.showMissingProductProfileForm.set(false);
    this.missingProductProfileError.set(null);
  }

  private clearMissingProductFiscalProfileState(): void {
    this.missingProductFiscalProfile.set(null);
    this.showMissingProductProfileForm.set(false);
    this.missingProductProfileError.set(null);
    this.pendingPrepareRequest.set(null);
  }

  protected async saveMissingProductProfile(request: UpsertProductFiscalProfileRequest): Promise<void> {
    if (!this.permissionService.canWriteMasterData() || this.savingMissingProductProfile()) {
      return;
    }

    this.savingMissingProductProfile.set(true);
    this.missingProductProfileError.set(null);

    try {
      await firstValueFrom(this.productFiscalProfilesApi.create(request));
      this.feedbackService.show('success', `Perfil fiscal del producto ${request.internalCode} creado.`);
      const pendingRequest = this.pendingPrepareRequest();
      this.closeMissingProductProfileForm();
      if (pendingRequest) {
        await this.executePrepare(pendingRequest);
      }
    } catch (error) {
      this.showMissingProductProfileForm.set(true);
      this.missingProductProfileError.set(extractApiErrorMessage(error, 'No fue posible crear el perfil fiscal del producto.'));
    } finally {
      this.savingMissingProductProfile.set(false);
    }
  }

  private async executePrepare(request: PrepareFiscalDocumentRequest): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (!billingDocumentId) {
      return;
    }

    this.loadingPrepare.set(true);
    this.closeMissingProductProfileForm();

    try {
      const response = await firstValueFrom(this.api.prepareFiscalDocument(billingDocumentId, request));

      if (!response.fiscalDocumentId) {
        this.pendingPrepareRequest.set(null);
        this.feedbackService.show('error', response.errorMessage || 'No se pudo preparar el documento fiscal.');
        return;
      }

      this.clearMissingProductFiscalProfileState();
      await this.loadFiscalDocument(response.fiscalDocumentId);
      this.feedbackService.show('success', 'Documento fiscal preparado.');
    } catch (error) {
      const missingProfile = extractMissingProductFiscalProfileContext(error);
      if (missingProfile) {
        this.missingProductFiscalProfile.set(missingProfile);
        this.showMissingProductProfileForm.set(true);
        this.feedbackService.show('warning', `Falta el perfil fiscal del producto ${missingProfile.internalCode}. Debes darlo de alta para continuar.`);
        return;
      }

      this.pendingPrepareRequest.set(null);
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loadingPrepare.set(false);
    }
  }

  protected async stamp(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.stampFiscalDocument(fiscalDocumentId, { retryRejected: false }));
      this.lastOperationMessage.set(response.errorMessage || `Resultado del timbrado: ${getDisplayLabel(response.outcome)}`);
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
    });
  }

  protected async cancel(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || !window.confirm('¿Cancelar este documento fiscal timbrado? Esta acción es operativamente sensible.')) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.cancelFiscalDocument(fiscalDocumentId, { cancellationReasonCode: '02' }));
      this.lastOperationMessage.set(response.errorMessage || `Resultado de la cancelación: ${getDisplayLabel(response.outcome)}`);
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadCancellation(fiscalDocumentId);
    });
  }

  protected async refreshStatus(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.refreshStatus(fiscalDocumentId));
      this.lastOperationMessage.set(
        response.providerMessage || response.errorMessage || `Último estatus externo: ${getDisplayLabel(response.lastKnownExternalStatus ?? 'Unknown')}`
      );
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
      await this.loadCancellation(fiscalDocumentId, false);
    });
  }

  protected toggleStampDetail(): void {
    this.showStampDetail.update((value) => !value);
  }

  protected async openStampXml(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    this.showStampXmlPanel.set(true);
    this.loadingStampXml.set(true);
    this.stampXmlError.set(null);
    this.stampXmlContent.set(null);

    try {
      this.stampXmlContent.set(await firstValueFrom(this.api.getStampXml(fiscalDocumentId)));
    } catch (error) {
      this.stampXmlError.set(extractErrorMessage(error));
    } finally {
      this.loadingStampXml.set(false);
    }
  }

  protected closeStampXml(): void {
    this.showStampXmlPanel.set(false);
    this.loadingStampXml.set(false);
    this.stampXmlContent.set(null);
    this.stampXmlError.set(null);
  }

  private async loadIssuer(): Promise<void> {
    try {
      this.activeIssuer.set(await firstValueFrom(this.api.getActiveIssuer()));
    } catch {
      this.feedbackService.show('warning', 'No se pudo cargar el perfil activo del emisor.');
    }
  }

  private async loadFiscalDocument(fiscalDocumentId: number, syncRoute = false): Promise<void> {
    this.fiscalDocumentId.set(fiscalDocumentId);
    this.showStampDetail.set(false);
    this.closeStampXml();
    const document = await firstValueFrom(this.api.getFiscalDocumentById(fiscalDocumentId));
    this.fiscalDocument.set(document);
    await this.loadBillingDocumentContext(document.billingDocumentId, false);

    if (syncRoute) {
      await this.router.navigate(['/app/fiscal-documents', fiscalDocumentId], {
        queryParams: { billingDocumentId: document.billingDocumentId }
      });
    }

    await this.loadStamp(fiscalDocumentId, false);
    await this.loadCancellation(fiscalDocumentId, false);
  }

  private async loadBillingDocumentContext(billingDocumentId: number, syncRoute = false): Promise<void> {
    try {
      const billingDocument = await firstValueFrom(this.api.getBillingDocumentById(billingDocumentId));
      this.clearMissingProductFiscalProfileState();
      this.billingDocumentContext.set(billingDocument);
      this.billingDocumentId.set(billingDocument.billingDocumentId);
      this.billingDocumentQuery = `${billingDocument.billingDocumentId}`;

      if (syncRoute) {
        await this.router.navigate(['/app/fiscal-documents'], {
          queryParams: { billingDocumentId: billingDocument.billingDocumentId }
        });
      }

      if (billingDocument.fiscalDocumentId && !this.fiscalDocument()) {
        await this.loadFiscalDocument(billingDocument.fiscalDocumentId, syncRoute);
      }
    } catch (error) {
      this.billingDocumentContext.set(null);
      this.billingDocumentSearchError.set(extractApiErrorMessage(error, 'No fue posible cargar el documento de facturación.'));
    }
  }

  private async loadStamp(fiscalDocumentId: number, notifyOnMissing = false): Promise<void> {
    try {
      this.stampEvidence.set(await firstValueFrom(this.api.getStamp(fiscalDocumentId)));
    } catch {
      this.stampEvidence.set(null);
      if (notifyOnMissing) {
        this.feedbackService.show('info', 'Aún no hay evidencia de timbrado disponible.');
      }
    }
  }

  private async loadCancellation(fiscalDocumentId: number, notifyOnMissing = false): Promise<void> {
    try {
      this.cancellation.set(await firstValueFrom(this.api.getCancellation(fiscalDocumentId)));
    } catch {
      this.cancellation.set(null);
      if (notifyOnMissing) {
        this.feedbackService.show('info', 'Aún no hay evidencia de cancelación disponible.');
      }
    }
  }

  private async runOperation(operation: () => Promise<void>): Promise<void> {
    this.loadingOperation.set(true);
    try {
      await operation();
    } catch (error) {
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loadingOperation.set(false);
    }
  }
}

function parseNumber(value: string | null): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function extractErrorMessage(error: unknown): string {
  return extractApiErrorMessage(error);
}
