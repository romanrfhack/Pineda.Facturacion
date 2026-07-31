Warning: truncated output (original token count: 40291)
Total output lines: 4449

import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import {
  BillingDocumentRemovedItemTraceResponse,
  BillingDocumentRemovedItemAssignmentTraceResponse,
  BillingDocumentSearchGroupResponse,
  BillingDocumentLookupResponse,
  BillingDocumentLookupItemResponse,
  AssignPendingBillingItemsResponse,
  CancelFiscalDocumentRequest,
  FiscalCancellationResponse,
  FiscalDocumentResponse,
  FiscalDocumentItemResponse,
  FiscalDocumentEmailDraftResponse,
  FiscalReceiverSearchResponse,
  FiscalStampResponse,
  QueryRemoteFiscalStampResponse,
  IssuerProfileResponse,
  LegacyOrderStampingBlockingOrderResponse,
  PendingBillingItemResponse,
  PendingCancellationAuthorizationItemResponse,
  PrepareFiscalDocumentRequest,
  StampAndEmailFiscalDocumentResponse,
} from '../models/fiscal-documents.models';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FiscalDocumentCardComponent } from '../components/fiscal-document-card.component';
import { FiscalStampEvidenceCardComponent } from '../components/fiscal-stamp-evidence-card.component';
import { FiscalCancellationCardComponent } from '../components/fiscal-cancellation-card.component';
import { FiscalStampEvidenceDetailComponent } from '../components/fiscal-stamp-evidence-detail.component';
import { XmlViewerPanelComponent } from '../../../shared/components/xml-viewer-panel.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { findInvalidEmailRecipients, parseEmailRecipients } from '../../../shared/utils/email-recipients';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { ProductFiscalProfileFormComponent } from '../../catalogs/components/product-fiscal-profile-form.component';
import { ProductFiscalProfilesApiService } from '../../catalogs/infrastructure/product-fiscal-profiles-api.service';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { FiscalReceiverFormComponent } from '../../catalogs/components/fiscal-receiver-form.component';
import {
  FiscalReceiver,
  FiscalReceiverSatCatalogOption,
  UpsertFiscalReceiverRequest,
  UpsertProductFiscalProfileRequest,
} from '../../catalogs/models/catalogs.models';
import { OrdersApiService } from '../../orders/infrastructure/orders-api.service';
import {
  extractMissingProductFiscalProfileContext,
  MissingProductFiscalProfileContext,
} from '../application/missing-product-fiscal-profile';
import { buildFiscalDocumentFileName } from '../application/fiscal-document-file-name';
import {
  buildCancellationConfirmationMessage,
  buildCancellationRequest,
  cancellationReasonOptions,
  getCancellationValidationError,
  normalizeSatCode,
  shouldKeepCurrentCancelledCancellation,
} from '../application/fiscal-cancellation-ui';
import { ConfirmationModalComponent } from '../../../shared/components/confirmation-modal.component';

type BillingItemRemovalReasonOption = {
  code: string;
  description: string;
};

type BillingItemRemovalDispositionOption = {
  code: string;
  description: string;
};

const billingItemRemovalReasonOptions: BillingItemRemovalReasonOption[] = [
  { code: 'CustomerRequestedByMistake', description: 'Cliente lo pidió por error' },
  { code: 'DefectiveProduct', description: 'Producto defectuoso' },
  { code: 'WarrantyApplies', description: 'Aplica garantía' },
  { code: 'WrongDocument', description: 'Producto no debe facturarse en este documento' },
  { code: 'WillBeBilledElsewhere', description: 'Producto será facturado en otro documento' },
  { code: 'CaptureOrAssignmentError', description: 'Error de captura / asignación' },
  { code: 'CommercialValidationPending', description: 'Pendiente de validación comercial' },
  { code: 'Other', description: 'Otro' },
];

const billingItemRemovalDispositionOptions: BillingItemRemovalDispositionOption[] = [
  { code: 'PendingBilling', description: 'Pendiente por facturar' },
  { code: 'ExcludedDefinitively', description: 'Excluir definitivamente' },
];

@Component({
  selector: 'app-fiscal-document-operations-page',
  imports: [
    FormsModule,
    RouterLink,
    FiscalDocumentCardComponent,
    FiscalStampEvidenceCardComponent,
    FiscalCancellationCardComponent,
    FiscalStampEvidenceDetailComponent,
    XmlViewerPanelComponent,
    ProductFiscalProfileFormComponent,
    FiscalReceiverFormComponent,
    ConfirmationModalComponent,
  ],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Operaciones de documento fiscal</p>
        <h2>Preparar, timbrar, consultar, cancelar y actualizar estatus</h2>
      </header>

      <section class="card">
        <h3>Seleccionar documento de facturación</h3>
        <p class="helper">
          Busca por ID documento de facturación, ID documento fiscal, ID de orden o ID legado para
          continuar con el flujo fiscal.
        </p>

        <form class="context-search" (ngSubmit)="searchBillingDocuments()">
          <label class="search-label">
            <span>Búsqueda de documento</span>
            <div class="search-row">
              <input
                [(ngModel)]="billingDocumentQuery"
                name="billingDocumentQuery"
                placeholder="ID documento de facturación, ID documento fiscal, ID de orden o ID legado"
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

        @if (billingDocumentSearchGroupsWithItems().length) {
          <section class="context-results">
            @for (group of billingDocumentSearchGroupsWithItems(); track group.field) {
              <section class="context-result-group">
                <h4>{{ group.label }}</h4>
                <div class="context-result-group-items">
                  @for (billingDocument of group.items; track billingDocument.billingDocumentId) {
                    <button
                      type="button"
                      class="context-result"
                      (click)="selectBillingDocument(billingDocument)"
                    >
                      <strong>{{ buildBillingDocumentSearchMatchText(billingDocument) }}</strong>
                      <span
                        >Documento de facturación #{{
                          billingDocument.billingDocumentId
                        }}</span
                      >
                      <span>{{
                        buildBillingDocumentSearchIdentifiersText(billingDocument)
                      }}</span>
                      <small>{{
                        buildBillingDocumentSearchOperationalText(billingDocument)
                      }}</small>
                    </button>
                  }
                </div>
              </section>
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
              <span
                >Orden {{ currentBillingDocument.salesOrderId }} · Legado
                {{ currentBillingDocument.legacyOrderId }}</span
              >
              <span
                >Estatus {{ getDisplayLabel(currentBillingDocument.status) }} ·
                {{ currentBillingDocument.currencyCode }} {{ currentBillingDocument.total }}</span
              >
            </div>

            <div class="context-actions">
              @if (currentBillingDocument.fiscalDocumentId) {
                <button
                  type="button"
                  class="secondary"
                  (click)="openExistingFiscalDocument(currentBillingDocument)"
                >
                  Abrir documento fiscal existente
                </button>
              }
              @if (permissionService.canCancelFiscal()) {
                <button
                  type="button"
                  class="danger"
                  (click)="openCancelBillingDocumentDialog()"
                  [disabled]="loadingOperation() || !canCancelCurrentBillingDocument()"
                >
                  Cancelar documento
                </button>
              }
              <button type="button" class="secondary" (click)="clearBillingDocumentSelection()">
                Cambiar documento
              </button>
            </div>
          </section>

          <section class="associated-orders">
            <div class="associated-orders-header">
              <div>
                <p class="selected-title">Órdenes legacy asociadas</p>
                <strong>{{ associatedOrders().length }} orden(es) en el documento fiscal</strong>
                <span class="helper"
                  >Puedes agregar o quitar órdenes completas antes del timbrado. El total se
                  recalcula automáticamente.</span
                >
              </div>
            </div>

            @if (blockingCanceledOrders().length) {
              <section class="context-warning canceled-orders-warning" aria-live="assertive">
                <strong>Timbrado bloqueado por órdenes canceladas</strong>
                <p>
                  El sistema de origen reportó estas órdenes como canceladas. Retira cada una y
                  vuelve a timbrar; las órdenes vigentes permanecerán en el documento.
                </p>
                <div class="canceled-orders-actions">
                  @for (order of blockingCanceledOrders(); track order.salesOrderId) {
                    <div>
                      <span>Orden {{ order.legacyOrderId }}</span>
                      <button
                        type="button"
                        class="danger"
                        (click)="removeAssociatedOrder(order.salesOrderId)"
                        [disabled]="
                          !canEditCurrentBillingComposition() ||
                          loadingBillingDocumentComposition() ||
                          associatedOrders().length <= 1
                        "
                      >
                        Retirar orden cancelada
                      </button>
                    </div>
                  }
                </div>
                @if (associatedOrders().length <= 1) {
                  <small>
                    Esta es la única orden del documento. Un CFDI no puede quedar vacío; cancela el
                    documento de facturación para liberarla y conservar la trazabilidad.
                  </small>
                }
              </section>
            }

            <div class="associated-orders-list">
              @for (order of associatedOrders(); track order.salesOrderId) {
                <article class="associated-order-card">
                  <div>
                    <strong>{{ order.legacyOrderId }}</strong>
                    <span>Orden interna {{ order.salesOrderId }} · {{ order.customerName }}</span>
                    <small>{{ currentBillingDocument.currencyCode }} {{ order.total }}</small>
                  </div>
                  <div class="context-actions">
                    @if (order.isPrimary) {
                      <span class="helper">Principal</span>
                    }
                    <button
                      type="button"
                      class="secondary"
                      (click)="removeAssociatedOrder(order.salesOrderId)"
                      [disabled]="
                        !canEditCurrentBillingComposition() ||
                        loadingBillingDocumentComposition() ||
                        associatedOrders().length <= 1
                      "
                    >
                      {{ loadingBillingDocumentComposition() ? 'Actualizando...' : 'Quitar orden' }}
                    </button>
                  </div>
                </article>
              }
            </div>

            @if (canEditCurrentBillingComposition()) {
              <form
                class="search-row associated-order-form"
                (ngSubmit)="addLegacyOrderToBillingDocument()"
              >
                <label class="search-label">
                  <span>Agregar otra orden legacy</span>
                  <input
                    [(ngModel)]="additionalLegacyOrderId"
                    name="additionalLegacyOrderId"
                    placeholder="Captura el id legado (noPedido) de la orden a asociar"
                  />
                </label>
                <button
                  type="submit"
                  class="secondary"
                  [disabled]="
                    loadingBillingDocumentComposition() || !additionalLegacyOrderId.trim()
                  "
                >
                  {{ loadingBillingDocumentComposition() ? 'Agregando...' : 'Agregar orden' }}
                </button>
              </form>
              <p class="helper">
                Sugerencia: agrega preferentemente órdenes del mismo cliente. La acción reutiliza la
                importación idempotente existente y solo adjunta la orden completa al documento.
              </p>
            } @else {
              <p class="helper">
                La composición del documento queda bloqueada cuando el CFDI ya no es editable antes
                del timbrado.
              </p>
            }
          </section>

          <section class="associated-orders included-items">
            <div class="associated-orders-header">
              <div>
                <p class="selected-title">Productos incluidos</p>
                <strong
                  >{{ includedBillingItems().length }} producto(s) activos en el documento</strong
                >
                <span class="helper"
                  >Puedes quitar productos completos antes del timbrado. La trazabilidad queda
                  persistida y los totales se recalculan en cada cambio.</span
                >
              </div>
            </div>

            <div class="included-items-list">
              @for (item of includedBillingItems(); track item.billingDocumentItemId) {
                <article class="included-item-card">
                  <div>
                    <strong
                      >Línea {{ item.lineNumber }} ·
                      {{ item.productInternalCode || 'Sin código' }}</strong
                    >
                    <span>{{ item.description }}</span>
                    <small>
                      Orden {{ item.sourceLegacyOrderId }} · Línea origen
                      {{ item.sourceSalesOrderLineNumber }} · Cant. {{ item.quantity }} ·
                      {{ currentBillingDocument.currencyCode }} {{ item.total }}
                    </small>
                    @if (item.sourceBillingDocumentItemRemovalId) {
                      <small
                        >Reutilizado manualmente desde PendingBilling #{{
                          item.sourceBillingDocumentItemRemovalId
                        }}</small
                      >
                    }
                  </div>
                  <div class="context-actions">
                    <button
                      type="button"
                      class="secondary danger"
                      (click)="openRemoveBillingItemDialog(item)"
                      [disabled]="
                        !canEditCurrentBillingComposition() ||
                        loadingBillingDocumentComposition() ||
                        includedBillingItems().length <= 1
                      "
                    >
                      {{
                        loadingBillingDocumentComposition() ? 'Actualizando...' : 'Quitar producto'
                      }}
                    </button>
                  </div>
                </article>
              }
            </div>

            @if (!canEditCurrentBillingComposition()) {
              <p class="helper">
                La edición de productos queda bloqueada cuando el CFDI ya no es editable antes del
                timbrado.
              </p>
            }
          </section>

          <section class="associated-orders pending-billing-items">
            <div class="associated-orders-header">
              <div>
                <p class="selected-title">PendingBilling disponible</p>
                <strong
                  >{{ pendingBillingItems().length }} producto(s) pendientes por facturar</strong
                >
                <span class="helper"
                  >Puedes seleccionar uno o varios productos removidos con destino PendingBilling y
                  agregarlos manualmente a este documento antes del timbrado.</span
                >
              </div>
              @if (canEditCurrentBillingComposition()) {
                <button
                  type="button"
                  class="secondary"
                  (click)="assignSelectedPendingBillingItems()"
                  [disabled]="
                    loadingBillingDocumentComposition() || pendingBillingSelectionCount() === 0
                  "
                >
                  {{
                    loadingBillingDocumentComposition()
                      ? 'Agregando...'
                      : 'Agregar seleccionados al documento'
                  }}
                </button>
              }
            </div>

            @if (pendingBillingItemsError()) {
              <p class="error">{{ pendingBillingItemsError() }}</p>
            } @else if (loadingPendingBillingItems()) {
              <p class="helper">Cargando productos pendientes por facturar...</p>
            } @else if (!pendingBillingItems().length) {
              <p class="helper">No hay productos PendingBilling disponibles para reutilizar.</p>
            } @else {
              <div class="included-items-list">
                @for (item of pendingBillingItems(); track item.removalId) {
                  <article class="included-item-card pending-item-card">
                    <div class="pending-item-selection">
                      <input
                        type="checkbox"
                        [checked]="isPendingBillingItemSelected(item.removalId)"
                        (change)="
                          togglePendingBillingSelection(item.removalId, $any($event.target).checked)
                        "
                        [disabled]="
                          loadingBillingDocumentComposition() || !canEditCurrentBillingComposition()
                        "
                      />
                    </div>
                    <div>
                      <strong
                        >{{ item.productInternalCode || 'Sin código' }} ·
                        {{ item.description }}</strong
                      >
                      <span>{{ item.sourceLegacyOrderId }} · Cliente {{ item.customerName }}</span>
                      <small>
                        Documento origen #{{ item.billingDocumentId }} · CFDI origen
                        {{ item.fiscalDocumentId ?? 'Sin preparar' }} · Línea origen
                        {{ item.sourceSalesOrderLineNumber }} · Cant. {{ item.quantityRemoved }}
                      </small>
                      <small
                        >Motivo {{ getDisplayLabel(item.removalReason) }} · Removido
                        {{ formatUtcToLocal(item.removedAtUtc) }}</small
                      >
                      @if (item.observations) {
                        <small>Observaciones: {{ item.observations }}</small>
                      }
                    </div>
                  </article>
                }
              </div>
            }
          </section>

          <section class="associated-orders removed-items-trace">
            <div class="associated-orders-header">
              <div>
                <p class="selected-title">Trazabilidad de productos removidos</p>
                <strong
                  >{{ removedBillingItems().length }} producto(s) removidos con historial</strong
                >
                <span class="helper"
                  >Aquí puedes validar de qué orden salió cada producto, a qué documento se reasignó
                  y, si ya existe, en qué CFDI final terminó.</span
                >
              </div>
            </div>

            @if (!removedBillingItems().length) {
              <p class="helper">
                Todavía no hay productos removidos con trazabilidad para este documento.
              </p>
            } @else {
              <div class="included-items-list">
                @for (item of removedBillingItems(); track item.removalId) {
                  <article class="included-item-card removed-trace-card">
                    <div>
                      <strong
                        >{{ item.productInternalCode || 'Sin código' }} ·
                        {{ item.description }}</strong
                      >
                      <span
                        >{{ item.sourceLegacyOrderId }} · Cliente {{ item.customerName }} · Línea
                        origen {{ item.sourceSalesOrderLineNumber }}</span
                      >
                      <small>
                        Documento origen #{{ item.billingDocumentId }} · CFDI origen
                        {{ item.fiscalDocumentId ?? 'Sin preparar' }} · Cant. removida
                        {{ item.quantityRemoved }} · Removido
                        {{ formatUtcToLocal(item.removedAtUtc) }}
                      </small>
                      <small
                        >Motivo {{ getDisplayLabel(item.removalReason) }} · Destino
                        {{ getDisplayLabel(item.removalDisposition) }}</small
                      >
                      <small><strong>Estado actual:</strong> {{ item.currentTraceMessage }}</small>
                      @if (item.currentDestinationBillingDocumentId) {
                        <small>
                          Documento destino #{{ item.currentDestinationBillingDocumentId }} ·
                          Estatus {{ item.currentDestinationBillingDocumentStatus || 'Sin dato' }} ·
                          Fiscal {{ item.currentDestinationFiscalDocumentId ?? 'Sin preparar' }}
                        </small>
                      }
                      @if (item.finalCfdiUuid || item.finalCfdiSeries || item.finalCfdiFolio) {
                        <small>
                          <strong>CFDI final:</strong>
                          {{ formatFinalCfdiReference(item) }}
                          @if (item.finalStampedAtUtc) {
                            <span> · Timbrado {{ formatUtcToLocal(item.finalStampedAtUtc) }}</span>
                          }
                        </small>
                      }
                      @if (item.observations) {
                        <small>Observaciones: {{ item.observations }}</small>
                      }
                      @if (item.assignmentHistory?.length) {
                        <div class="trace-history">
                          <small><strong>Movimientos manuales</strong></small>
                          @for (movement of item.assignmentHistory; track movement.assignmentId) {
                            <small>
                              Asignado {{ formatUtcToLocal(movement.assignedAtUtc) }} a documento
                              #{{ movement.destinationBillingDocumentId }} · Fiscal
                              {{ movement.destinationFiscalDocumentId ?? 'Sin preparar' }}
                              @if (
                                movement.destinationFinalCfdiUuid ||
                                movement.destinationFinalCfdiSeries ||
                                movement.destinationFinalCfdiFolio
                              ) {
                                <span>
                                  · CFDI {{ formatMovementFinalCfdiReference(movement) }}</span
                                >
                              }
                              @if (movement.releasedAtUtc) {
                                <span>
                                  · Liberado {{ formatUtcToLocal(movement.releasedAtUtc) }}</span
                                >
                              }
                            </small>
                          }
                        </div>
                      }
                    </div>
                  </article>
                }
              </div>
            }
          </section>
        }
      </section>

      @if (!fiscalDocument() && billingDocumentContext(); as currentBillingDocument) {
        <section class="card">
          <h3>Preparar documento fiscal</h3>
          <p class="helper">
            Id de documento de facturación: <strong>{{ billingDocumentId() }}</strong>
          </p>

          @if (currentBillingDocument.status === 'Cancelled') {
            <p class="helper">
              El documento está cancelado. Su composición permanece visible como histórico, pero ya
              no puede prepararse ni editarse.
            </p>
          } @else {
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
                        <button
                          type="button"
                          class="link-button"
                          (click)="openReceiverCreateModal()"
                        >
                          Agregar receptor
                        </button>
                      }
                    </div>
                  } @else {
                    <ul>
                      @for (
                        receiver of receiverResults();
                        track receiverTrackBy($index, receiver)
                      ) {
                        <li>
                          <button
                            type="button"
                            class="suggestion-button"
                            (click)="selectReceiver(receiver)"
                          >
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
                <p class="helper">
                  Escribe al menos 2 caracteres para buscar por RFC o razón social.
                </p>
              }

              @if (selectedReceiver(); as currentReceiver) {
                <section class="selected-receiver">
                  <div>
                    <p class="selected-title">Receptor seleccionado</p>
                    <strong>{{ currentReceiver.rfc }} · {{ currentReceiver.legalName }}</strong>
                    <span
                      >Código postal {{ currentReceiver.postalCode }} · Régimen
                      {{ currentReceiver.fiscalRegimeCode }}</span
                    >
                    @if (selectedReceiverOperationalWarning()) {
                      <p class="warning">{{ selectedReceiverOperationalWarning() }}</p>
                    }
                  </div>
                  <button type="button" class="secondary" (click)="clearSelectedReceiver()">
                    Cambiar
                  </button>
                </section>
              }
            </section>

            @if (activeReceiverSpecialFields().length) {
              <section class="receiver-selector special-fields-section">
                <div>
                  <p class="selected-title">Campos especiales de facturación</p>
                  <strong>Datos adicionales requeridos por el receptor</strong>
                  <span class="helper"
                    >Captura los valores requeridos antes de preparar el documento fiscal.</span
                  >
                </div>

                <div class="form-grid">
                  @for (field of activeReceiverSpecialFields(); track field.fieldCode) {
                    <label>
                      <span
                        >{{ field.label }}
                        @if (field.isRequired) {
                          <strong>*</strong>
                        }
                      </span>
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
                required
              >
                <option value="">Selecciona método de pago</option>
                @for (option of paymentMethodOptions(); track option.code) {
                  <option [value]="option.code">
                    {{ option.code }} - {{ option.description }}
                  </option>
                }
              </select>
              <small class="helper"
                >Selecciona primero el método SAT para guiar la forma de pago.</small
              >
            </label>

            <label>
              <span>Forma de pago SAT</span>
              <select
                [ngModel]="paymentFormSat"
                (ngModelChange)="onPaymentFormChange($event)"
                name="paymentFormSat"
                required
              >
                <option value="">Selecciona forma de pago</option>
                @for (option of availablePaymentFormOptions(); track option.code) {
                  <option [value]="option.code">
                    {{ option.code }} - {{ option.description }}
                  </option>
                }
              </select>
              @if (normalizedPaymentMethodSat() === 'PPD') {
                <small class="helper"
                  >Para PPD, la forma de pago SAT se restringe a 99 - Por definir.</small
                >
              } @else {
                <small class="helper"
                  >Para PUE, selecciona una forma de pago real del catálogo SAT.</small
                >
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
              <small class="helper"
                >Texto comercial controlado por la aplicación. No es un catálogo SAT cerrado.</small
              >
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
              <input
                [ngModel]="creditDays"
                (ngModelChange)="onCreditDaysChange($event)"
                name="creditDays"
                type="number"
                min="1"
              />
            </label>

            <button type="submit" [disabled]="!canPrepareFiscalDocument()">
              {{ loadingPrepare() ? 'Preparando...' : 'Preparar documento fiscal' }}
            </button>
          </form>

          @if (missingProductFiscalProfile(); as missingProduct) {
            <section class="recovery-panel">
              <div class="recovery-summary">
                <div>
                  <p class="selected-title">Recuperación requerida</p>
                  <strong
                    >Falta el perfil fiscal del producto {{ missingProduct.internalCode }}.</strong
                  >
                  <span>Producto interno: {{ missingProduct.description }}.</span>
                  @if (missingProduct.existingProfileStatus === 'Inactive') {
                    <span>Ya existe un perfil maestro inactivo para este código. Puedes actualizarlo y reactivarlo para continuar.</span>
                  } @else if (missingProduct.existingProfileStatus === 'PendingReview') {
                    <span>Ya existe un perfil maestro para este código, pero quedó marcado como pendiente de revisión SAT. Debes confirmar una clave SAT oficial para continuar.</span>
                  } @else {
                    <span>Debes darlo de alta para continuar.</span>
                  }
                  @if (missingProduct.lineNumber) {
                    <span>Línea {{ missingProduct.lineNumber }} del documento de facturación.</span>
                  }
                  @if (missingProduct.reviewMessages?.length) {
                    <ul class="review-messages">
                      @for (message of missingProduct.reviewMessages ?? []; track message) {
                        <li>{{ message }}</li>
                      }
                    </ul>
                  }
                  @if (missingProduct.suggestions.length) {
                    @if (hasAmbiguousProductFiscalSuggestions(missingProduct)) {
                      <span>Hay mappings históricos ambiguos. Selecciona una opción candidata antes de reintentar.</span>
                    } @else if (hasLegacyProductFiscalSuggestions(missingProduct)) {
                      <span>Hay sugerencias del historial fiscal importado con fuente, motivo y nivel de confianza.</span>
                    } @else {
                      <span>Hay sugerencias determinísticas disponibles. Las coincidencias por descripción requieren confirmación explícita.</span>
                    }
                  }
                </div>

                <div class="context-actions">
                  @if (permissionService.canWriteMasterData()) {
                    <button
                      type="button"
                      class="secondary"
                      (click)="openMissingProductProfileForm()"
                      [disabled]="savingMissingProductProfile()"
                    >
                      Agregar producto fiscal
                    </button>
                  }
                  <button
                    type="button"
                    class="secondary"
                    (click)="closeMissingProductProfileForm()"
                    [disabled]="savingMissingProductProfile()"
                  >
                    Cancelar
                  </button>
                </div>
              </div>

              @if (showMissingProductProfileForm()) {
                <section class="card nested-card">
                  <h4>
                    {{
                      missingProduct.existingProfileStatus === 'Inactive'
                        ? 'Reactivar perfil fiscal de producto'
                        : missingProduct.existingProfileStatus === 'PendingReview'
                          ? 'Actualizar perfil fiscal pendiente de revisión'
                        : 'Alta de perfil fiscal de producto'
                    }}
                  </h4>
                  <app-product-fiscal-profile-form
                    [initialValue]="missingProduct.draft"
                    [recoverySuggestions]="missingProduct.suggestions"
                    [allowExplicitGeneric]="missingProduct.canUseExplicitGeneric"
                    [submitLabel]="resolveMissingProductSubmitLabel(missingProduct)"
                    [submitting]="savingMissingProductProfile()"
                    [errorMessage]="missingProductProfileError()"
                    (submitted)="saveMissingProductProfile($event)"
                  />
                </section>
              }
            </section>
          }
          }
        </section>
      } @else if (!fiscalDocument()) {
        <section class="card">
          <h3>Selecciona un documento de facturación</h3>
          <p class="helper">
            Carga un documento de facturación para preparar su documento fiscal o abrir el documento
            fiscal ya existente.
          </p>
        </section>
      }

      @if (fiscalDocument(); as currentDocument) {
        <app-fiscal-document-card [document]="currentDocument" />

        @if (currentDocument.items.length) {
          <section class="card">
            <div class="associated-orders-header">
              <div>
                <p class="selected-title">Perfil fiscal por línea</p>
                <strong>{{ currentDocument.items.length }} línea(s) con snapshot fiscal persistido</strong>
                <span class="helper">
                  Este ajuste solo modifica el snapshot fiscal de este documento. No actualiza el
                  perfil maestro del producto.
                </span>
              </div>
            </div>

            <div class="included-items-list">
              @for (item of currentDocument.items; track item.id) {
                <article class="included-item-card">
                  <div>
                    <strong>Línea {{ item.lineNumber }} · {{ item.internalCode }}</strong>
                    <span>{{ item.description }}</span>
                    <small>
                      SAT {{ item.satProductServiceCode }} · Unidad {{ item.satUnitCode }} ·
                      ObjImp {{ item.taxObjectCode }} · IVA {{ item.vatRate }} · Texto unidad
                      {{ item.unitText || 'N/D' }}
                    </small>
                  </div>
                  <div class="context-actions">
                    @if (canEditCurrentFiscalItemProfile()) {
                      <button
                        type="button"
                        class="secondary"
                        (click)="openFiscalItemProfileDialog(item)"
                        [disabled]="savingFiscalItemProfile()"
                      >
                        {{
                          savingFiscalItemProfile() &&
                          selectedFiscalDocumentItemForProfileEdit()?.id === item.id
                            ? 'Guardando...'
                            : 'Editar perfil fiscal'
                        }}
                      </button>
                    }
                  </div>
                </article>
              }
            </div>

            @if (permissionService.canStampFiscal() && !canEditCurrentFiscalItemProfile()) {
              <p class="helper">
                La edición del perfil fiscal por línea queda bloqueada cuando el CFDI ya no es
                editable antes del timbrado.
              </p>
            }
          </section>
        }

        @if (canSyncCurrentFiscalDocument()) {
          @if (selectedReceiver(); as currentReceiver) {
            <section class="card">
              <h3>Campos especiales del documento fiscal</h3>
              <p class="helper">
                Receptor actual:
                <strong>{{ currentReceiver.rfc }} · {{ currentReceiver.legalName }}</strong
                >. Puedes refrescar el snapshot de campos especiales del CFDI antes de timbrar o
                reintentar.
              </p>

              @if (activeReceiverSpecialFields().length) {
                <div class="form-grid">
                  @for (field of activeReceiverSpecialFields(); track field.fieldCode) {
                    <label>
                      <span
                        >{{ field.label }}
                        @if (field.isRequired) {
                          <strong>*</strong>
                        }
                      </span>
                      <input
                        [(ngModel)]="field.value"
                        [name]="'document-specialField-' + field.fieldCode"
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
              } @else {
                <p class="helper">
                  El receptor actual no tiene campos especiales activos para sincronizar.
                </p>
              }

              <div class="button-row">
                <button
                  type="button"
                  class="secondary"
                  (click)="syncCurrentFiscalDocumentSpecialFields()"
                  [disabled]="loadingOperation() || !canSyncCurrentFiscalDocument()"
                >
                  Sincronizar campos especiales
                </button>
                <a [routerLink]="['/app/catalogs/receivers']">Abrir catálogo de receptores</a>
              </div>
            </section>
          }
        }

        <section class="card actions">
          <h3>Operaciones</h3>
          <div class="button-row">
            @if (permissionService.canStampFiscal()) {
              <button
                type="button"
                (click)="stamp()"
                [disabled]="loadingOperation() || !canStampCurrentFiscalDocument()"
              >
                {{ stampActionLabel() }}
              </button>
            }
            @if (permissionService.canStampFiscal() && canReprepareCurrentFiscalDocument()) {
              <button
                type="button"
                class="secondary"
                (click)="reprepare()"
                [disabled]="loadingOperation() || !canReprepareCurrentFiscalDocument()"
              >
                Regenerar snapshot
              </button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button
                type="button"
                class="danger"
                (click)="openCancelDialog()"
                [disabled]="loadingOperation() || !canCancelCurrentFiscalDocument()"
              >
                {{ cancelActionLabel() }}
              </button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button
                type="button"
                class="secondary"
                (click)="refreshStatus()"
                [disabled]="loadingOperation() || !canRefreshCurrentFiscalDocument()"
              >
                Actualizar estatus
              </button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button
                type="button"
                class="secondary"
                (click)="queryRemoteStamp()"
                [disabled]="loadingOperation() || !canQueryRemoteStamp()"
              >
                Consultar CFDI en PAC
              </button>
            }
            @if (currentDocument.status === 'Stamped') {
              <button
                type="button"
                class="secondary"
                (click)="openStampPdf()"
                [disabled]="loadingPdf() || sendingEmail()"
              >
                {{ loadingPdf() ? 'Abriendo PDF...' : 'Ver PDF' }}
              </button>
              <button
                type="button"
                class="secondary"
                (click)="downloadStampPdf()"
                [disabled]="loadingPdf() || sendingEmail()"
              >
                {{ loadingPdf() ? 'Descargando PDF...' : 'Descargar PDF' }}
              </button>
            }
            <a
              [routerLink]="['/app/accounts-receivable']"
              [queryParams]="{ fiscalDocumentId: currentDocument.id }"
              >Abrir cuentas por cobrar y pagos</a
            >
          </div>

          @if (selectedReceiverOperationalWarning()) {
            <p class="warning">{{ selectedReceiverOperationalWarning() }}</p>
          }
          @if (lastOperationMessage()) {
            <p class="helper">{{ lastOperationMessage() }}</p>
          }
          @if (shouldShowEmailFallbackAction()) {
            <div class="context-actions">
              <button
                type="button"
                class="secondary"
                (click)="reopenEmailComposerAfterStamp()"
                [disabled]="loadingEmailDraft() || sendingEmail()"
              >
                {{ loadingEmailDraft() ? 'Cargando envío...' : 'Completar envío por correo' }}
              </button>
            </div>
          }
          @if (!canRefreshCurrentFiscalDocument()) {
            <p class="helper">
              Actualizar estatus solo está disponible para CFDI timbrados con UUID.
            </p>
          }
          @if (!canQueryRemoteStamp()) {
            <p class="helper">
              Consultar CFDI en PAC solo está disponible para CFDI con UUID persistido.
            </p>
          }
          @if (!canStampCurrentFiscalDocument()) {
            <p class="helper">
              @if (fiscalDocument()?.status === 'DiscardedUnstamped') {
                El snapshot fue descartado localmente. Regénéralo antes de volver a timbrar.
              } @else {
                Timbrar solo está disponible para documentos listos para timbrar o reintentos
                explícitos de rechazo.
              }
            </p>
          }
        </section>
      }

      @if (permissionService.canCancelFiscal()) {
        <section class="card">
          <div class="associated-orders-header">
            <div>
              <p class="selected-title">Cancelaciones con aceptación</p>
              <h3>Solicitudes pendientes de autorización</h3>
              <span class="helper"
                >Consulta los CFDI con solicitud pendiente y responde manualmente autorizar o
                rechazar sin salir del flujo fiscal.</span
              >
            </div>
            <button
              type="button"
              class="secondary"
              (click)="loadPendingCancellationAuthorizations()"
              [disabled]="loadingPendingCancellationAuthorizations() || loadingOperation()"
            >
              {{
                loadingPendingCancellationAuthorizations()
                  ? 'Actualizando...'
                  : pendingCancellationAuthorizationsLoaded()
                    ? 'Actualizar pendientes'
                    : 'Consultar pendientes'
              }}
            </button>
          </div>

          @if (pendingCancellationAuthorizationsError()) {
            <section class="context-warning" aria-live="polite">
              <p>{{ pendingCancellationAuthorizationsError() }}</p>
              @if (pendingCancellationAuthorizationsErrorDetail()) {
                <small>Detalle técnico: {{ pendingCancellationAuthorizationsErrorDetail() }}</small>
              }
            </section>
          } @else if (loadingPendingCancellationAuthorizations()) {
            <p class="helper">Consultando autorizaciones pendientes...</p>
          } @else if (!pendingCancellationAuthorizationsLoaded()) {
            <p class="helper">
              Consulta esta bandeja solo cuando necesites revisar solicitudes de cancelación
              pendientes con el proveedor.
            </p>
          } @else if (!pendingCancellationAuthorizations().length) {
            <p class="helper">No hay solicitudes pendientes de autorización en este momento.</p>
          } @else {
            <div class="associated-orders-list">
              @for (item of pendingCancellationAuthorizations(); track item.uuid) {
                <article class="associated-order-card">
                  <div>
                    <strong>{{ item.uuid }}</strong>
                    <span
                      >{{ item.issuerRfc || 'RFC emisor N/D' }} →
                      {{ item.receiverRfc || 'RFC receptor N/D' }}</span
                    >
                    <small>
                      @if (item.fiscalDocumentId) {
                        Documento local #{{ item.fiscalDocumentId }} ·
                        {{ getDisplayLabel(item.fiscalDocumentStatus || 'Unknown') }}
                      } @else {
                        Sin correlación local
                      }
                    </small>
                    @if (item.localOperationalMessage) {
                      <small>{{ item.localOperationalMessage }}</small>
                    }
                    @if (item.providerMessage) {
                      <small>Mensaje PAC: {{ item.providerMessage }}</small>
                    }
                  </div>
                  <div class="context-actions">
                    <button
                      type="button"
                      (click)="respondCancellationAuthorization(item, 'Accept')"
                      [disabled]="loadingOperation()"
                    >
                      Autorizar
                    </button>
                    <button
                      type="button"
                      class="secondary danger"
                      (click)="respondCancellationAuthorization(item, 'Reject')"
                      [disabled]="loadingOperation()"
                    >
                      Rechazar
                    </button>
                  </div>
                </article>
              }
            </div>
          }
        </section>
      }

      @if (stampEvidence(); as currentStamp) {
        @if (shouldExplainStampEvidenceAsHistory()) {
          <section class="card">
            <h3>Estado actual vs evidencia histórica</h3>
            <p class="helper">
              El estado vigente del documento es
              <strong>{{ getDisplayLabel(fiscalDocument()?.status || 'Unknown') }}</strong
              >. La tarjeta siguiente muestra sólo la última evidencia o intento persistido en
              <code>/stamp</code>.
            </p>
          </section>
        }
        <app-fiscal-stamp-evidence-card
          [stamp]="currentStamp"
          (detailsRequested)="toggleStampDetail()"
          (xmlRequested)="openStampXml()"
          (remoteQueryRequested)="queryRemoteStamp()"
        />
        @if (showStampDetail()) {
          <app-fiscal-stamp-evidence-detail [stamp]="currentStamp" />
        }
      } @else if (fiscalDocument()) {
        <section class="card">
          <h3>Evidencia de timbrado</h3>
          <p class="helper">
            Aún no hay evidencia de timbrado disponible. Primero timbra el documento fiscal para
            consultar metadatos persistidos y XML.
          </p>
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
          <p class="helper">{{ emailComposerContextMessage() }}</p>

          <form class="form-grid" (ngSubmit)="sendEmail()">
            <label class="receiver-selector">
              <span>Correo(s) destino</span>
              <input
                [(ngModel)]="emailRecipientsInput"
                name="emailRecipientsInput"
                placeholder="correo@cliente.com, compras@cliente.com"
              />
              <small class="helper"
                >Puedes capturar uno o varios correos separados por comas o punto y coma.</small
              >
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
              <button
                type="button"
                class="secondary"
                (click)="closeEmailComposer()"
                [disabled]="sendingEmail()"
              >
                {{ emailComposerCloseLabel() }}
              </button>
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
              <button
                type="button"
                class="secondary"
                (click)="closeCancelDialog()"
                [disabled]="loadingOperation()"
              >
                Cerrar
              </button>
            </header>

            <p class="helper">
              Selecciona el motivo SAT de cancelación. Si eliges 01, debes capturar el UUID del
              comprobante sustituto.
            </p>

            <form class="form-grid" (ngSubmit)="cancel()">
              <label class="receiver-selector">
                <span>Motivo de cancelación SAT</span>
                <select
                  [ngModel]="cancellationReasonCode"
                  (ngModelChange)="onCancellationReasonChange($event)"
                  name="cancellationReasonCode"
                  required
                >
                  <option value="">Selecciona motivo de cancelación</option>
                  @for (option of cancellationReasonOptions; track option.code) {
                    <option [value]="option.code">
                      {{ option.code }} - {{ option.description }}
                    </option>
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
                <button
                  type="submit"
                  class="danger"
                  [disabled]="loadingOperation() || !!getCancellationValidationError()"
                >
                  {{ loadingOperation() ? 'Cancelando...' : 'Confirmar cancelación' }}
                </button>
                <button
                  type="button"
                  class="secondary"
                  (click)="closeCancelDialog()"
                  [disabled]="loadingOperation()"
                >
                  Volver
                </button>
              </div>
            </form>
          </section>
        </section>
      }

      <app-confirmation-modal
        [open]="showCancelConfirmationDialog()"
        [eyebrow]="canLocalDiscardCurrentFiscalDocument() ? 'Descarte local' : 'Cancelación SAT'"
        [title]="canLocalDiscardCurrentFiscalDocument() ? 'Descartar snapshot fiscal' : 'Confirmar cancelación'"
        [message]="cancellationConfirmationMessage()"
        [confirmLabel]="canLocalDiscardCurrentFiscalDocument() ? 'Sí, descartar borrador' : 'Sí, cancelar CFDI'"
        cancelLabel="No, volver"
        [busyConfirmLabel]="canLocalDiscardCurrentFiscalDocument() ? 'Descartando...' : 'Cancelando...'"
        [tone]="canLocalDiscardCurrentFiscalDocument() ? 'default' : 'danger'"
        [busy]="loadingOperation()"
        (confirmed)="confirmCancellation()"
        (cancelled)="closeCancelConfirmationDialog()"
      />

      <app-confirmation-modal
        [open]="showBillingCancelConfirmationDialog()"
        eyebrow="Cancelación interna"
        title="Cancelar documento de facturación"
        [message]="billingCancellationConfirmationMessage()"
        confirmLabel="Sí, cancelar documento…10291 tokens truncated…Document.fiscalDocumentId) {
      parts.push(`Documento fiscal #${billingDocument.fiscalDocumentId}`);
    }

    parts.push(`Orden #${billingDocument.salesOrderId}`);
    parts.push(`ID legado ${billingDocument.legacyOrderId}`);
    return parts.join(' · ');
  }

  protected buildBillingDocumentSearchOperationalText(
    billingDocument: BillingDocumentLookupResponse,
  ): string {
    return `Estatus ${this.getDisplayLabel(billingDocument.status)} · ${billingDocument.currencyCode} ${billingDocument.total}`;
  }

  protected canEditCurrentBillingComposition(): boolean {
    if (!this.currentBillingDocumentIsDraft()) {
      return false;
    }

    const fiscalDocument = this.fiscalDocument();
    if (!fiscalDocument) {
      return true;
    }

    if (this.hasPersistedStampedUuid() || fiscalDocument.status === 'Stamped') {
      return false;
    }

    return (
      fiscalDocument.status === 'Draft' ||
      fiscalDocument.status === 'ReadyForStamping' ||
      fiscalDocument.status === 'StampingRejected' ||
      fiscalDocument.status === 'DiscardedUnstamped'
    );
  }

  protected canReprepareCurrentFiscalDocument(): boolean {
    if (!this.currentBillingDocumentIsDraft()) {
      return false;
    }

    const fiscalDocument = this.fiscalDocument();
    if (!fiscalDocument || this.hasPersistedStampedUuid() || !this.hasActiveSelectedReceiver()) {
      return false;
    }

    return (
      fiscalDocument.status === 'Draft' ||
      fiscalDocument.status === 'ReadyForStamping' ||
      fiscalDocument.status === 'StampingRejected' ||
      fiscalDocument.status === 'DiscardedUnstamped'
    );
  }

  protected canEditCurrentFiscalItemProfile(): boolean {
    return this.permissionService.canStampFiscal() && this.canReprepareCurrentFiscalDocument();
  }

  protected cancelActionLabel(): string {
    return this.canLocalDiscardCurrentFiscalDocument() ? 'Descartar borrador' : 'Cancelar';
  }

  protected shouldExplainStampEvidenceAsHistory(): boolean {
    return !!this.stampEvidence() && !this.hasPersistedStampedUuid();
  }

  protected canCancelCurrentBillingDocument(): boolean {
    if (!this.currentBillingDocumentIsDraft()) {
      return false;
    }

    const fiscalDocument = this.fiscalDocument();
    if (!fiscalDocument) {
      return true;
    }

    if (this.hasPersistedStampedUuid() || fiscalDocument.status === 'Stamped') {
      return false;
    }

    return (
      fiscalDocument.status === 'Draft' ||
      fiscalDocument.status === 'ReadyForStamping' ||
      fiscalDocument.status === 'StampingRejected' ||
      fiscalDocument.status === 'DiscardedUnstamped'
    );
  }

  protected openCancelBillingDocumentDialog(): void {
    if (!this.canCancelCurrentBillingDocument() || this.loadingOperation()) {
      return;
    }

    this.showBillingCancelConfirmationDialog.set(true);
  }

  protected closeCancelBillingDocumentDialog(): void {
    this.showBillingCancelConfirmationDialog.set(false);
  }

  protected async confirmBillingCancellation(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (
      !billingDocumentId ||
      !this.showBillingCancelConfirmationDialog() ||
      !this.canCancelCurrentBillingDocument()
    ) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.cancelBillingDocument(billingDocumentId));
      this.lastOperationMessage.set(
        response.errorMessage ||
          this.buildBillingCancellationMessage(
            response.releasedOrderLinkCount,
            response.releasedPendingAssignmentCount,
          ),
      );
      this.showBillingCancelConfirmationDialog.set(false);
      this.resetFiscalContextAfterBillingCancellation();
      await this.loadBillingDocumentContext(response.billingDocumentId, true);
      this.feedbackService.show(
        response.isSuccess ? 'success' : 'error',
        response.errorMessage || 'Documento de facturación cancelado correctamente.',
      );
    });
  }

  protected async addLegacyOrderToBillingDocument(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    const legacyOrderId = this.additionalLegacyOrderId.trim();

    if (
      !billingDocumentId ||
      !legacyOrderId ||
      this.loadingBillingDocumentComposition() ||
      !this.canEditCurrentBillingComposition()
    ) {
      return;
    }

    this.loadingBillingDocumentComposition.set(true);
    try {
      const importResult = await firstValueFrom(this.ordersApi.importLegacyOrder(legacyOrderId));
      if (!importResult.salesOrderId) {
        this.feedbackService.show(
          'error',
          importResult.errorMessage || 'No fue posible importar la orden legacy a asociar.',
        );
        return;
      }

      const response = await firstValueFrom(
        this.api.addSalesOrderToBillingDocument(billingDocumentId, importResult.salesOrderId),
      );
      this.blockingCanceledOrders.set([]);
      this.additionalLegacyOrderId = '';
      this.lastOperationMessage.set(
        response.errorMessage || 'Orden legacy agregada al documento fiscal.',
      );
      await this.reloadCompositionContext();
      this.feedbackService.show('success', 'Orden legacy agregada correctamente.');
    } catch (error) {
      this.feedbackService.show(
        'error',
        extractApiErrorMessage(
          error,
          'No fue posible agregar la orden legacy al documento fiscal.',
        ),
      );
    } finally {
      this.loadingBillingDocumentComposition.set(false);
    }
  }

  protected async removeAssociatedOrder(salesOrderId: number): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (
      !billingDocumentId ||
      this.loadingBillingDocumentComposition() ||
      !this.canEditCurrentBillingComposition()
    ) {
      return;
    }

    if (
      !window.confirm(
        'Esta acción quitará la orden completa del documento fiscal antes del timbrado.',
      )
    ) {
      return;
    }

    this.loadingBillingDocumentComposition.set(true);
    try {
      const response = await firstValueFrom(
        this.api.removeSalesOrderFromBillingDocument(billingDocumentId, salesOrderId),
      );
      this.blockingCanceledOrders.update((orders) =>
        orders.filter((order) => order.salesOrderId !== salesOrderId),
      );
      this.lastOperationMessage.set(
        response.errorMessage || 'Orden legacy quitada del documento fiscal.',
      );
      await this.reloadCompositionContext();
      this.feedbackService.show('success', 'Orden legacy quitada correctamente.');
    } catch (error) {
      this.feedbackService.show(
        'error',
        extractApiErrorMessage(
          error,
          'No fue posible quitar la orden legacy del documento fiscal.',
        ),
      );
    } finally {
      this.loadingBillingDocumentComposition.set(false);
    }
  }

  protected isPendingBillingItemSelected(removalId: number): boolean {
    return this.selectedPendingBillingRemovalIds().includes(removalId);
  }

  protected togglePendingBillingSelection(removalId: number, checked: boolean): void {
    const next = new Set(this.selectedPendingBillingRemovalIds());
    if (checked) {
      next.add(removalId);
    } else {
      next.delete(removalId);
    }

    this.selectedPendingBillingRemovalIds.set(Array.from(next).sort((left, right) => left - right));
  }

  protected async assignSelectedPendingBillingItems(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    const removalIds = this.selectedPendingBillingRemovalIds();

    if (
      !billingDocumentId ||
      !removalIds.length ||
      this.loadingBillingDocumentComposition() ||
      !this.canEditCurrentBillingComposition()
    ) {
      return;
    }

    this.loadingBillingDocumentComposition.set(true);
    try {
      const response = await firstValueFrom(
        this.api.assignPendingBillingItems(billingDocumentId, { removalIds }),
      );
      this.selectedPendingBillingRemovalIds.set([]);
      this.lastOperationMessage.set(
        response.errorMessage ||
          'Productos PendingBilling agregados manualmente al documento fiscal.',
      );
      await this.reloadCompositionContext();
      await this.loadPendingBillingItems();
      this.feedbackService.show('success', 'Productos PendingBilling agregados correctamente.');
    } catch (error) {
      this.feedbackService.show(
        'error',
        extractApiErrorMessage(
          error,
          'No fue posible agregar los productos PendingBilling al documento fiscal.',
        ),
      );
    } finally {
      this.loadingBillingDocumentComposition.set(false);
    }
  }

  protected formatFinalCfdiReference(item: BillingDocumentRemovedItemTraceResponse): string {
    if (item.finalCfdiUuid) {
      return item.finalCfdiUuid;
    }

    const segments = [item.finalCfdiSeries, item.finalCfdiFolio].filter(
      (value): value is string => !!value && !!value.trim(),
    );
    return segments.length ? segments.join('-') : 'CFDI final sin folio visible';
  }

  protected formatMovementFinalCfdiReference(
    movement: BillingDocumentRemovedItemAssignmentTraceResponse,
  ): string {
    if (movement.destinationFinalCfdiUuid) {
      return movement.destinationFinalCfdiUuid;
    }

    const segments = [
      movement.destinationFinalCfdiSeries,
      movement.destinationFinalCfdiFolio,
    ].filter((value): value is string => !!value && !!value.trim());
    return segments.length ? segments.join('-') : 'sin folio visible';
  }

  protected async confirmRemoveBillingItem(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    const selectedItem = this.selectedBillingItemForRemoval();
    const validationError = this.getBillingItemRemovalValidationError();

    if (
      !billingDocumentId ||
      !selectedItem ||
      validationError ||
      this.loadingBillingDocumentComposition() ||
      !this.canEditCurrentBillingComposition()
    ) {
      return;
    }

    this.loadingBillingDocumentComposition.set(true);
    try {
      const response = await firstValueFrom(
        this.api.removeBillingDocumentItem(billingDocumentId, selectedItem.billingDocumentItemId, {
          removalReason: this.billingItemRemovalReason(),
          observations: this.billingItemRemovalObservations().trim() || null,
          removalDisposition: this.billingItemRemovalDisposition(),
        }),
      );
      this.lastOperationMessage.set(
        response.errorMessage ||
          'Producto quitado del documento fiscal con trazabilidad persistida.',
      );
      this.showRemoveBillingItemDialog.set(false);
      this.selectedBillingItemForRemoval.set(null);
      await this.reloadCompositionContext();
      this.feedbackService.show('success', 'Producto quitado correctamente.');
    } catch (error) {
      this.feedbackService.show(
        'error',
        extractApiErrorMessage(error, 'No fue posible quitar el producto del documento fiscal.'),
      );
    } finally {
      this.loadingBillingDocumentComposition.set(false);
    }
  }

  protected async syncCurrentFiscalDocumentSpecialFields(): Promise<void> {
    if (!this.fiscalDocumentId() || !this.canSyncCurrentFiscalDocument()) {
      return;
    }

    await this.runOperation(async () => {
      await this.syncCurrentFiscalDocumentSpecialFieldsCore(true);
    });
  }

  private async searchReceivers(query: string): Promise<void> {
    this.searchingReceivers.set(true);
    this.receiverSearchError.set(null);

    try {
      const results = await firstValueFrom(this.api.searchReceivers(query));
      this.receiverResults.set(results.filter((receiver) => receiver.isActive).slice(0, 5));
      this.receiverSearchTouched.set(true);
    } catch (error) {
      this.receiverResults.set([]);
      this.receiverSearchTouched.set(true);
      this.receiverSearchError.set(
        extractApiErrorMessage(error, 'No fue posible buscar receptores.'),
      );
    } finally {
      this.searchingReceivers.set(false);
    }
  }

  private applySelectedReceiver(
    receiver: FiscalReceiver,
    fiscalDocument: FiscalDocumentResponse | null = this.fiscalDocument(),
  ): void {
    this.selectedReceiver.set(receiver);
    this.selectedReceiverId = receiver.id;
    this.receiverQuery.set(`${receiver.rfc} · ${receiver.legalName}`);
    this.receiverResults.set([]);
    this.receiverSearchError.set(null);
    this.receiverSearchTouched.set(false);
    this.specialFieldDrafts.set(this.buildSpecialFieldDrafts(receiver, fiscalDocument));
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

  private async syncCurrentFiscalDocumentSpecialFieldsCore(
    showSuccessMessage: boolean,
  ): Promise<boolean> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || !this.canSyncCurrentFiscalDocument()) {
      return true;
    }

    const specialFieldValidationError = this.validateSpecialFields();
    if (specialFieldValidationError) {
      this.feedbackService.show('error', specialFieldValidationError);
      return false;
    }

    const response = await firstValueFrom(
      this.api.syncFiscalDocumentSpecialFields(fiscalDocumentId, {
        specialFields: this.activeReceiverSpecialFields().map((field) => ({
          fieldCode: field.fieldCode,
          value: field.value.trim(),
        })),
      }),
    );

    this.lastOperationMessage.set(
      (response.isSuccess ? 'Campos especiales sincronizados correctamente.' : null) ||
        response.errorMessage ||
        `Resultado de la sincronización: ${getDisplayLabel(response.outcome)}`,
    );

    await this.loadFiscalDocument(fiscalDocumentId);

    if (response.isSuccess) {
      if (showSuccessMessage) {
        this.feedbackService.show(
          'success',
          'Campos especiales del documento fiscal sincronizados.',
        );
      }
    } else {
      this.feedbackService.show(
        'error',
        response.errorMessage ||
          'No fue posible sincronizar los campos especiales del documento fiscal.',
      );
    }

    return response.isSuccess;
  }

  private async loadReceiverForFiscalDocument(document: FiscalDocumentResponse): Promise<void> {
    try {
      const receiver = await firstValueFrom(this.fiscalReceiversApi.getByRfc(document.receiverRfc));
      this.applySelectedReceiver(receiver, document);
    } catch {
      this.selectedReceiver.set(null);
      this.selectedReceiverId = document.fiscalReceiverId;
      this.specialFieldDrafts.set([]);
    }
  }

  private buildSpecialFieldDrafts(
    receiver: FiscalReceiver,
    fiscalDocument: FiscalDocumentResponse | null,
  ): ReceiverSpecialFieldDraft[] {
    const valuesByCode = new Map(
      (fiscalDocument?.specialFields ?? [])
        .filter((field) => !!field.fieldCode?.trim())
        .map((field) => [normalizeSpecialFieldCode(field.fieldCode), field.value ?? '']),
    );

    return (receiver.specialFields ?? [])
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
        value: valuesByCode.get(normalizeSpecialFieldCode(field.code)) ?? '',
      }));
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
      if (!createdReceiver.isActive) {
        this.showReceiverCreateModal.set(false);
        this.receiverCreateDraft.set(null);
        this.feedbackService.show(
          'warning',
          'El receptor fiscal se creó en estado inactivo. Reactívalo desde el catálogo de Receptores fiscales para poder usarlo en documentos fiscales.',
        );
        return;
      }

      this.applySelectedReceiver(createdReceiver);
      this.showReceiverCreateModal.set(false);
      this.receiverCreateDraft.set(null);
      this.feedbackService.show('success', 'Receptor creado y seleccionado.');
    } catch (error) {
      this.showReceiverCreateModal.set(true);
      this.receiverCreateError.set(
        extractApiErrorMessage(error, 'No fue posible crear el receptor.'),
      );
    } finally {
      this.savingReceiver.set(false);
    }
  }

  protected async prepare(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (!billingDocumentId) {
      this.feedbackService.show(
        'error',
        'Selecciona un documento de facturación antes de preparar el CFDI.',
      );
      return;
    }

    if (!this.hasActiveSelectedReceiver()) {
      this.feedbackService.show(
        'error',
        'Selecciona un receptor fiscal activo para preparar el documento.',
      );
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

    const selectedReceiver = this.selectedReceiver()!;
    const request: PrepareFiscalDocumentRequest = {
      fiscalReceiverId: selectedReceiver.id,
      issuerProfileId: this.activeIssuer()?.id ?? null,
      paymentMethodSat: this.normalizedPaymentMethodSat(),
      paymentFormSat: normalizeSatCode(this.paymentFormSat),
      paymentCondition: this.paymentCondition.trim(),
      isCreditSale: this.isCreditSale,
      creditDays: this.creditDays,
      specialFields: this.activeReceiverSpecialFields().map((field) => ({
        fieldCode: field.fieldCode,
        value: field.value.trim(),
      })),
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

  protected hasLegacyProductFiscalSuggestions(
    missingProduct: MissingProductFiscalProfileContext,
  ): boolean {
    return missingProduct.suggestions.some((suggestion) => suggestion.source === 'legacy_mapping');
  }

  protected hasAmbiguousProductFiscalSuggestions(
    missingProduct: MissingProductFiscalProfileContext,
  ): boolean {
    return missingProduct.suggestions.some((suggestion) =>
      suggestion.matchKind.toLowerCase().includes('ambiguous'),
    );
  }

  private clearMissingProductFiscalProfileState(): void {
    this.missingProductFiscalProfile.set(null);
    this.showMissingProductProfileForm.set(false);
    this.missingProductProfileError.set(null);
    this.pendingPrepareRequest.set(null);
  }

  protected async saveMissingProductProfile(
    request: UpsertProductFiscalProfileRequest,
  ): Promise<void> {
    const missingProfile = this.missingProductFiscalProfile();
    if (
      !this.permissionService.canWriteMasterData()
      || this.savingMissingProductProfile()
      || !missingProfile
    ) {
      return;
    }

    this.savingMissingProductProfile.set(true);
    this.missingProductProfileError.set(null);

    try {
      if (missingProfile.existingProductFiscalProfileId) {
        await firstValueFrom(
          this.productFiscalProfilesApi.update(
            missingProfile.existingProductFiscalProfileId,
            request,
          ),
        );
      } else {
        await firstValueFrom(this.productFiscalProfilesApi.create(request));
      }

      this.feedbackService.show('success', this.buildMissingProductProfileSavedMessage(missingProfile, request));
      const pendingRequest = this.pendingPrepareRequest();
      this.closeMissingProductProfileForm();
      if (pendingRequest) {
        await this.executePrepare(pendingRequest);
      }
    } catch (error) {
      this.showMissingProductProfileForm.set(true);
      this.missingProductProfileError.set(
        extractApiErrorMessage(
          error,
          missingProfile.existingProductFiscalProfileId
            ? 'No fue posible actualizar o reactivar el perfil fiscal del producto.'
            : 'No fue posible crear el perfil fiscal del producto.',
        ),
      );
    } finally {
      this.savingMissingProductProfile.set(false);
    }
  }

  protected async saveFiscalItemProfile(
    request: UpsertProductFiscalProfileRequest,
  ): Promise<void> {
    const selectedItem = this.selectedFiscalDocumentItemForProfileEdit();
    if (
      !selectedItem
      || !this.canEditCurrentFiscalItemProfile()
      || this.savingFiscalItemProfile()
    ) {
      return;
    }

    this.savingFiscalItemProfile.set(true);
    this.fiscalItemProfileError.set(null);

    try {
      const response = await firstValueFrom(
        this.api.updateFiscalDocumentItemFiscalProfile(selectedItem.id, {
          satProductServiceCode: request.satProductServiceCode,
          satUnitCode: request.satUnitCode,
          taxObjectCode: request.taxObjectCode,
          vatRate: request.vatRate,
          unitText: request.defaultUnitText?.trim() || null,
        }),
      );

      if (response.item) {
        this.applyUpdatedFiscalDocumentItem(response.item);
      }

      this.resetFiscalItemProfileDialogState();
      this.lastOperationMessage.set(
        'Se actualizó el perfil fiscal solo para esta línea del documento actual.',
      );
      this.feedbackService.show(
        'success',
        'Perfil fiscal de la línea actualizado correctamente.',
      );
    } catch (error) {
      this.showFiscalItemProfileDialog.set(true);
      this.fiscalItemProfileError.set(
        extractApiErrorMessage(
          error,
          'No fue posible actualizar el perfil fiscal de esta línea.',
        ),
      );
    } finally {
      this.savingFiscalItemProfile.set(false);
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
      const response = await firstValueFrom(
        this.api.prepareFiscalDocument(billingDocumentId, request),
      );

      if (!response.fiscalDocumentId) {
        this.pendingPrepareRequest.set(null);
        this.feedbackService.show(
          'error',
          response.errorMessage || 'No se pudo preparar el documento fiscal.',
        );
        return;
      }

      this.clearMissingProductFiscalProfileState();
      await this.loadFiscalDocument(response.fiscalDocumentId);
      this.feedbackService.show('success', 'Documento fiscal preparado.');
    } catch (error) {
      const missingProfile = extractMissingProductFiscalProfileContext(error, {
        fallbackDescription: this.resolveMissingProductFallbackDescriptionFromError(error),
      });
      if (missingProfile) {
        this.missingProductFiscalProfile.set(missingProfile);
        this.showMissingProductProfileForm.set(true);
        this.feedbackService.show(
          'warning',
          missingProfile.existingProfileStatus === 'Inactive'
            ? `El perfil fiscal del producto ${missingProfile.internalCode} existe pero está inactivo. Debes reactivarlo o actualizarlo para continuar.`
            : missingProfile.existingProfileStatus === 'PendingReview'
              ? `El perfil fiscal del producto ${missingProfile.internalCode} quedó pendiente de revisión SAT. Debes confirmar una clave SAT oficial para continuar.`
            : `Falta el perfil fiscal del producto ${missingProfile.internalCode}. Debes darlo de alta para continuar.`,
        );
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
    const retryRejected = this.fiscalDocument()?.status === 'StampingRejected';
    if (!fiscalDocumentId) {
      return;
    }

    await this.runOperation(async () => {
      const synchronized = await this.syncCurrentFiscalDocumentSpecialFieldsCore(false);
      if (!synchronized) {
        return;
      }

      let response: StampAndEmailFiscalDocumentResponse;
      try {
        response = await firstValueFrom(
          this.api.stampAndEmailFiscalDocument(fiscalDocumentId, { retryRejected }),
        );
      } catch (error) {
        const blockingOrders = extractBlockingCanceledOrders(error);
        if (!blockingOrders.length) {
          throw error;
        }

        this.blockingCanceledOrders.set(blockingOrders);
        const message = extractApiErrorMessage(
          error,
          'Hay órdenes canceladas que deben retirarse antes de timbrar.',
        );
        this.lastOperationMessage.set(message);
        this.feedbackService.show('warning', message);
        return;
      }

      this.blockingCanceledOrders.set([]);
      const feedbackMessage = this.buildStampAndEmailMessage(response, retryRejected);
      this.pendingAutomaticEmailStatus.set(null);
      this.lastOperationMessage.set(null);
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
      this.feedbackService.show(response.stamped ? 'success' : 'error', feedbackMessage);
      if (response.stamped && shouldOpenEmailComposerAfterStamp(response.email.status)) {
        this.pendingAutomaticEmailStatus.set(response.email.status);
        await this.openEmailComposer(true);
      }
    });
  }

  protected async reprepare(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || !this.canReprepareCurrentFiscalDocument()) {
      if (fiscalDocumentId && this.selectedReceiver()?.isActive === false) {
        this.feedbackService.show(
          'warning',
          'El receptor fiscal seleccionado está inactivo. Reactívalo desde el catálogo de Receptores fiscales o selecciona otro receptor.',
        );
      }
      return;
    }

    await this.runOperation(async () => {
      await firstValueFrom(this.api.reprepareFiscalDocument(fiscalDocumentId));
      await this.loadFiscalDocument(fiscalDocumentId);
      this.lastOperationMessage.set(
        'Snapshot fiscal regenerado desde el documento de facturación actual. El estado vigente vuelve a estar listo para timbrar.',
      );
      this.feedbackService.show('success', 'Snapshot fiscal regenerado correctamente.');
    });
  }

  protected async cancel(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    if (this.canLocalDiscardCurrentFiscalDocument()) {
      if (!this.loadingOperation()) {
        this.showCancelConfirmationDialog.set(true);
      }
      return;
    }

    const cancellationValidationError = this.getCancellationValidationError();
    if (cancellationValidationError) {
      this.feedbackService.show('error', cancellationValidationError);
      return;
    }

    const cancellationRequest = this.buildCancellationRequest();
    if (!cancellationRequest || this.loadingOperation()) {
      return;
    }

    this.showCancelConfirmationDialog.set(true);
  }

  protected async confirmCancellation(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    const cancellationRequest = this.canLocalDiscardCurrentFiscalDocument()
      ? {}
      : this.buildCancellationRequest();
    if (!fiscalDocumentId || !cancellationRequest || !this.showCancelConfirmationDialog()) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(
        this.api.cancelFiscalDocument(fiscalDocumentId, cancellationRequest),
      );
      this.lastOperationMessage.set(null);
      this.showCancelConfirmationDialog.set(false);
      this.showCancelDialog.set(false);
      await this.loadFiscalDocument(fiscalDocumentId);
      if (response.operationType === 'LocalDiscard' && response.isSuccess) {
        this.cancellation.set(null);
      } else {
        await this.loadCancellation(fiscalDocumentId, false);
      }
      await this.loadPendingCancellationAuthorizations();
      const feedbackMessage = this.buildCancellationFeedbackMessage(response);
      this.feedbackService.show(
        response.isSuccess ? 'success' : 'error',
        feedbackMessage,
      );
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
        response.operationalMessage ||
          response.providerMessage ||
          response.supportMessage ||
          response.errorMessage ||
          `Último estatus externo: ${getDisplayLabel(response.lastKnownExternalStatus ?? 'Unknown')}`,
      );
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
      await this.loadCancellation(fiscalDocumentId, false);
      await this.loadPendingCancellationAuthorizations();
    });
  }

  protected async queryRemoteStamp(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || !this.canQueryRemoteStamp()) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.queryRemoteStamp(fiscalDocumentId));
      this.applyRemoteStampQueryResult(response);
      await this.loadStamp(fiscalDocumentId);
    });
  }

  protected async loadPendingCancellationAuthorizations(): Promise<void> {
    this.loadingPendingCancellationAuthorizations.set(true);
    this.pendingCancellationAuthorizationsError.set(null);
    this.pendingCancellationAuthorizationsErrorDetail.set(null);

    try {
      const response = await firstValueFrom(this.api.listPendingCancellationAuthorizations());
      this.pendingCancellationAuthorizations.set(response.items ?? []);
      this.pendingCancellationAuthorizationsLoaded.set(true);
    } catch (error) {
      this.pendingCancellationAuthorizations.set([]);
      this.pendingCancellationAuthorizationsLoaded.set(true);
      this.pendingCancellationAuthorizationsError.set(
        'No se pudieron consultar las autorizaciones pendientes con el proveedor. El documento de facturación no fue afectado.',
      );
      const detail = extractApiErrorMessage(error, '').trim();
      this.pendingCancellationAuthorizationsErrorDetail.set(detail || null);
    } finally {
      this.loadingPendingCancellationAuthorizations.set(false);
    }
  }

  protected async respondCancellationAuthorization(
    item: PendingCancellationAuthorizationItemResponse,
    response: 'Accept' | 'Reject',
  ): Promise<void> {
    const responseLabel = response === 'Accept' ? 'autorizar' : 'rechazar';
    if (
      !window.confirm(
        `¿Confirmas ${responseLabel} la solicitud pendiente de cancelación del UUID ${item.uuid}?`,
      )
    ) {
      return;
    }

    await this.runOperation(async () => {
      const result = await firstValueFrom(
        this.api.respondCancellationAuthorization({
          uuid: item.uuid,
          response,
        }),
      );

      this.lastOperationMessage.set(
        result.providerMessage ||
          result.supportMessage ||
          result.retryAdvice ||
          result.errorMessage ||
          `Respuesta de autorización registrada para ${item.uuid}.`,
      );

      await this.loadPendingCancellationAuthorizations();
      if (result.fiscalDocumentId && result.fiscalDocumentId === this.fiscalDocumentId()) {
        await this.loadFiscalDocument(result.fiscalDocumentId);
      }

      if (result.isSuccess) {
        this.feedbackService.show(
          'success',
          response === 'Accept'
            ? 'La autorización de cancelación fue registrada correctamente.'
            : 'El rechazo de cancelación fue registrado correctamente.',
        );
      }
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

  protected async openEmailComposer(openedAutomatically = false): Promise<void> {
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
    return parseEmailRecipients(this.emailRecipientsInput).length > 0
      && findInvalidEmailRecipients(this.emailRecipientsInput).length === 0;
  }

  protected async sendEmail(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || this.sendingEmail()) {
      return;
    }

    const invalidRecipients = findInvalidEmailRecipients(this.emailRecipientsInput);
    if (invalidRecipients.length > 0) {
      this.emailRecipientsError.set(
        `Correo inválido: ${invalidRecipients.join(', ')}. Para varios correos, sepáralos con punto y coma (;).`,
      );
      return;
    }

    const recipients = parseEmailRecipients(this.emailRecipientsInput);
    if (recipients.length === 0) {
      this.emailRecipientsError.set('Captura al menos un correo válido para continuar.');
      return;
    }

    this.sendingEmail.set(true);
    this.emailDraftError.set(null);
    this.emailRecipientsError.set(null);

    try {
      const response = await firstValueFrom(
        this.api.sendByEmail(fiscalDocumentId, {
          recipients,
          subject: this.emailSubject,
          body: this.emailBody,
        }),
      );

      this.lastOperationMessage.set(
        `CFDI enviado correctamente a ${response.recipients.join(', ')}.`,
      );
      this.pendingAutomaticEmailStatus.set(null);
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
      this.syncPaymentMethodDependencies(false);
      this.syncCreditSaleWithPaymentMethod();
      this.applySuggestedPaymentCondition();
    } catch {
      this.paymentMethodCatalog.set([]);
      this.paymentFormCatalog.set([]);
      this.feedbackService.show(
        'warning',
        'No se pudieron cargar los catálogos SAT de método y forma de pago.',
      );
    }
  }

  private async loadFiscalDocument(fiscalDocumentId: number, syncRoute = false): Promise<void> {
    this.fiscalDocumentId.set(fiscalDocumentId);
    this.showStampDetail.set(false);
    this.closeStampXml();
    this.closeEmailComposer();
    this.pendingAutomaticEmailStatus.set(null);
    this.closeRemoveBillingItemDialog();
    this.resetFiscalItemProfileDialogState();
    const document = await firstValueFrom(this.api.getFiscalDocumentById(fiscalDocumentId));
    this.fiscalDocument.set(document);
    await this.loadReceiverForFiscalDocument(document);
    await this.loadBillingDocumentContext(document.billingDocumentId, false, true);

    if (syncRoute) {
      await this.router.navigate(['/app/fiscal-documents', fiscalDocumentId], {
        queryParams: { billingDocumentId: document.billingDocumentId },
      });
    }

    await this.loadStamp(fiscalDocumentId, false);
    await this.loadCancellation(fiscalDocumentId, false);
  }

  private async loadBillingDocumentContext(
    billingDocumentId: number,
    syncRoute = false,
    preserveCurrentFiscalDocument = false,
  ): Promise<void> {
    try {
      const billingDocument = await firstValueFrom(
        this.api.getBillingDocumentById(billingDocumentId),
      );
      const isDifferentBillingDocument =
        this.billingDocumentId() !== null &&
        this.billingDocumentId() !== billingDocument.billingDocumentId;

      this.clearMissingProductFiscalProfileState();
      this.closeEmailComposer();
      this.closeRemoveBillingItemDialog();
      this.resetFiscalItemProfileDialogState();

      if (!preserveCurrentFiscalDocument && isDifferentBillingDocument) {
        this.resetReceiverSelectionState();
        this.clearOpenFiscalDocumentState();
        this.blockingCanceledOrders.set([]);
      }

      this.billingDocumentContext.set(billingDocument);
      this.selectedPendingBillingRemovalIds.set([]);
      this.billingDocumentId.set(billingDocument.billingDocumentId);
      this.billingDocumentQuery = `${billingDocument.billingDocumentId}`;
      await this.loadPendingBillingItems();

      if (syncRoute) {
        await this.router.navigate(['/app/fiscal-documents'], {
          queryParams: { billingDocumentId: billingDocument.billingDocumentId },
        });
      }

      if (billingDocument.fiscalDocumentId && !this.fiscalDocument()) {
        await this.loadFiscalDocument(billingDocument.fiscalDocumentId, syncRoute);
      }
    } catch (error) {
      this.billingDocumentContext.set(null);
      this.pendingBillingItems.set([]);
      this.pendingBillingItemsError.set(null);
      this.billingDocumentSearchError.set(
        extractApiErrorMessage(error, 'No fue posible cargar el documento de facturación.'),
      );
    }
  }

  private async reloadCompositionContext(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    const billingDocumentId = this.billingDocumentId();

    if (fiscalDocumentId) {
      await this.loadFiscalDocument(fiscalDocumentId);
      return;
    }

    if (billingDocumentId) {
      await this.loadBillingDocumentContext(billingDocumentId);
    }
  }

  private hasActiveSelectedReceiver(): boolean {
    const receiver = this.selectedReceiver();
    return (
      !!this.selectedReceiverId &&
      !!receiver &&
      receiver.id === this.selectedReceiverId &&
      receiver.isActive
    );
  }

  private resetReceiverSelectionState(): void {
    this.selectedReceiver.set(null);
    this.selectedReceiverId = null;
    this.specialFieldDrafts.set([]);
    this.receiverQuery.set('');
    this.receiverResults.set([]);
    this.receiverSearchError.set(null);
    this.receiverSearchTouched.set(false);
  }

  private clearOpenFiscalDocumentState(): void {
    this.fiscalDocument.set(null);
    this.stampEvidence.set(null);
    this.cancellation.set(null);
    this.blockingCanceledOrders.set([]);
    this.pendingAutomaticEmailStatus.set(null);
    this.fiscalDocumentId.set(null);
    this.lastOperationMessage.set(null);
  }

  private applyUpdatedFiscalDocumentItem(item: FiscalDocumentItemResponse): void {
    const fiscalDocument = this.fiscalDocument();
    if (!fiscalDocument) {
      return;
    }

    this.fiscalDocument.set({
      ...fiscalDocument,
      items: fiscalDocument.items.map((currentItem) =>
        currentItem.id === item.id ? item : currentItem,
      ),
    });
  }

  private async loadPendingBillingItems(): Promise<void> {
    this.loadingPendingBillingItems.set(true);
    this.pendingBillingItemsError.set(null);

    try {
      this.pendingBillingItems.set(await firstValueFrom(this.api.listPendingBillingItems()));
    } catch (error) {
      this.pendingBillingItems.set([]);
      this.pendingBillingItemsError.set(
        extractApiErrorMessage(error, 'No fue posible cargar los productos PendingBilling.'),
      );
    } finally {
      this.loadingPendingBillingItems.set(false);
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

    const exactMatch = items.find(
      (item) =>
        item.lineNumber === context.lineNumber &&
        (item.productInternalCode?.trim().toUpperCase() ?? '') === normalizedCode &&
        item.description?.trim(),
    );

    if (exactMatch?.description?.trim()) {
      return exactMatch.description.trim();
    }

    const lineMatch = items.find(
      (item) => item.lineNumber === context.lineNumber && item.description?.trim(),
    );

    if (lineMatch?.description?.trim()) {
      return lineMatch.description.trim();
    }

    const codeMatch = items.find(
      (item) =>
        (item.productInternalCode?.trim().toUpperCase() ?? '') === normalizedCode &&
        item.description?.trim(),
    );

    return codeMatch?.description?.trim() || null;
  }

  protected resolveMissingProductSubmitLabel(
    missingProduct: MissingProductFiscalProfileContext,
  ): string {
    return missingProduct.existingProfileStatus === 'Inactive'
      ? 'Reactivar y reintentar'
      : 'Guardar y reintentar';
  }

  private buildMissingProductProfileSavedMessage(
    missingProduct: MissingProductFiscalProfileContext,
    request: UpsertProductFiscalProfileRequest,
  ): string {
    if (missingProduct.existingProfileStatus === 'Inactive') {
      return `Perfil fiscal del producto ${request.internalCode} reactivado y actualizado.`;
    }

    if (missingProduct.existingProductFiscalProfileId) {
      return `Perfil fiscal del producto ${request.internalCode} actualizado.`;
    }

    return `Perfil fiscal del producto ${request.internalCode} creado.`;
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
      const fetchedCancellation = await firstValueFrom(this.api.getCancellation(fiscalDocumentId));
      const currentCancellation = this.cancellation();
      if (shouldKeepCurrentCancelledCancellation(currentCancellation, fetchedCancellation)) {
        return;
      }

      this.cancellation.set(fetchedCancellation);
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
        link.download = buildFiscalDocumentFileName(
          {
            issuerRfc: this.fiscalDocument()?.issuerRfc ?? 'CFDI',
            series: this.fiscalDocument()?.series,
            folio: this.fiscalDocument()?.folio,
            receiverRfc: this.fiscalDocument()?.receiverRfc ?? 'CFDI',
            fallbackToken: this.stampEvidence()?.uuid ?? fiscalDocumentId,
          },
          'pdf',
        );
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
    return buildCancellationRequest(this.cancellationReasonCode, this.cancellationReplacementUuid);
  }

  private applyRemoteStampQueryResult(response: QueryRemoteFiscalStampResponse): void {
    this.lastOperationMessage.set(
      (response.xmlRecoveredLocally
        ? 'Se recuperó XML remoto y ya quedó persistido localmente.'
        : null) ||
        response.supportMessage ||
        response.providerMessage ||
        response.errorMessage ||
        (response.remoteExists
          ? 'El CFDI fue encontrado remotamente en el PAC.'
          : 'El PAC no devolvió evidencia remota para el UUID consultado.'),
    );
  }

  private buildStampAndEmailMessage(
    response: StampAndEmailFiscalDocumentResponse,
    retryRejected: boolean,
  ): string {
    if (!response.stamped) {
      return (
        response.providerMessage ||
        response.supportMessage ||
        response.errorMessage ||
        (retryRejected
          ? 'No fue posible reintentar el timbrado.'
          : 'No fue posible timbrar el documento fiscal.')
      );
    }

    switch (response.email.status) {
      case 'sent':
        return `CFDI timbrado correctamente. El correo fue enviado automáticamente a: ${response.email.recipients.join(', ')}.`;
      case 'missing':
        return 'CFDI timbrado correctamente. El receptor no tiene un email registrado.';
      case 'invalid':
        return 'CFDI timbrado correctamente. El email registrado del receptor no es válido.';
      case 'failed':
        return 'CFDI timbrado correctamente, pero no fue posible enviar el correo.';
      default:
        return retryRejected
          ? 'Reintento de timbrado ejecutado correctamente.'
          : 'Documento fiscal timbrado correctamente.';
    }
  }

  protected canCancelCurrentFiscalDocument(): boolean {
    return this.canLocalDiscardCurrentFiscalDocument() || this.canProviderCancelCurrentFiscalDocument();
  }

  protected canStampCurrentFiscalDocument(): boolean {
    const status = this.fiscalDocument()?.status;
    return this.currentBillingDocumentIsDraft()
      && !this.hasPersistedStampedUuid()
      && (status === 'ReadyForStamping' || status === 'StampingRejected');
  }

  protected canSyncCurrentFiscalDocument(): boolean {
    const status = this.fiscalDocument()?.status;
    return this.currentBillingDocumentIsDraft()
      && !this.hasPersistedStampedUuid()
      && (status === 'ReadyForStamping' || status === 'StampingRejected');
  }

  protected stampActionLabel(): string {
    return this.fiscalDocument()?.status === 'StampingRejected' ? 'Reintentar timbrado' : 'Timbrar';
  }

  protected canRefreshCurrentFiscalDocument(): boolean {
    return !!this.stampEvidence()?.uuid;
  }

  protected canQueryRemoteStamp(): boolean {
    return !!this.stampEvidence()?.uuid;
  }

  protected shouldShowEmailFallbackAction(): boolean {
    return (
      this.fiscalDocument()?.status === 'Stamped' &&
      this.pendingAutomaticEmailStatus() !== null &&
      !this.showEmailComposer()
    );
  }

  protected async reopenEmailComposerAfterStamp(): Promise<void> {
    await this.openEmailComposer(true);
  }

  protected emailComposerCloseLabel(): string {
    return this.pendingAutomaticEmailStatus() === null ? 'Cancelar' : 'Continuar sin enviar';
  }

  protected emailComposerContextMessage(): string {
    return this.pendingAutomaticEmailStatus() === null
      ? 'Se adjuntarán el XML timbrado y el PDF del CFDI.'
      : 'El CFDI ya quedó timbrado. Puedes capturar o corregir el correo para enviarlo ahora, o continuar sin enviar.';
  }

  protected billingItemRemovalValidationError(): string | null {
    return this.getBillingItemRemovalValidationError();
  }

  protected formatUtcToLocal(value: string | null | undefined): string {
    if (!value) {
      return 'Sin fecha';
    }

    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
  }

  protected getCancellationValidationError(): string | null {
    return getCancellationValidationError(
      this.cancellationReasonCode,
      this.cancellationReplacementUuid,
    );
  }

  protected canLocalDiscardCurrentFiscalDocument(): boolean {
    const status = this.fiscalDocument()?.status;
    return (
      this.currentBillingDocumentIsDraft() &&
      !this.hasPersistedStampedUuid() &&
      (status === 'Draft' || status === 'ReadyForStamping' || status === 'StampingRejected')
    );
  }

  private canProviderCancelCurrentFiscalDocument(): boolean {
    const status = this.fiscalDocument()?.status;
    return status === 'Stamped' || status === 'CancellationRejected';
  }

  private currentBillingDocumentIsDraft(): boolean {
    return this.billingDocumentContext()?.status === 'Draft';
  }

  private hasPersistedStampedUuid(): boolean {
    return !!this.stampEvidence()?.uuid;
  }

  private resetFiscalContextAfterBillingCancellation(): void {
    this.fiscalDocument.set(null);
    this.stampEvidence.set(null);
    this.cancellation.set(null);
    this.blockingCanceledOrders.set([]);
    this.pendingAutomaticEmailStatus.set(null);
    this.fiscalDocumentId.set(null);
    this.resetFiscalItemProfileDialogState();
    this.showCancelDialog.set(false);
    this.showCancelConfirmationDialog.set(false);
  }

  private resetFiscalItemProfileDialogState(): void {
    this.showFiscalItemProfileDialog.set(false);
    this.selectedFiscalDocumentItemForProfileEdit.set(null);
    this.fiscalItemProfileError.set(null);
  }

  private buildBillingCancellationMessage(
    releasedOrderLinkCount: number,
    releasedPendingAssignmentCount: number,
  ): string {
    const releasedOrdersMessage =
      releasedOrderLinkCount > 0
        ? `${releasedOrderLinkCount} vínculo(s) operativo(s) liberado(s)`
        : 'sin vínculos operativos pendientes por liberar';
    const releasedAssignmentsMessage =
      releasedPendingAssignmentCount > 0
        ? `${releasedPendingAssignmentCount} asignación(es) PendingBilling liberada(s)`
        : 'sin asignaciones PendingBilling activas';

    return `Documento de facturación cancelado. ${releasedOrdersMessage}; ${releasedAssignmentsMessage}.`;
  }

  private buildCancellationFeedbackMessage(
    response: import('../models/fiscal-documents.models').CancelFiscalDocumentResponse,
  ): string {
    if (response.isSuccess && response.operationType === 'LocalDiscard') {
      return (
        response.supportMessage ||
        'Snapshot fiscal descartado localmente. No se envió cancelación al SAT/PAC.'
      );
    }

    if (response.isSuccess) {
      return response.providerMessage || response.supportMessage || 'CFDI cancelado correctamente ante SAT/PAC.';
    }

    return (
      response.providerMessage ||
      response.supportMessage ||
      response.retryAdvice ||
      response.errorMessage ||
      `Resultado de la cancelación: ${getDisplayLabel(response.outcome)}`
    );
  }

  private getBillingItemRemovalValidationError(): string | null {
    if (!this.selectedBillingItemForRemoval()) {
      return 'Selecciona un producto válido para quitar.';
    }

    if (!this.billingItemRemovalReason().trim()) {
      return 'Selecciona el motivo base del producto removido.';
    }

    if (!this.billingItemRemovalDisposition().trim()) {
      return 'Selecciona el destino del producto removido.';
    }

    if (this.billingItemRemovalObservations().trim().length > 1000) {
      return 'Las observaciones no pueden exceder 1000 caracteres.';
    }

    return null;
  }

  private getPaymentPreparationValidationError(): string | null {
    const paymentMethod = this.normalizedPaymentMethodSat();
    if (
      !paymentMethod ||
      !this.paymentMethodCatalog().some((option) => option.code === paymentMethod)
    ) {
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

function extractBlockingCanceledOrders(
  error: unknown,
): LegacyOrderStampingBlockingOrderResponse[] {
  if (typeof error !== 'object' || !error || !('error' in error)) {
    return [];
  }

  const payload = (error as {
    error?: { blockingCanceledOrders?: unknown };
  }).error;
  if (!Array.isArray(payload?.blockingCanceledOrders)) {
    return [];
  }

  return payload.blockingCanceledOrders
    .filter(
      (item): item is LegacyOrderStampingBlockingOrderResponse =>
        typeof item === 'object' &&
        item !== null &&
        'salesOrderId' in item &&
        typeof item.salesOrderId === 'number' &&
        Number.isFinite(item.salesOrderId) &&
        item.salesOrderId > 0 &&
        'legacyOrderId' in item &&
        typeof item.legacyOrderId === 'string' &&
        item.legacyOrderId.trim().length > 0,
    )
    .map((item) => ({
      salesOrderId: item.salesOrderId,
      legacyOrderId: item.legacyOrderId.trim(),
    }));
}

function shouldOpenEmailComposerAfterStamp(
  status: StampAndEmailFiscalDocumentResponse['email']['status'],
): status is 'missing' | 'invalid' | 'failed' {
  return status === 'missing' || status === 'invalid' || status === 'failed';
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
  return creditDays && creditDays > 0 ? `Crédito a ${creditDays} días` : 'Crédito';
}

function normalizeSpecialFieldCode(value: string): string {
  return value
    .trim()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toUpperCase();
}

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
