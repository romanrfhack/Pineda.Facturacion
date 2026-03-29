import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import {
  BillingDocumentLookupResponse,
  CancelFiscalDocumentRequest,
  FiscalCancellationResponse,
  FiscalDocumentResponse,
  FiscalDocumentEmailDraftResponse,
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
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { FiscalReceiverFormComponent } from '../../catalogs/components/fiscal-receiver-form.component';
import { FiscalReceiver, FiscalReceiverSatCatalogOption, UpsertFiscalReceiverRequest, UpsertProductFiscalProfileRequest } from '../../catalogs/models/catalogs.models';
import { extractMissingProductFiscalProfileContext, MissingProductFiscalProfileContext } from '../application/missing-product-fiscal-profile';
import { buildFiscalDocumentFileName } from '../application/fiscal-document-file-name';

@Component({
  selector: 'app-fiscal-document-operations-page',
  imports: [FormsModule, RouterLink, FiscalDocumentCardComponent, FiscalStampEvidenceCardComponent, FiscalCancellationCardComponent, FiscalStampEvidenceDetailComponent, XmlViewerPanelComponent, ProductFiscalProfileFormComponent, FiscalReceiverFormComponent],
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
                    <div class="empty-receiver-state">
                      <p class="helper">Sin coincidencias.</p>
                      @if (permissionService.canWriteMasterData()) {
                        <button type="button" class="link-button" (click)="openReceiverCreateModal()">
                          Agregar receptor
                        </button>
                      }
                    </div>
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

            @if (activeReceiverSpecialFields().length) {
              <section class="receiver-selector special-fields-section">
                <div>
                  <p class="selected-title">Campos especiales de facturación</p>
                  <strong>Datos adicionales requeridos por el receptor</strong>
                  <span class="helper">Captura los valores requeridos antes de preparar el documento fiscal.</span>
                </div>

                <div class="form-grid">
                  @for (field of activeReceiverSpecialFields(); track field.fieldCode) {
                    <label>
                      <span>{{ field.label }} @if (field.isRequired) { <strong>*</strong> }</span>
                      <input
                        [(ngModel)]="field.value"
                        [name]="'specialField-' + field.fieldCode"
                        [attr.maxLength]="field.maxLength ?? null"
                        [attr.placeholder]="field.helpText || null"
                        [type]="resolveSpecialFieldInputType(field.dataType)"
                      />
                      @if (field.helpText) {
                        <small class="helper">{{ field.helpText }}</small>
                      }
                    </label>
                  }
                </div>
              </section>
            }

            <label>
              <span>Emisor activo</span>
              <input [value]="activeIssuerLabel()" disabled />
            </label>

            <label>
              <span>Método de pago SAT</span>
              <select
                [ngModel]="paymentMethodSat"
                (ngModelChange)="onPaymentMethodChange($event)"
                name="paymentMethodSat"
                required>
                <option value="">Selecciona método de pago</option>
                @for (option of paymentMethodOptions(); track option.code) {
                  <option [value]="option.code">{{ option.code }} - {{ option.description }}</option>
                }
              </select>
              <small class="helper">Selecciona primero el método SAT para guiar la forma de pago.</small>
            </label>

            <label>
              <span>Forma de pago SAT</span>
              <select
                [ngModel]="paymentFormSat"
                (ngModelChange)="onPaymentFormChange($event)"
                name="paymentFormSat"
                required>
                <option value="">Selecciona forma de pago</option>
                @for (option of availablePaymentFormOptions(); track option.code) {
                  <option [value]="option.code">{{ option.code }} - {{ option.description }}</option>
                }
              </select>
              @if (normalizedPaymentMethodSat() === 'PPD') {
                <small class="helper">Para PPD, la forma de pago SAT se restringe a 99 - Por definir.</small>
              } @else {
                <small class="helper">Para PUE, selecciona una forma de pago real del catálogo SAT.</small>
              }
            </label>

            <label>
              <span>Condición de pago</span>
              <input
                [ngModel]="paymentCondition"
                (ngModelChange)="onPaymentConditionChange($event)"
                name="paymentCondition"
                maxlength="50"
                required
              />
              <small class="helper">Texto comercial controlado por la aplicación. No es un catálogo SAT cerrado.</small>
            </label>

            <label class="checkbox">
              <input
                [ngModel]="isCreditSale"
                (ngModelChange)="onCreditSaleChange($event)"
                name="isCreditSale"
                type="checkbox"
                [disabled]="isCreditSaleCheckboxDisabled()"
              />
              <span>Venta a crédito</span>
            </label>

            <label>
              <span>Días de crédito</span>
              <input [ngModel]="creditDays" (ngModelChange)="onCreditDaysChange($event)" name="creditDays" type="number" min="1" />
            </label>

            <button type="submit" [disabled]="!canPrepareFiscalDocument()"> {{ loadingPrepare() ? 'Preparando...' : 'Preparar documento fiscal' }} </button>
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
              <button type="button" class="danger" (click)="openCancelDialog()" [disabled]="loadingOperation() || !canCancelCurrentFiscalDocument()">Cancelar</button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button type="button" class="secondary" (click)="refreshStatus()" [disabled]="loadingOperation()">Actualizar estatus</button>
            }
            @if (currentDocument.status === 'Stamped') {
              <button type="button" class="secondary" (click)="openStampPdf()" [disabled]="loadingPdf() || sendingEmail()">
                {{ loadingPdf() ? 'Abriendo PDF...' : 'Ver PDF' }}
              </button>
              <button type="button" class="secondary" (click)="downloadStampPdf()" [disabled]="loadingPdf() || sendingEmail()">
                {{ loadingPdf() ? 'Descargando PDF...' : 'Descargar PDF' }}
              </button>
              <button type="button" class="secondary" (click)="openEmailComposer()" [disabled]="loadingEmailDraft() || sendingEmail()">
                {{ loadingEmailDraft() ? 'Cargando envío...' : 'Enviar por correo' }}
              </button>
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

      @if (showEmailComposer()) {
        <section class="card nested-card email-panel">
          <h3>Enviar CFDI por correo</h3>
          <p class="helper">Se adjuntarán el XML timbrado y el PDF del CFDI.</p>

          <form class="form-grid" (ngSubmit)="sendEmail()">
            <label class="receiver-selector">
              <span>Correo(s) destino</span>
              <input
                [(ngModel)]="emailRecipientsInput"
                name="emailRecipientsInput"
                placeholder="correo@cliente.com, compras@cliente.com"
              />
              <small class="helper">Puedes capturar uno o varios correos separados por comas o punto y coma.</small>
            </label>

            <label class="receiver-selector">
              <span>Asunto</span>
              <input [(ngModel)]="emailSubject" name="emailSubject" />
            </label>

            <label class="receiver-selector">
              <span>Mensaje</span>
              <textarea [(ngModel)]="emailBody" name="emailBody" rows="5"></textarea>
            </label>

            @if (emailRecipientsError()) {
              <p class="error">{{ emailRecipientsError() }}</p>
            }

            @if (emailDraftError()) {
              <p class="error">{{ emailDraftError() }}</p>
            }

            <div class="context-actions">
              <button type="submit" [disabled]="sendingEmail() || !hasValidEmailRecipients()">
                {{ sendingEmail() ? 'Enviando...' : 'Enviar CFDI' }}
              </button>
              <button type="button" class="secondary" (click)="closeEmailComposer()" [disabled]="sendingEmail()">Cancelar</button>
            </div>
          </form>
        </section>
      }

      @if (showCancelDialog()) {
        <section class="modal-backdrop" (click)="closeCancelDialog()">
          <section class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <div>
                <p class="selected-title">Cancelación SAT</p>
                <h3>Cancelar CFDI</h3>
              </div>
              <button type="button" class="secondary" (click)="closeCancelDialog()" [disabled]="loadingOperation()">Cerrar</button>
            </header>

            <p class="helper">Selecciona el motivo SAT de cancelación. Si eliges 01, debes capturar el UUID del comprobante sustituto.</p>

            <form class="form-grid" (ngSubmit)="cancel()">
              <label class="receiver-selector">
                <span>Motivo de cancelación SAT</span>
                <select
                  [ngModel]="cancellationReasonCode"
                  (ngModelChange)="onCancellationReasonChange($event)"
                  name="cancellationReasonCode"
                  required>
                  <option value="">Selecciona motivo de cancelación</option>
                  @for (option of cancellationReasonOptions; track option.code) {
                    <option [value]="option.code">{{ option.code }} - {{ option.description }}</option>
                  }
                </select>
                @if (selectedCancellationReasonHelp()) {
                  <small class="helper">{{ selectedCancellationReasonHelp() }}</small>
                }
              </label>

              @if (requiresCancellationReplacementUuid()) {
                <label class="receiver-selector">
                  <span>UUID de sustitución</span>
                  <input
                    [ngModel]="cancellationReplacementUuid"
                    (ngModelChange)="onCancellationReplacementUuidChange($event)"
                    name="cancellationReplacementUuid"
                    placeholder="UUID del CFDI que sustituye al comprobante cancelado"
                    required
                  />
                  <small class="helper">Obligatorio para el motivo 01.</small>
                </label>
              }

              @if (getCancellationValidationError(); as cancellationValidationError) {
                <p class="error receiver-selector">{{ cancellationValidationError }}</p>
              }

              <div class="context-actions receiver-selector">
                <button type="submit" class="danger" [disabled]="loadingOperation() || !!getCancellationValidationError()">
                  {{ loadingOperation() ? 'Cancelando...' : 'Confirmar cancelación' }}
                </button>
                <button type="button" class="secondary" (click)="closeCancelDialog()" [disabled]="loadingOperation()">Volver</button>
              </div>
            </form>
          </section>
        </section>
      }

      @if (showReceiverCreateModal()) {
        <section class="modal-backdrop" (click)="closeReceiverCreateModal()">
          <section class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <div>
                <p class="selected-title">Documento fiscal</p>
                <h3>Nuevo receptor</h3>
              </div>
              <button type="button" class="secondary" (click)="closeReceiverCreateModal()" [disabled]="savingReceiver()">Cerrar</button>
            </header>

            <p class="helper">Completa los datos fiscales del receptor para continuar sin salir de este flujo.</p>

            <app-fiscal-receiver-form
              [initialValue]="receiverCreateDraft()"
              [submitLabel]="'Crear receptor'"
              [submitting]="savingReceiver()"
              [errorMessage]="receiverCreateError()"
              (submitted)="saveReceiver($event)"
            />
          </section>
        </section>
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
    input, select, textarea { width:100%; box-sizing:border-box; font:inherit; }
    input, select, textarea { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
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
    .empty-receiver-state { display:flex; align-items:center; justify-content:space-between; gap:0.75rem; padding:0.35rem 0.15rem 0.1rem; }
    .suggestions ul { list-style:none; margin:0; padding:0; display:grid; gap:0.35rem; }
    .suggestion-button { width:100%; display:grid; gap:0.15rem; text-align:left; border:1px solid #ece5d7; border-radius:0.8rem; background:#fff; color:#182533; padding:0.75rem 0.9rem; cursor:pointer; }
    .suggestion-button:hover { background:#f7f2e7; }
    .suggestion-button small { color:#5f6b76; }
    .link-button { border:none; background:transparent; color:#182533; padding:0; text-decoration:underline; text-underline-offset:0.18em; }
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
    .modal-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.42); display:grid; place-items:center; padding:1rem; z-index:50; }
    .modal-card { width:min(880px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .modal-header h3 { margin:0.2rem 0 0; }
    button, a { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.danger { background:#7a2020; }
    button:disabled { opacity:0.6; cursor:wait; }
    textarea { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; font:inherit; resize:vertical; }
    .error { margin:0; color:#7a2020; }
    @media (max-width: 720px) {
      .search-row { flex-direction:column; align-items:stretch; }
      .billing-context { flex-direction:column; align-items:stretch; }
      .selected-receiver { flex-direction:column; align-items:stretch; }
      .recovery-summary { flex-direction:column; align-items:stretch; }
      .empty-receiver-state { flex-direction:column; align-items:flex-start; }
      .modal-header { flex-direction:column; align-items:stretch; }
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
  private readonly fiscalReceiversApi = inject(FiscalReceiversApiService);
  protected readonly permissionService = inject(PermissionService);
  protected readonly getDisplayLabel = getDisplayLabel;
  protected readonly cancellationReasonOptions = cancellationReasonOptions;

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
  protected readonly selectedReceiver = signal<FiscalReceiver | null>(null);
  protected readonly fiscalDocument = signal<FiscalDocumentResponse | null>(null);
  protected readonly stampEvidence = signal<FiscalStampResponse | null>(null);
  protected readonly cancellation = signal<FiscalCancellationResponse | null>(null);
  protected readonly lastOperationMessage = signal<string | null>(null);
  protected readonly showCancelDialog = signal(false);
  protected readonly showStampDetail = signal(false);
  protected readonly showStampXmlPanel = signal(false);
  protected readonly loadingStampXml = signal(false);
  protected readonly stampXmlContent = signal<string | null>(null);
  protected readonly stampXmlError = signal<string | null>(null);
  protected readonly loadingPdf = signal(false);
  protected readonly showEmailComposer = signal(false);
  protected readonly loadingEmailDraft = signal(false);
  protected readonly sendingEmail = signal(false);
  protected readonly emailDraft = signal<FiscalDocumentEmailDraftResponse | null>(null);
  protected readonly emailDraftError = signal<string | null>(null);
  protected readonly emailRecipientsError = signal<string | null>(null);
  protected readonly searchingReceivers = signal(false);
  protected readonly receiverSearchError = signal<string | null>(null);
  protected readonly receiverSearchTouched = signal(false);
  protected readonly missingProductFiscalProfile = signal<MissingProductFiscalProfileContext | null>(null);
  protected readonly showMissingProductProfileForm = signal(false);
  protected readonly missingProductProfileError = signal<string | null>(null);
  protected readonly showReceiverCreateModal = signal(false);
  protected readonly savingReceiver = signal(false);
  protected readonly receiverCreateError = signal<string | null>(null);
  protected readonly receiverCreateDraft = signal<UpsertFiscalReceiverRequest | null>(null);
  protected readonly specialFieldDrafts = signal<ReceiverSpecialFieldDraft[]>([]);
  protected readonly paymentMethodCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);
  protected readonly paymentFormCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);
  private readonly pendingPrepareRequest = signal<PrepareFiscalDocumentRequest | null>(null);

  protected readonly receiverQuery = signal('');
  protected billingDocumentQuery = '';
  protected selectedReceiverId: number | null = null;
  protected paymentMethodSat = '';
  protected paymentFormSat = '';
  protected paymentCondition = '';
  protected cancellationReasonCode = '';
  protected cancellationReplacementUuid = '';
  protected isCreditSale = true;
  protected creditDays: number | null = 7;
  protected emailRecipientsInput = '';
  protected emailSubject = '';
  protected emailBody = '';
  private paymentConditionEditedByUser = false;

  protected readonly activeIssuerLabel = computed(() => {
    const issuer = this.activeIssuer();
    return issuer ? `${issuer.rfc} · ${issuer.legalName}` : 'Cargando emisor activo...';
  });
  protected readonly showReceiverSuggestions = computed(() =>
    !this.selectedReceiver()
    && this.receiverQuery().trim().length >= 2
    && (this.searchingReceivers() || this.receiverResults().length > 0 || !!this.receiverSearchError() || this.receiverSearchTouched())
  );
  protected readonly activeReceiverSpecialFields = computed(() => this.specialFieldDrafts().filter((field) => field.isActive));
  protected readonly isCreditSaleCheckboxDisabled = computed(
    () => this.normalizedPaymentMethodSat() !== 'PPD'
  );

  private receiverSearchTimer: number | null = null;

  constructor() {
    void this.loadIssuer();
    void this.loadSatCatalogs();
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
    this.specialFieldDrafts.set([]);
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

  protected paymentMethodOptions(): FiscalReceiverSatCatalogOption[] {
    return this.paymentMethodCatalog();
  }

  protected availablePaymentFormOptions(): FiscalReceiverSatCatalogOption[] {
    const paymentMethod = this.normalizedPaymentMethodSat();
    const options = this.paymentFormCatalog();
    if (paymentMethod === 'PPD') {
      return options.filter((option) => option.code === '99');
    }

    if (paymentMethod === 'PUE') {
      return options.filter((option) => option.code !== '99');
    }

    return options;
  }

  protected normalizedPaymentMethodSat(): string {
    return normalizeSatCode(this.paymentMethodSat);
  }

  protected canPrepareFiscalDocument(): boolean {
    return !this.loadingPrepare()
      && !this.savingMissingProductProfile()
      && !!this.selectedReceiverId
      && !this.validateSpecialFields()
      && !this.getPaymentPreparationValidationError();
  }

  protected onPaymentMethodChange(value: string): void {
    this.paymentMethodSat = normalizeSatCode(value);
    this.syncPaymentMethodDependencies(true);
    this.syncCreditSaleWithPaymentMethod();
  }

  protected onPaymentFormChange(value: string): void {
    this.paymentFormSat = normalizeSatCode(value);
  }

  protected onPaymentConditionChange(value: string): void {
    this.paymentConditionEditedByUser = true;
    this.paymentCondition = value;
  }

  protected onCreditSaleChange(value: boolean): void {
    this.isCreditSale = value;
    this.paymentMethodSat = value ? 'PPD' : 'PUE';
    this.syncPaymentMethodDependencies(true);
    this.paymentConditionEditedByUser = false;
    this.applySuggestedPaymentCondition();
  }

  protected onCreditDaysChange(value: number | string | null): void {
    this.creditDays = normalizeCreditDays(value);
    if (this.isCreditSale) {
      this.applySuggestedPaymentCondition();
    }
  }

  protected openCancelDialog(): void {
    if (!this.fiscalDocumentId() || this.loadingOperation() || !this.canCancelCurrentFiscalDocument()) {
      return;
    }

    this.showCancelDialog.set(true);
    this.cancellationReasonCode = this.cancellation()?.cancellationReasonCode ?? '';
    this.cancellationReplacementUuid = this.cancellation()?.replacementUuid ?? '';
  }

  protected closeCancelDialog(): void {
    if (this.loadingOperation()) {
      return;
    }

    this.showCancelDialog.set(false);
  }

  protected onCancellationReasonChange(value: string): void {
    this.cancellationReasonCode = normalizeSatCode(value);
    if (!this.requiresCancellationReplacementUuid()) {
      this.cancellationReplacementUuid = '';
    }
  }

  protected onCancellationReplacementUuidChange(value: string): void {
    this.cancellationReplacementUuid = value;
  }

  protected requiresCancellationReplacementUuid(): boolean {
    return normalizeSatCode(this.cancellationReasonCode) === '01';
  }

  protected selectedCancellationReasonHelp(): string | null {
    const reasonCode = normalizeSatCode(this.cancellationReasonCode);
    return cancellationReasonOptions.find((option) => option.code === reasonCode)?.helpText ?? null;
  }

  protected async selectReceiver(receiver: FiscalReceiverSearchResponse): Promise<void> {
    try {
      const fullReceiver = await firstValueFrom(this.fiscalReceiversApi.getByRfc(receiver.rfc));
      this.applySelectedReceiver(fullReceiver);
    } catch (error) {
      this.receiverSearchError.set(extractApiErrorMessage(error, 'No fue posible cargar el detalle del receptor.'));
    }
  }

  protected clearSelectedReceiver(): void {
    this.selectedReceiver.set(null);
    this.selectedReceiverId = null;
    this.specialFieldDrafts.set([]);
    this.receiverQuery.set('');
    this.receiverResults.set([]);
    this.receiverSearchError.set(null);
    this.receiverSearchTouched.set(false);
  }

  protected openReceiverCreateModal(): void {
    this.receiverCreateError.set(null);
    this.receiverCreateDraft.set({
      rfc: this.receiverQuery().trim(),
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
    });
    this.showReceiverCreateModal.set(true);
  }

  protected closeReceiverCreateModal(): void {
    if (this.savingReceiver()) {
      return;
    }

    this.showReceiverCreateModal.set(false);
    this.receiverCreateError.set(null);
  }

  protected receiverTrackBy(index: number, receiver: FiscalReceiverSearchResponse): number {
    return receiver.id;
  }

  protected resolveSpecialFieldInputType(dataType: string): string {
    return dataType === 'number' || dataType === 'date' ? dataType : 'text';
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

  private applySelectedReceiver(receiver: FiscalReceiver): void {
    this.selectedReceiver.set(receiver);
    this.selectedReceiverId = receiver.id;
    this.receiverQuery.set(`${receiver.rfc} · ${receiver.legalName}`);
    this.receiverResults.set([]);
    this.receiverSearchError.set(null);
    this.receiverSearchTouched.set(false);
    this.specialFieldDrafts.set(
      (receiver.specialFields ?? [])
        .filter((field) => field.isActive)
        .sort((left, right) => left.displayOrder - right.displayOrder)
        .map((field) => ({
          fieldCode: field.code,
          label: field.label,
          dataType: field.dataType,
          isRequired: field.isRequired,
          isActive: field.isActive,
          maxLength: field.maxLength ?? null,
          helpText: field.helpText ?? null,
          value: ''
        }))
    );
  }

  private validateSpecialFields(): string | null {
    for (const field of this.activeReceiverSpecialFields()) {
      const trimmed = field.value.trim();
      if (field.isRequired && !trimmed) {
        return `El campo especial '${field.label}' es requerido.`;
      }

      if (field.maxLength && trimmed.length > field.maxLength) {
        return `El campo especial '${field.label}' excede la longitud máxima permitida de ${field.maxLength} caracteres.`;
      }
    }

    return null;
  }

  protected async saveReceiver(request: UpsertFiscalReceiverRequest): Promise<void> {
    if (!this.permissionService.canWriteMasterData() || this.savingReceiver()) {
      return;
    }

    this.savingReceiver.set(true);
    this.receiverCreateError.set(null);

    try {
      await firstValueFrom(this.fiscalReceiversApi.create(request));
      const createdReceiver = await firstValueFrom(this.fiscalReceiversApi.getByRfc(request.rfc));
      this.applySelectedReceiver(createdReceiver);
      this.showReceiverCreateModal.set(false);
      this.receiverCreateDraft.set(null);
      this.feedbackService.show('success', 'Receptor creado y seleccionado.');
    } catch (error) {
      this.showReceiverCreateModal.set(true);
      this.receiverCreateError.set(extractApiErrorMessage(error, 'No fue posible crear el receptor.'));
    } finally {
      this.savingReceiver.set(false);
    }
  }

  protected async prepare(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (!billingDocumentId || !this.selectedReceiverId) {
      this.feedbackService.show('error', 'Selecciona un receptor y abre esta página desde un documento de facturación.');
      return;
    }

    const paymentValidationError = this.getPaymentPreparationValidationError();
    if (paymentValidationError) {
      this.feedbackService.show('error', paymentValidationError);
      return;
    }

    const specialFieldValidationError = this.validateSpecialFields();
    if (specialFieldValidationError) {
      this.feedbackService.show('error', specialFieldValidationError);
      return;
    }

    const request: PrepareFiscalDocumentRequest = {
      fiscalReceiverId: this.selectedReceiverId,
      issuerProfileId: this.activeIssuer()?.id ?? null,
      paymentMethodSat: this.normalizedPaymentMethodSat(),
      paymentFormSat: normalizeSatCode(this.paymentFormSat),
      paymentCondition: this.paymentCondition.trim(),
      isCreditSale: this.isCreditSale,
      creditDays: this.creditDays,
      specialFields: this.activeReceiverSpecialFields().map((field) => ({
        fieldCode: field.fieldCode,
        value: field.value.trim()
      }))
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
      const missingProfile = extractMissingProductFiscalProfileContext(error, {
        fallbackDescription: this.resolveMissingProductFallbackDescriptionFromError(error)
      });
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
      this.lastOperationMessage.set(
        response.errorMessage || (response.isSuccess ? 'Documento fiscal timbrado correctamente.' : `Resultado del timbrado: ${getDisplayLabel(response.outcome)}`)
      );
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
    });
  }

  protected async cancel(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    const cancellationValidationError = this.getCancellationValidationError();
    if (!fiscalDocumentId) {
      return;
    }

    if (cancellationValidationError) {
      this.feedbackService.show('error', cancellationValidationError);
      return;
    }

    const cancellationRequest = this.buildCancellationRequest();
    if (!cancellationRequest) {
      return;
    }

    if (!window.confirm(buildCancellationConfirmationMessage(cancellationRequest))) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.cancelFiscalDocument(fiscalDocumentId, cancellationRequest));
      this.lastOperationMessage.set(
        response.providerMessage
          || response.supportMessage
          || response.errorMessage
          || `Resultado de la cancelación: ${getDisplayLabel(response.outcome)}`
      );
      this.showCancelDialog.set(false);
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
        response.operationalMessage
          || response.providerMessage
          || response.errorMessage
          || `Último estatus externo: ${getDisplayLabel(response.lastKnownExternalStatus ?? 'Unknown')}`
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

  protected async openStampPdf(): Promise<void> {
    await this.handleStampPdf(false);
  }

  protected async downloadStampPdf(): Promise<void> {
    await this.handleStampPdf(true);
  }

  protected async openEmailComposer(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || this.loadingEmailDraft() || this.sendingEmail()) {
      return;
    }

    this.loadingEmailDraft.set(true);
    this.emailDraftError.set(null);

    try {
      const draft = await firstValueFrom(this.api.getEmailDraft(fiscalDocumentId));
      this.emailDraft.set(draft);
      this.emailRecipientsInput = draft.defaultRecipientEmail ?? '';
      this.emailSubject = draft.suggestedSubject ?? '';
      this.emailBody = draft.suggestedBody ?? '';
      this.emailRecipientsError.set(null);
      this.showEmailComposer.set(true);
    } catch (error) {
      this.emailDraftError.set(extractErrorMessage(error));
      this.showEmailComposer.set(true);
    } finally {
      this.loadingEmailDraft.set(false);
    }
  }

  protected closeEmailComposer(): void {
    this.showEmailComposer.set(false);
    this.emailDraftError.set(null);
    this.emailRecipientsError.set(null);
  }

  protected hasValidEmailRecipients(): boolean {
    const recipients = parseRecipients(this.emailRecipientsInput);
    return recipients.length > 0 && recipients.every(isValidEmail);
  }

  protected async sendEmail(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || this.sendingEmail()) {
      return;
    }

    const recipients = parseRecipients(this.emailRecipientsInput);
    if (recipients.length === 0 || !recipients.every(isValidEmail)) {
      this.emailRecipientsError.set('Captura al menos un correo válido para continuar.');
      return;
    }

    this.sendingEmail.set(true);
    this.emailDraftError.set(null);
    this.emailRecipientsError.set(null);

    try {
      const response = await firstValueFrom(this.api.sendByEmail(fiscalDocumentId, {
        recipients,
        subject: this.emailSubject,
        body: this.emailBody
      }));

      this.lastOperationMessage.set(`CFDI enviado correctamente a ${response.recipients.join(', ')}.`);
      this.feedbackService.show('success', 'CFDI enviado por correo correctamente.');
      this.closeEmailComposer();
    } catch (error) {
      this.emailDraftError.set(extractErrorMessage(error));
    } finally {
      this.sendingEmail.set(false);
    }
  }

  private async loadIssuer(): Promise<void> {
    try {
      this.activeIssuer.set(await firstValueFrom(this.api.getActiveIssuer()));
    } catch {
      this.feedbackService.show('warning', 'No se pudo cargar el perfil activo del emisor.');
    }
  }

  private async loadSatCatalogs(): Promise<void> {
    try {
      const catalog = await firstValueFrom(this.fiscalReceiversApi.getSatCatalog());
      this.paymentMethodCatalog.set(catalog.paymentMethods ?? []);
      this.paymentFormCatalog.set(catalog.paymentForms ?? []);
      if (!this.paymentMethodSat) {
        this.paymentMethodSat = this.isCreditSale ? 'PPD' : 'PUE';
      }
      this.syncPaymentMethodDependencies(false);
      this.syncCreditSaleWithPaymentMethod();
      this.applySuggestedPaymentCondition();
    } catch {
      this.paymentMethodCatalog.set([]);
      this.paymentFormCatalog.set([]);
      this.feedbackService.show('warning', 'No se pudieron cargar los catálogos SAT de método y forma de pago.');
    }
  }

  private async loadFiscalDocument(fiscalDocumentId: number, syncRoute = false): Promise<void> {
    this.fiscalDocumentId.set(fiscalDocumentId);
    this.showStampDetail.set(false);
    this.closeStampXml();
    this.closeEmailComposer();
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
      this.closeEmailComposer();
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

  private resolveMissingProductFallbackDescriptionFromError(error: unknown): string | null {
    const context = extractMissingProductFiscalProfileContext(error);
    if (!context) {
      return null;
    }

    const items = this.billingDocumentContext()?.items ?? [];
    if (!items.length) {
      return null;
    }

    const normalizedCode = context.internalCode.trim().toUpperCase();

    const exactMatch = items.find((item) =>
      item.lineNumber === context.lineNumber
      && (item.productInternalCode?.trim().toUpperCase() ?? '') === normalizedCode
      && item.description?.trim());

    if (exactMatch?.description?.trim()) {
      return exactMatch.description.trim();
    }

    const lineMatch = items.find((item) =>
      item.lineNumber === context.lineNumber
      && item.description?.trim());

    if (lineMatch?.description?.trim()) {
      return lineMatch.description.trim();
    }

    const codeMatch = items.find((item) =>
      (item.productInternalCode?.trim().toUpperCase() ?? '') === normalizedCode
      && item.description?.trim());

    return codeMatch?.description?.trim() || null;
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

  private async handleStampPdf(download: boolean): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || this.loadingPdf()) {
      return;
    }

    this.loadingPdf.set(true);
    try {
      const blob = await firstValueFrom(this.api.getStampPdf(fiscalDocumentId));
      const objectUrl = window.URL.createObjectURL(blob);

      if (download) {
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = buildFiscalDocumentFileName({
          issuerRfc: this.fiscalDocument()?.issuerRfc ?? 'CFDI',
          series: this.fiscalDocument()?.series,
          folio: this.fiscalDocument()?.folio,
          receiverRfc: this.fiscalDocument()?.receiverRfc ?? 'CFDI',
          fallbackToken: this.stampEvidence()?.uuid ?? fiscalDocumentId
        }, 'pdf');
        link.click();
      } else {
        window.open(objectUrl, '_blank', 'noopener,noreferrer');
      }

      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 30000);
    } catch (error) {
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loadingPdf.set(false);
    }
  }

  private buildCancellationRequest(): CancelFiscalDocumentRequest | null {
    const reasonCode = normalizeSatCode(this.cancellationReasonCode);
    if (!reasonCode) {
      return null;
    }

    return {
      cancellationReasonCode: reasonCode,
      replacementUuid: reasonCode === '01'
        ? normalizeOptionalText(this.cancellationReplacementUuid)
        : undefined
    };
  }

  protected canCancelCurrentFiscalDocument(): boolean {
    const document = this.fiscalDocument();
    if (!document) {
      return false;
    }

    return document.status === 'Stamped' || document.status === 'CancellationRejected';
  }

  protected getCancellationValidationError(): string | null {
    const reasonCode = normalizeSatCode(this.cancellationReasonCode);
    if (!reasonCode || !cancellationReasonOptions.some((option) => option.code === reasonCode)) {
      return 'Selecciona un motivo de cancelación SAT válido.';
    }

    if (reasonCode === '01' && !normalizeOptionalText(this.cancellationReplacementUuid)) {
      return "El motivo 01 requiere capturar el UUID de sustitución.";
    }

    return null;
  }

  private getPaymentPreparationValidationError(): string | null {
    const paymentMethod = this.normalizedPaymentMethodSat();
    if (!paymentMethod || !this.paymentMethodCatalog().some((option) => option.code === paymentMethod)) {
      return 'Selecciona un método de pago SAT válido.';
    }

    const paymentForm = normalizeSatCode(this.paymentFormSat);
    const availablePaymentForms = this.availablePaymentFormOptions();
    if (!paymentForm || !availablePaymentForms.some((option) => option.code === paymentForm)) {
      return paymentMethod === 'PPD'
        ? 'Forma de pago SAT debe ser 99 - Por definir cuando el método es PPD.'
        : 'Selecciona una forma de pago SAT válida.';
    }

    if (paymentMethod === 'PUE' && paymentForm === '99') {
      return 'Forma de pago SAT 99 - Por definir no aplica cuando el método es PUE.';
    }

    const paymentCondition = this.paymentCondition.trim();
    if (!paymentCondition) {
      return 'Captura una condición de pago.';
    }

    if (paymentCondition.length > 50) {
      return 'La condición de pago no puede exceder 50 caracteres.';
    }

    if (this.isCreditSale) {
      if (!Number.isInteger(this.creditDays) || (this.creditDays ?? 0) <= 0) {
        return 'Captura días de crédito válidos para una venta a crédito.';
      }

      if (paymentMethod !== 'PPD' || paymentForm !== '99') {
        return 'Las ventas a crédito requieren método PPD y forma de pago 99.';
      }
    }

    return null;
  }

  private syncPaymentMethodDependencies(resetImmediateFormSelection: boolean): void {
    const paymentMethod = this.normalizedPaymentMethodSat();
    const paymentForm = normalizeSatCode(this.paymentFormSat);

    if (paymentMethod === 'PPD') {
      this.paymentFormSat = '99';
      return;
    }

    if (paymentMethod === 'PUE' && paymentForm === '99') {
      this.paymentFormSat = resetImmediateFormSelection ? '' : this.paymentFormSat;
    }
  }

  private syncCreditSaleWithPaymentMethod(): void {
    const method = this.normalizedPaymentMethodSat();
    if (method === 'PPD') {
      this.isCreditSale = true;
    } else {
      this.isCreditSale = false;
    }
    this.applySuggestedPaymentCondition();
  }

  private applySuggestedPaymentCondition(): void {
    if (this.paymentConditionEditedByUser && this.paymentCondition.trim().length > 0) {
      return;
    }

    this.paymentCondition = this.isCreditSale
      ? buildCreditPaymentCondition(this.creditDays)
      : 'Contado';
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

function parseRecipients(value: string): string[] {
  return value
    .split(/[,;\n]+/)
    .map((recipient) => recipient.trim())
    .filter((recipient) => recipient.length > 0);
}

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

function normalizeSatCode(value: string | null | undefined): string {
  return value?.trim().toUpperCase() ?? '';
}

function normalizeOptionalText(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

function normalizeCreditDays(value: number | string | null): number | null {
  if (typeof value === 'number') {
    return Number.isFinite(value) && value > 0 ? Math.trunc(value) : null;
  }

  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? Math.trunc(parsed) : null;
  }

  return null;
}

function buildCreditPaymentCondition(creditDays: number | null): string {
  return creditDays && creditDays > 0
    ? `Crédito a ${creditDays} días`
    : 'Crédito';
}

function buildCancellationConfirmationMessage(request: CancelFiscalDocumentRequest): string {
  const reason = cancellationReasonOptions.find((option) => option.code === request.cancellationReasonCode);
  if (request.cancellationReasonCode === '01' && request.replacementUuid) {
    return `¿Confirmas la cancelación SAT con motivo ${request.cancellationReasonCode} - ${reason?.description ?? 'Motivo SAT'} y UUID de sustitución ${request.replacementUuid}?`;
  }

  return `¿Confirmas la cancelación SAT con motivo ${request.cancellationReasonCode} - ${reason?.description ?? 'Motivo SAT'}?`;
}

const cancellationReasonOptions = [
  {
    code: '01',
    description: 'Comprobante emitido con errores con relación',
    helpText: 'Usa este motivo cuando el CFDI será sustituido por otro comprobante relacionado.'
  },
  {
    code: '02',
    description: 'Comprobante emitido con errores sin relación',
    helpText: 'Usa este motivo cuando el CFDI tiene errores y no será sustituido por un nuevo comprobante relacionado.'
  },
  {
    code: '03',
    description: 'No se llevó a cabo la operación',
    helpText: 'Usa este motivo cuando la operación comercial finalmente no ocurrió.'
  },
  {
    code: '04',
    description: 'Operación nominativa relacionada en una factura global',
    helpText: 'Usa este motivo cuando la operación ya quedó incluida en una factura global nominativa.'
  }
] as const;

interface ReceiverSpecialFieldDraft {
  fieldCode: string;
  label: string;
  dataType: string;
  isRequired: boolean;
  isActive: boolean;
  maxLength: number | null;
  helpText: string | null;
  value: string;
}
