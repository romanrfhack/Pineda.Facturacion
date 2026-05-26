import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { AccountsReceivableApiService } from '../../accounts-receivable/infrastructure/accounts-receivable-api.service';
import { PaymentComplementStampEvidenceDetailComponent } from '../components/payment-complement-stamp-evidence-detail.component';
import { PaymentContextModalComponent } from '../components/payment-context-modal.component';
import {
  buildPaymentComplementStampFeedbackMessage,
  shouldOpenPaymentComplementEmailComposerAfterStamp,
} from '../application/payment-complement-stamp-feedback';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import {
  RepBaseDocumentBulkRefreshResponse,
  InternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentItemResponse,
  PaymentComplementEmailDraftResponse,
  StampAndEmailPaymentComplementEmailResponse,
  PaymentComplementStampResponse,
  InternalRepBaseDocumentPaymentComplementResponse,
  InternalRepBaseDocumentPaymentHistoryResponse,
  PrepareInternalRepBaseDocumentPaymentComplementResponse,
  RepOperationalAlertResponse,
  RepBaseDocumentTimelineEntryResponse,
  RepOperationalSummaryCountsResponse,
  RegisterInternalRepBaseDocumentPaymentResponse,
  StampInternalRepBaseDocumentPaymentComplementResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-base-documents-page',
  imports: [FormsModule, DecimalPipe, PaymentComplementStampEvidenceDetailComponent, PaymentContextModalComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Complementos de pago</p>
        <h2>Bandeja operativa de CFDI internos para REP</h2>
        <p class="helper">La unidad operativa es el CFDI base. Desde esta vista ya se puede registrar el pago, preparar el REP y timbrarlo sin salir del contexto del CFDI interno.</p>
      </header>

      <section class="card">
        <form class="filters" (ngSubmit)="applyFilters()">
          <label><span>Desde</span><input [(ngModel)]="fromDate" name="fromDate" type="date" /></label>
          <label><span>Hasta</span><input [(ngModel)]="toDate" name="toDate" type="date" /></label>
          <label><span>RFC receptor</span><input [(ngModel)]="receiverRfc" name="receiverRfc" /></label>
          <label class="wide"><span>Búsqueda general</span><input [(ngModel)]="query" name="query" placeholder="UUID, RFC, receptor, serie o folio" /></label>
          <label>
            <span>Elegible</span>
            <select [(ngModel)]="eligibleFilter" name="eligibleFilter">
              <option value="">Todos</option>
              <option value="true">Sí</option>
              <option value="false">No</option>
            </select>
          </label>
          <label>
            <span>Bloqueado</span>
            <select [(ngModel)]="blockedFilter" name="blockedFilter">
              <option value="">Todos</option>
              <option value="true">Sí</option>
              <option value="false">No</option>
            </select>
          </label>
          <label>
            <span>Saldo pendiente</span>
            <select [(ngModel)]="outstandingFilter" name="outstandingFilter">
              <option value="">Todos</option>
              <option value="true">Con saldo</option>
              <option value="false">Sin saldo</option>
            </select>
          </label>
          <label>
            <span>REP emitidos</span>
            <select [(ngModel)]="repEmittedFilter" name="repEmittedFilter">
              <option value="">Todos</option>
              <option value="true">Con REP emitidos</option>
              <option value="false">Sin REP emitidos</option>
            </select>
          </label>
          <label>
            <span>Alerta</span>
            <select [(ngModel)]="alertCodeFilter" name="alertCodeFilter">
              <option value="">Todas</option>
              @for (option of alertOptions; track option) {
                <option [value]="option">{{ getDisplayLabel(option) }}</option>
              }
            </select>
          </label>
          <label>
            <span>Severidad</span>
            <select [(ngModel)]="severityFilter" name="severityFilter">
              <option value="">Todas</option>
              @for (option of severityOptions; track option) {
                <option [value]="option">{{ getDisplayLabel(option) }}</option>
              }
            </select>
          </label>
          <label>
            <span>Acción recomendada</span>
            <select [(ngModel)]="nextRecommendedActionFilter" name="nextRecommendedActionFilter">
              <option value="">Todas</option>
              @for (option of recommendedActionOptions; track option) {
                <option [value]="option">{{ getDisplayLabel(option) }}</option>
              }
            </select>
          </label>

          @if (filtersError()) {
            <p class="error wide">{{ filtersError() }}</p>
          }

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>

        <div class="quick-filters">
          <button type="button" class="secondary small quick-chip" [class.quick-chip-active]="!quickViewFilter" (click)="applyQuickView('')" [disabled]="loading()">
            Todos
          </button>
          @for (quickView of quickViewOptions; track quickView) {
            <button type="button" class="secondary small quick-chip" [class.quick-chip-active]="quickViewFilter === quickView" (click)="applyQuickView(quickView)" [disabled]="loading()">
              {{ getDisplayLabel(quickView) }} ({{ countForQuickView(quickView) }})
            </button>
          }
          @if (quickViewFilter) {
            <button type="button" class="secondary small quick-chip" (click)="applyQuickView('')" [disabled]="loading()">
              Volver a Todos
            </button>
          }
          @if (hasOperationalFilters()) {
            <button type="button" class="secondary small quick-chip" (click)="clearOperationalFilters()" [disabled]="loading()">
              Limpiar operativos
            </button>
          }
        </div>
      </section>

      <section class="card">
        @if (loading()) {
          <p class="helper">Cargando documentos base REP...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (!items().length) {
          <p class="helper">No se encontraron CFDI internos para la bandeja REP con los filtros actuales.</p>
        } @else {
          <div class="toolbar">
            <p class="helper">Mostrando {{ items().length }} de {{ totalCount() }} CFDI internos.</p>
            <label class="page-size">
              <span>Tamaño</span>
              <select [ngModel]="pageSize()" (ngModelChange)="changePageSize($event)" name="pageSize">
                <option [ngValue]="10">10</option>
                <option [ngValue]="25">25</option>
                <option [ngValue]="50">50</option>
              </select>
            </label>
          </div>

          <div class="bulk-toolbar">
            <label class="selection-toggle">
              <input type="checkbox" [checked]="allVisibleSelected()" (change)="toggleSelectAll($any($event.target).checked)" [disabled]="bulkRefreshing()" />
              <span>Seleccionar visibles</span>
            </label>
            <span class="helper">{{ selectedCount() }} seleccionados</span>
            <button type="button" [disabled]="bulkRefreshing() || !selectedCount()" (click)="refreshSelectedDocuments()">
              {{ bulkRefreshing() ? 'Refrescando...' : 'Refrescar seleccionados' }}
            </button>
            <button type="button" class="secondary" [disabled]="bulkRefreshing() || !items().length" (click)="refreshFilteredDocuments()">
              {{ bulkRefreshing() ? 'Refrescando...' : 'Refrescar filtrados' }}
            </button>
            @if (bulkRefreshResult()) {
              <button type="button" class="secondary small" [disabled]="bulkRefreshing()" (click)="clearBulkRefreshResult()">
                Limpiar resultado
              </button>
            }
          </div>

          @if (bulkRefreshError()) {
            <p class="error">{{ bulkRefreshError() }}</p>
          }

          @if (bulkRefreshResult(); as result) {
            <section class="nested-card bulk-result-card">
              <div class="section-header">
                <div>
                  <h4>Resultado del refresh masivo</h4>
                  <p class="helper">
                    Modo {{ getDisplayLabel(result.mode) }} · solicitados {{ result.totalRequested }} · procesados {{ result.totalAttempted }} · actualizados {{ result.refreshedCount }} · sin cambios {{ result.noChangesCount }} · bloqueados {{ result.blockedCount }} · fallidos {{ result.failedCount }}
                  </p>
                </div>
              </div>

              @if (result.items.length) {
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Documento</th>
                        <th>Resultado</th>
                        <th>REP</th>
                        <th>Estado externo</th>
                        <th>Mensaje</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of result.items; track item.sourceId) {
                        <tr>
                          <td>#{{ item.sourceId }}</td>
                          <td>{{ getDisplayLabel(item.outcome) }}</td>
                          <td>{{ item.paymentComplementDocumentId ? ('#' + item.paymentComplementDocumentId) : '—' }}{{ item.paymentComplementStatus ? (' · ' + getDisplayLabel(item.paymentComplementStatus)) : '' }}</td>
                          <td>{{ item.lastKnownExternalStatus || '—' }}</td>
                          <td>{{ item.message }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </section>
          }

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>
                    <input type="checkbox" [checked]="allVisibleSelected()" (change)="toggleSelectAll($any($event.target).checked)" [disabled]="bulkRefreshing()" />
                  </th>
                  <th>Emisión</th>
                  <th>Serie/Folio</th>
                  <th>UUID</th>
                  <th>RFC receptor</th>
                  <th>Receptor</th>
                  <th>Total</th>
                  <th>Pagado</th>
                  <th>Saldo</th>
                  <th>Fiscal</th>
                  <th>REP</th>
                  <th>Pagos</th>
                  <th>REP emitidos</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.fiscalDocumentId) {
                  <tr>
                    <td>
                      <input type="checkbox" [checked]="isSelected(item.fiscalDocumentId)" (change)="toggleSelection(item.fiscalDocumentId, $any($event.target).checked)" [disabled]="bulkRefreshing()" />
                    </td>
                    <td>{{ formatUtc(item.issuedAtUtc) }}</td>
                    <td>{{ buildSeriesFolio(item) }}</td>
                    <td>{{ item.uuid || '—' }}</td>
                    <td>{{ item.receiverRfc }}</td>
                    <td>{{ item.receiverLegalName }}</td>
                    <td>{{ item.total | number:'1.2-2' }}</td>
                    <td>{{ item.paidTotal | number:'1.2-2' }}</td>
                    <td>{{ item.outstandingBalance | number:'1.2-2' }}</td>
                    <td>{{ getDisplayLabel(item.fiscalStatus) }}</td>
                    <td>
                      <span class="status-pill" [class.status-eligible]="item.isEligible" [class.status-blocked]="item.isBlocked" [class.status-ineligible]="!item.isEligible && !item.isBlocked">
                        {{ getDisplayLabel(item.repOperationalStatus) }}
                      </span>
                      @if (getPrimarySeverity(item); as severity) {
                        <span class="severity-pill" [class.severity-warning]="severity === 'warning'" [class.severity-error]="severity === 'error'" [class.severity-critical]="severity === 'critical'" [class.severity-info]="severity === 'info'">
                          {{ getDisplayLabel(severity) }}
                        </span>
                      }
                      <small class="row-reason">{{ item.eligibility.primaryReasonMessage }}</small>
                      <small class="row-reason">Siguiente: {{ getRecommendedActionLabel(item.nextRecommendedAction) }}</small>
                      @if (getAlerts(item).length) {
                        <div class="alert-chip-list">
                          @for (alert of visibleAlerts(getAlerts(item)); track alert.code + '-' + alert.message) {
                            <span class="alert-chip" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-info]="alert.severity === 'info'">
                              {{ getDisplayLabel(alert.code) }}
                            </span>
                          }
                        </div>
                      }
                    </td>
                    <td>{{ item.registeredPaymentCount }}</td>
                    <td>{{ item.stampedPaymentComplementCount }}</td>
                    <td>
                      <div class="row-actions">
                        <button type="button" class="secondary small" (click)="openDetailModal(item)">Ver contexto</button>
                        @if (item.isEligible && item.outstandingBalance > 0) {
                          <button type="button" class="small" (click)="openDetailModal(item, true)">Registrar pago</button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button type="button" class="secondary" (click)="goToPage(page() - 1)" [disabled]="page() <= 1 || loading()">Anterior</button>
            <span>Página {{ page() }} de {{ totalPages() || 1 }}</span>
            <button type="button" class="secondary" (click)="goToPage(page() + 1)" [disabled]="page() >= totalPages() || loading()">Siguiente</button>
          </div>
        }
      </section>

      <app-payment-context-modal
        [open]="showDetailModal()"
        [detail]="selectedDetail()"
        [loading]="loadingDetail()"
        [error]="detailError()"
        [showRegisterPaymentForm]="showRegisterPaymentForm()"
        [submittingPayment]="submittingPayment()"
        [ensuringAccountsReceivable]="ensuringAccountsReceivable()"
        [paymentError]="paymentError()"
        [repActionError]="repActionError()"
        [preparingComplement]="preparingComplement()"
        [stampingComplement]="stampingComplement()"
        [refreshingComplement]="refreshingComplement()"
        [cancellingComplement]="cancellingComplement()"
        [repUtilityActionKey]="repUtilityActionKey()"
        [canSendPaymentComplementEmail]="permissionService.canStampFiscal()"
        [groupedPaymentIds]="groupedPaymentIds()"
        [groupedPrepareToken]="GROUPED_PREPARE_TOKEN"
        [(paymentDate)]="paymentDate"
        [(paymentFormSat)]="paymentFormSat"
        [(paymentAmount)]="paymentAmount"
        [(paymentReference)]="paymentReference"
        [(paymentNotes)]="paymentNotes"
        (closeRequested)="closeDetailModal()"
        (registerPaymentRequested)="openRegisterPaymentForm()"
        (registerPaymentCancelled)="cancelRegisterPaymentForm()"
        (registerPaymentSubmitted)="submitRegisterPayment()"
        (ensureAccountsReceivableRequested)="ensureAccountsReceivable($event)"
        (groupedPaymentToggled)="toggleGroupedPaymentSelection($event.accountsReceivablePaymentId, $event.checked)"
        (groupedPaymentSelectionCleared)="clearGroupedPaymentSelection()"
        (prepareSelectedPaymentComplementRequested)="prepareSelectedPaymentComplement()"
        (preparePaymentComplementRequested)="preparePaymentComplement($event)"
        (stampPaymentComplementRequested)="stampPaymentComplement($event)"
        (viewPaymentComplementStampRequested)="openPaymentComplementStampDetail($event)"
        (downloadPaymentComplementXmlRequested)="downloadPaymentComplementXml($event)"
        (downloadPaymentComplementPdfRequested)="downloadPaymentComplementPdf($event)"
        (emailPaymentComplementRequested)="openPaymentComplementEmailComposer($event)"
        (refreshPaymentComplementRequested)="refreshPaymentComplement($event)"
        (cancelPaymentComplementRequested)="cancelPaymentComplement($event)"
      />

      @if (showPaymentComplementStampDetail()) {
        <section class="overlay-backdrop" (click)="closePaymentComplementStampDetail()">
          <section class="overlay-card detail-overlay" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
            <div class="overlay-header">
              <div>
                <p class="eyebrow">Complementos de pago</p>
                <h3>Detalle de timbrado REP</h3>
              </div>
              <button type="button" class="secondary" (click)="closePaymentComplementStampDetail()">Cerrar</button>
            </div>

            @if (loadingPaymentComplementStampDetail()) {
              <p class="helper">Cargando evidencia de timbrado...</p>
            } @else if (paymentComplementStampDetailError()) {
              <p class="error">{{ paymentComplementStampDetailError() }}</p>
            } @else if (paymentComplementStampDetail(); as stamp) {
              @if (selectedPaymentComplementForStampDetail(); as rep) {
                <section class="card nested-card stamp-summary-card">
                  <div class="summary-grid">
                    <div><strong>REP</strong><span>#{{ rep.paymentComplementId }}</span></div>
                    <div><strong>UUID</strong><span>{{ stamp.uuid || rep.uuid || '—' }}</span></div>
                    <div><strong>Proveedor</strong><span>{{ stamp.providerName || rep.providerName || '—' }}</span></div>
                    <div><strong>Estado</strong><span>{{ getDisplayLabel(stamp.status || rep.status) }}</span></div>
                    <div><strong>Timbrado</strong><span>{{ formatOptionalUtc(stamp.stampedAtUtc || rep.stampedAtUtc) }}</span></div>
                    <div><strong>XML</strong><span>{{ rep.xmlAvailable ? 'Disponible' : 'No disponible' }}</span></div>
                    <div><strong>PDF</strong><span>{{ rep.pdfAvailable ? 'Disponible' : (rep.canGeneratePdf ? 'Generable' : 'No disponible') }}</span></div>
                    <div><strong>Cancelación</strong><span>{{ formatOptionalUtc(rep.cancelledAtUtc) }}</span></div>
                  </div>
                </section>
              }

              <app-payment-complement-stamp-evidence-detail [stamp]="stamp" />
            }
          </section>
        </section>
      }

      @if (showPaymentComplementEmailComposer()) {
        <section class="overlay-backdrop" (click)="closePaymentComplementEmailComposer()">
          <section class="overlay-card" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
            <div class="overlay-header">
              <div>
                <p class="eyebrow">Complementos de pago</p>
                <h3>Enviar REP por correo</h3>
              </div>
              <button type="button" class="secondary" (click)="closePaymentComplementEmailComposer()" [disabled]="sendingPaymentComplementEmail()">
                Cerrar
              </button>
            </div>

            <p class="helper">El backend adjuntará el XML y PDF del complemento de pago disponible para este REP.</p>

            @if (paymentComplementEmailAttachments().length) {
              <section class="attachment-section">
                <strong>Adjuntos esperados</strong>
                <div class="attachment-chip-list">
                  @for (fileName of paymentComplementEmailAttachments(); track fileName) {
                    <span class="attachment-chip">{{ fileName }}</span>
                  }
                </div>
              </section>
            }

            <form class="email-form" (ngSubmit)="sendPaymentComplementEmail()">
              <label class="wide">
                <span>Correo(s) destino</span>
                <input [(ngModel)]="paymentComplementEmailRecipientsInput" name="paymentComplementEmailRecipientsInput" />
              </label>
              <label class="wide">
                <span>Asunto</span>
                <input [(ngModel)]="paymentComplementEmailSubject" name="paymentComplementEmailSubject" />
              </label>
              <label class="wide">
                <span>Mensaje</span>
                <textarea [(ngModel)]="paymentComplementEmailBody" name="paymentComplementEmailBody" rows="6"></textarea>
              </label>

              @if (paymentComplementEmailRecipientsError()) {
                <p class="error wide">{{ paymentComplementEmailRecipientsError() }}</p>
              }

              @if (paymentComplementEmailDraftError()) {
                <p class="error wide">{{ paymentComplementEmailDraftError() }}</p>
              }

              <div class="actions wide">
                <button type="submit" [disabled]="sendingPaymentComplementEmail() || !hasValidPaymentComplementEmailRecipients()">
                  {{ sendingPaymentComplementEmail() ? 'Enviando...' : 'Enviar complemento' }}
                </button>
                <button
                  type="button"
                  class="secondary"
                  (click)="closePaymentComplementEmailComposer()"
                  [disabled]="sendingPaymentComplementEmail()"
                >
                  Cancelar
                </button>
              </div>
            </form>
          </section>
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card, .summary-card, .nested-card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .filters { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:1rem; align-items:end; }
    .wide { grid-column:1 / -1; }
    label { display:grid; gap:0.35rem; }
    input, select, textarea, button { font:inherit; }
    input, select, textarea { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.45rem 0.7rem; font-size:0.88rem; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .actions, .toolbar, .pagination, .modal-actions, .section-header, .row-actions, .quick-filters, .bulk-toolbar { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    .toolbar, .pagination { justify-content:space-between; }
    .section-header { justify-content:space-between; margin-bottom:0.75rem; }
    .bulk-toolbar { margin-bottom:0.75rem; }
    .selection-toggle { display:inline-flex; align-items:center; gap:0.5rem; }
    .bulk-result-card { margin-bottom:1rem; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; vertical-align:top; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
    .status-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.65rem; font-size:0.82rem; font-weight:600; }
    .status-eligible { background:#e5f6eb; color:#1b6b3a; }
    .status-blocked { background:#fdeaea; color:#8a1f1f; }
    .status-ineligible { background:#f4efe4; color:#6f5b22; }
    .eligibility-box { display:grid; gap:0.5rem; margin-bottom:0.75rem; }
    .eligibility-reason { margin:0; font-weight:600; color:#182533; }
    .signal-list { list-style:none; padding:0; margin:0; display:grid; gap:0.65rem; }
    .signal-item { display:grid; grid-template-columns:auto 1fr; gap:0.65rem; align-items:flex-start; padding:0.65rem 0.75rem; border:1px solid #ece5d7; border-radius:0.85rem; background:#fbf8f1; }
    .signal-item p { margin:0.2rem 0 0; color:#5f6b76; }
    .signal-pill { display:inline-flex; align-items:center; justify-content:center; min-width:5.5rem; border-radius:999px; padding:0.25rem 0.55rem; font-size:0.78rem; font-weight:700; }
    .signal-positive { background:#e5f6eb; color:#1b6b3a; }
    .signal-warning { background:#fff1d6; color:#8a5a00; }
    .signal-blocking { background:#fdeaea; color:#8a1f1f; }
    .severity-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.2rem 0.55rem; margin-left:0.35rem; font-size:0.74rem; font-weight:700; }
    .severity-info { background:#eef1f4; color:#425466; }
    .severity-warning { background:#fff3dd; color:#8a5a00; }
    .severity-error { background:#fde8e8; color:#8a1f1f; }
    .severity-critical { background:#f8d7d7; color:#6f1111; }
    .alert-chip-list { display:flex; flex-wrap:wrap; gap:0.35rem; margin-top:0.45rem; }
    .alert-chip, .alert-item { border-radius:0.8rem; }
    .alert-chip { display:inline-flex; align-items:center; padding:0.2rem 0.55rem; font-size:0.75rem; font-weight:700; }
    .alert-list { list-style:none; padding:0; margin:0; display:grid; gap:0.65rem; }
    .alert-item { padding:0.7rem 0.8rem; border:1px solid #ece5d7; }
    .alert-item p { margin:0.2rem 0 0; color:#425466; }
    .alert-warning { background:#fff3dd; color:#8a5a00; }
    .alert-error { background:#fde8e8; color:#8a1f1f; }
    .alert-critical { background:#fdeaea; color:#8a1f1f; }
    .alert-info { background:#eef1f4; color:#425466; }
    .quick-chip { border:1px solid #d8d1c2; }
    .quick-chip.quick-chip-active { outline:2px solid #182533; }
    .row-reason { display:block; margin-top:0.35rem; color:#5f6b76; }
    .timeline-list { display:grid; gap:0.85rem; }
    .timeline-item { border:1px solid #ece5d7; border-left:4px solid #8a6a32; border-radius:0.9rem; padding:0.85rem 1rem; display:grid; gap:0.45rem; background:#fcfaf4; }
    .timeline-item header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .timeline-heading { display:grid; gap:0.45rem; }
    .timeline-badges, .timeline-meta-list { display:flex; gap:0.45rem; flex-wrap:wrap; }
    .timeline-chip { display:inline-flex; align-items:center; border-radius:999px; padding:0.2rem 0.55rem; background:#eef1f4; color:#425466; font-size:0.75rem; font-weight:700; }
    .timeline-item p, .timeline-item small { margin:0; color:#425466; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.42); display:grid; place-items:center; padding:1rem; z-index:50; }
    .modal-card { width:min(1180px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .detail-modal { align-content:start; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .payment-form { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:1rem; align-items:end; }
    textarea { resize:vertical; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(300px, 1fr)); gap:1rem; }
    .overlay-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.52); display:grid; place-items:center; padding:1rem; z-index:60; }
    .overlay-card { width:min(980px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .detail-overlay { width:min(1080px, 100%); }
    .overlay-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .stamp-summary-card { background:#fcfaf4; }
    .summary-grid div { display:grid; gap:0.2rem; }
    .summary-grid strong { color:#5f6b76; font-size:0.82rem; }
    .summary-grid span { color:#182533; font-weight:600; overflow-wrap:anywhere; }
    .attachment-section { display:grid; gap:0.65rem; }
    .attachment-chip-list { display:flex; flex-wrap:wrap; gap:0.55rem; }
    .attachment-chip { display:inline-flex; align-items:center; border:1px solid #d8d1c2; border-radius:999px; background:#f6f1e7; color:#5a4d35; padding:0.3rem 0.75rem; }
    .email-form { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:start; }
    dl { display:grid; gap:0.5rem; margin:0; }
    dl div { display:grid; gap:0.15rem; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0; font-weight:600; color:#182533; }
    @media (max-width: 720px) {
      .toolbar, .pagination, .modal-header, .overlay-header { flex-direction:column; align-items:stretch; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementBaseDocumentsPageComponent {
  protected readonly GROUPED_PREPARE_TOKEN = -1;
  private readonly api = inject(PaymentComplementsApiService);
  private readonly accountsReceivableApi = inject(AccountsReceivableApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);
  protected readonly getDisplayLabel = getDisplayLabel;

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected query = '';
  protected eligibleFilter = '';
  protected blockedFilter = '';
  protected outstandingFilter = '';
  protected repEmittedFilter = '';
  protected alertCodeFilter = '';
  protected severityFilter = '';
  protected nextRecommendedActionFilter = '';
  protected quickViewFilter = '';
  protected readonly alertOptions = REP_OPERATIONAL_ALERT_OPTIONS;
  protected readonly severityOptions = REP_OPERATIONAL_SEVERITY_OPTIONS;
  protected readonly recommendedActionOptions = REP_RECOMMENDED_ACTION_OPTIONS;
  protected readonly quickViewOptions = REP_QUICK_VIEW_OPTIONS;
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly items = signal<InternalRepBaseDocumentItemResponse[]>([]);
  protected readonly selectedIds = signal<number[]>([]);
  protected readonly summaryCounts = signal<RepOperationalSummaryCountsResponse>(createEmptySummaryCounts());
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filtersError = signal<string | null>(null);
  protected readonly bulkRefreshing = signal(false);
  protected readonly bulkRefreshError = signal<string | null>(null);
  protected readonly bulkRefreshResult = signal<RepBaseDocumentBulkRefreshResponse | null>(null);
  protected readonly showDetailModal = signal(false);
  protected readonly selectedDetail = signal<InternalRepBaseDocumentDetailResponse | null>(null);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly showRegisterPaymentForm = signal(false);
  protected readonly submittingPayment = signal(false);
  protected readonly ensuringAccountsReceivable = signal(false);
  protected readonly paymentError = signal<string | null>(null);
  protected readonly preparingComplement = signal<number | null>(null);
  protected readonly stampingComplement = signal<number | null>(null);
  protected readonly refreshingComplement = signal<number | null>(null);
  protected readonly cancellingComplement = signal<number | null>(null);
  protected readonly repUtilityActionKey = signal<string | null>(null);
  protected readonly repActionError = signal<string | null>(null);
  protected readonly showPaymentComplementStampDetail = signal(false);
  protected readonly loadingPaymentComplementStampDetail = signal(false);
  protected readonly paymentComplementStampDetail = signal<PaymentComplementStampResponse | null>(null);
  protected readonly paymentComplementStampDetailError = signal<string | null>(null);
  protected readonly selectedPaymentComplementForStampDetail = signal<InternalRepBaseDocumentPaymentComplementResponse | null>(null);
  protected readonly showPaymentComplementEmailComposer = signal(false);
  protected readonly sendingPaymentComplementEmail = signal(false);
  protected readonly paymentComplementEmailDraft = signal<PaymentComplementEmailDraftResponse | null>(null);
  protected readonly paymentComplementEmailDraftError = signal<string | null>(null);
  protected readonly paymentComplementEmailRecipientsError = signal<string | null>(null);
  protected readonly selectedPaymentComplementForEmail = signal<InternalRepBaseDocumentPaymentComplementResponse | null>(null);
  protected readonly groupedPaymentIds = signal<number[]>([]);
  protected paymentDate = todayInputValue();
  protected paymentFormSat = '03';
  protected paymentAmount: number | null = null;
  protected paymentReference = '';
  protected paymentNotes = '';
  protected paymentComplementEmailRecipientsInput = '';
  protected paymentComplementEmailSubject = '';
  protected paymentComplementEmailBody = '';

  constructor() {
    void this.load();
  }

  protected async applyFilters(): Promise<void> {
    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.filtersError.set('La fecha inicial no puede ser mayor a la fecha final.');
      return;
    }

    this.filtersError.set(null);
    this.page.set(1);
    await this.load();
  }

  protected async clearFilters(): Promise<void> {
    this.fromDate = '';
    this.toDate = '';
    this.receiverRfc = '';
    this.query = '';
    this.eligibleFilter = '';
    this.blockedFilter = '';
    this.outstandingFilter = '';
    this.repEmittedFilter = '';
    this.alertCodeFilter = '';
    this.severityFilter = '';
    this.nextRecommendedActionFilter = '';
    this.quickViewFilter = '';
    this.filtersError.set(null);
    this.page.set(1);
    this.pageSize.set(25);
    await this.load();
  }

  protected async goToPage(page: number): Promise<void> {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }

    this.page.set(page);
    await this.load();
  }

  protected async changePageSize(value: number): Promise<void> {
    this.pageSize.set(Number(value) || 25);
    this.page.set(1);
    await this.load();
  }

  protected async openDetailModal(item: InternalRepBaseDocumentItemResponse, openRegisterPayment = false): Promise<void> {
    this.showDetailModal.set(true);
    await this.loadDetail(item.fiscalDocumentId, openRegisterPayment);
  }

  protected closeDetailModal(): void {
    this.showDetailModal.set(false);
    this.selectedDetail.set(null);
    this.loadingDetail.set(false);
    this.detailError.set(null);
    this.repActionError.set(null);
    this.closePaymentComplementStampDetail();
    this.forceClosePaymentComplementEmailComposer();
    this.repUtilityActionKey.set(null);
    this.clearGroupedPaymentSelection();
    this.cancelRegisterPaymentForm();
  }

  protected openRegisterPaymentForm(): void {
    const detail = this.selectedDetail();
    if (!detail?.summary.isEligible || detail.summary.outstandingBalance <= 0) {
      this.paymentError.set(detail?.summary.eligibility.primaryReasonMessage ?? 'El CFDI no puede recibir pagos desde esta vista.');
      this.showRegisterPaymentForm.set(false);
      return;
    }

    this.paymentDate = todayInputValue();
    this.paymentFormSat = '03';
    this.paymentAmount = Number(detail.summary.outstandingBalance.toFixed(2));
    this.paymentReference = '';
    this.paymentNotes = '';
    this.paymentError.set(null);
    this.repActionError.set(null);
    this.showRegisterPaymentForm.set(true);
  }

  protected cancelRegisterPaymentForm(): void {
    this.showRegisterPaymentForm.set(false);
    this.submittingPayment.set(false);
    this.paymentError.set(null);
    this.paymentDate = todayInputValue();
    this.paymentFormSat = '03';
    this.paymentAmount = null;
    this.paymentReference = '';
    this.paymentNotes = '';
  }

  protected canEnsureAccountsReceivable(detail: InternalRepBaseDocumentDetailResponse | null): boolean {
    return detail?.summary.eligibility.primaryReasonCode === 'AccountsReceivableMissing';
  }

  protected buildRegisterPaymentBlockedMessage(detail: InternalRepBaseDocumentDetailResponse): string {
    if (this.canEnsureAccountsReceivable(detail)) {
      return 'El CFDI no puede recibir pagos desde esta vista porque todavía no tiene una cuenta por cobrar operativa. Habilítala para controlar saldo, parcialidades y REP.';
    }

    return `El CFDI no puede recibir pagos desde esta vista: ${detail.summary.eligibility.primaryReasonMessage}`;
  }

  protected async ensureAccountsReceivable(fiscalDocumentId: number): Promise<void> {
    this.ensuringAccountsReceivable.set(true);
    this.paymentError.set(null);
    this.repActionError.set(null);

    try {
      const result = await firstValueFrom(this.accountsReceivableApi.ensureInvoiceForFiscalDocument(fiscalDocumentId));
      const invoiceId = result.accountsReceivableInvoice?.id;
      const message = invoiceId
        ? `Cuenta por cobrar ${invoiceId} habilitada para el CFDI.`
        : (result.errorMessage || 'La cuenta por cobrar ya quedó asegurada.');
      this.feedbackService.show(result.outcome === 'Skipped' ? 'warning' : 'success', message);
      await this.loadDetail(fiscalDocumentId);
      await this.load();
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible habilitar la cuenta por cobrar operativa para este CFDI.');
      this.repActionError.set(message);
      this.paymentError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.ensuringAccountsReceivable.set(false);
    }
  }

  protected async submitRegisterPayment(): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.paymentError.set(null);
    this.submittingPayment.set(true);

    try {
      const result = await firstValueFrom(this.api.registerInternalBaseDocumentPayment(detail.summary.fiscalDocumentId, {
        paymentDate: this.paymentDate,
        paymentFormSat: this.paymentFormSat,
        amount: Number(this.paymentAmount),
        reference: this.paymentReference || null,
        notes: this.paymentNotes || null
      }));

      await this.handleSuccessfulPaymentRegistration(detail.summary.fiscalDocumentId, result);
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible registrar y aplicar el pago sobre el CFDI.');
      this.paymentError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.submittingPayment.set(false);
    }
  }

  protected canPrepareComplement(payment: InternalRepBaseDocumentPaymentHistoryResponse): boolean {
    return !payment.paymentComplementId && payment.amountAppliedToDocument > 0;
  }

  protected canStampComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.status === 'ReadyForStamping' || complement.status === 'StampingRejected';
  }

  protected canRefreshComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return ['Stamped', 'CancellationRequested', 'CancellationRejected', 'Cancelled'].includes(complement.status);
  }

  protected canCancelComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.status === 'Stamped' || complement.status === 'CancellationRejected';
  }

  protected async preparePaymentComplement(payment: InternalRepBaseDocumentPaymentHistoryResponse): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.repActionError.set(null);
    this.preparingComplement.set(payment.accountsReceivablePaymentId);

    try {
      const additionalPaymentIds = this.getSelectedGroupedPaymentIdsExcluding(payment.accountsReceivablePaymentId);
      const result = await firstValueFrom(this.api.prepareInternalBaseDocumentPaymentComplement(detail.summary.fiscalDocumentId, {
        accountsReceivablePaymentId: payment.accountsReceivablePaymentId,
        ...(additionalPaymentIds.length ? { additionalPaymentIds } : {})
      }));
      await this.handleSuccessfulPrepare(detail.summary.fiscalDocumentId, result);
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible preparar el REP desde el CFDI base.');
      this.repActionError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.preparingComplement.set(null);
    }
  }

  protected async prepareSelectedPaymentComplement(): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    const selectedPayments = detail.paymentHistory.filter((payment) =>
      this.groupedPaymentIds().includes(payment.accountsReceivablePaymentId) && this.canPrepareComplement(payment));
    if (selectedPayments.length < 2) {
      this.repActionError.set('Selecciona al menos dos pagos elegibles para preparar un REP agrupado.');
      return;
    }

    const [anchorPayment, ...additionalPayments] = selectedPayments;
    this.repActionError.set(null);
    this.preparingComplement.set(this.GROUPED_PREPARE_TOKEN);

    try {
      const result = await firstValueFrom(this.api.prepareInternalBaseDocumentPaymentComplement(detail.summary.fiscalDocumentId, {
        accountsReceivablePaymentId: anchorPayment.accountsReceivablePaymentId,
        additionalPaymentIds: additionalPayments.map((payment) => payment.accountsReceivablePaymentId)
      }));
      await this.handleSuccessfulPrepare(detail.summary.fiscalDocumentId, result);
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible preparar el REP agrupado desde el CFDI base.');
      this.repActionError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.preparingComplement.set(null);
    }
  }

  protected async stampPaymentComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.repActionError.set(null);
    this.stampingComplement.set(complement.paymentComplementId);

    try {
      const result = await firstValueFrom(this.api.stampInternalBaseDocumentPaymentComplement(detail.summary.fiscalDocumentId, {
        paymentComplementDocumentId: complement.paymentComplementId,
        retryRejected: complement.status === 'StampingRejected'
      }));
      await this.handleSuccessfulStamp(detail.summary.fiscalDocumentId, result);
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible timbrar el REP desde el CFDI base.');
      this.repActionError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.stampingComplement.set(null);
    }
  }

  protected async refreshPaymentComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.repActionError.set(null);
    this.refreshingComplement.set(complement.paymentComplementId);

    try {
      const result = await firstValueFrom(this.api.refreshInternalBaseDocumentPaymentComplementStatus(detail.summary.fiscalDocumentId, {
        paymentComplementDocumentId: complement.paymentComplementId
      }));

      const suffix = result.lastKnownExternalStatus ? ` Estado externo: ${result.lastKnownExternalStatus}.` : '.';
      this.feedbackService.show('success', `REP ${result.paymentComplementDocumentId ?? complement.paymentComplementId} actualizado.${suffix}`);
      await this.loadDetail(detail.summary.fiscalDocumentId);
      await this.load();
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible refrescar el estatus del REP desde el CFDI base.');
      this.repActionError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.refreshingComplement.set(null);
    }
  }

  protected async cancelPaymentComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.repActionError.set(null);
    this.cancellingComplement.set(complement.paymentComplementId);

    try {
      const result = await firstValueFrom(this.api.cancelInternalBaseDocumentPaymentComplement(detail.summary.fiscalDocumentId, {
        paymentComplementDocumentId: complement.paymentComplementId,
        cancellationReasonCode: '02',
        replacementUuid: null
      }));

      const suffix = result.cancellationStatus ? ` Estatus: ${getDisplayLabel(result.cancellationStatus)}.` : '.';
      this.feedbackService.show('success', `REP ${result.paymentComplementDocumentId ?? complement.paymentComplementId} cancelado.${suffix}`);
      await this.loadDetail(detail.summary.fiscalDocumentId);
      await this.load();
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible cancelar el REP desde el CFDI base.');
      this.repActionError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.cancellingComplement.set(null);
    }
  }

  protected async openPaymentComplementStampDetail(
    complement: InternalRepBaseDocumentPaymentComplementResponse
  ): Promise<void> {
    this.selectedPaymentComplementForStampDetail.set(complement);
    this.showPaymentComplementStampDetail.set(true);
    this.loadingPaymentComplementStampDetail.set(true);
    this.paymentComplementStampDetail.set(null);
    this.paymentComplementStampDetailError.set(null);
    this.repUtilityActionKey.set(`detail:${complement.paymentComplementId}`);

    try {
      const stamp = await firstValueFrom(this.api.getPaymentComplementStamp(complement.paymentComplementId));
      this.paymentComplementStampDetail.set(stamp);
    } catch (error) {
      const message = resolvePaymentComplementStampDetailError(error);
      this.paymentComplementStampDetailError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.loadingPaymentComplementStampDetail.set(false);
      this.repUtilityActionKey.set(null);
    }
  }

  protected closePaymentComplementStampDetail(): void {
    this.showPaymentComplementStampDetail.set(false);
    this.loadingPaymentComplementStampDetail.set(false);
    this.paymentComplementStampDetail.set(null);
    this.paymentComplementStampDetailError.set(null);
    this.selectedPaymentComplementForStampDetail.set(null);
  }

  protected async downloadPaymentComplementXml(
    complement: InternalRepBaseDocumentPaymentComplementResponse
  ): Promise<void> {
    this.repUtilityActionKey.set(`xml:${complement.paymentComplementId}`);

    try {
      const response = await firstValueFrom(this.api.downloadPaymentComplementXml(complement.paymentComplementId));
      const blob = response.body;
      if (!blob) {
        throw new Error('MissingBlob');
      }

      triggerBlobDownload(
        blob,
        getFileNameFromContentDisposition(response.headers.get('content-disposition')) ??
          buildRepFileName(complement, 'xml'),
      );
    } catch (error) {
      this.feedbackService.show('error', resolvePaymentComplementDownloadError(error, 'xml'));
    } finally {
      this.repUtilityActionKey.set(null);
    }
  }

  protected async downloadPaymentComplementPdf(
    complement: InternalRepBaseDocumentPaymentComplementResponse
  ): Promise<void> {
    this.repUtilityActionKey.set(`pdf:${complement.paymentComplementId}`);

    try {
      const response = await firstValueFrom(this.api.downloadPaymentComplementPdf(complement.paymentComplementId));
      const blob = response.body;
      if (!blob) {
        throw new Error('MissingBlob');
      }

      triggerBlobDownload(
        blob,
        getFileNameFromContentDisposition(response.headers.get('content-disposition')) ??
          buildRepFileName(complement, 'pdf'),
      );
    } catch (error) {
      this.feedbackService.show('error', resolvePaymentComplementDownloadError(error, 'pdf'));
    } finally {
      this.repUtilityActionKey.set(null);
    }
  }

  protected async openPaymentComplementEmailComposer(
    complement: InternalRepBaseDocumentPaymentComplementResponse,
    automaticEmailResult?: StampAndEmailPaymentComplementEmailResponse | null
  ): Promise<void> {
    this.selectedPaymentComplementForEmail.set(complement);
    this.paymentComplementEmailDraft.set(null);
    this.paymentComplementEmailDraftError.set(null);
    this.paymentComplementEmailRecipientsError.set(null);
    this.paymentComplementEmailRecipientsInput = '';
    this.paymentComplementEmailSubject = '';
    this.paymentComplementEmailBody = '';
    this.repUtilityActionKey.set(`email:${complement.paymentComplementId}`);

    try {
      const draft = await firstValueFrom(this.api.getPaymentComplementEmailDraft(complement.paymentComplementId));
      this.paymentComplementEmailDraft.set(draft);
      this.paymentComplementEmailRecipientsInput = draft.recipients.join(', ');
      if (!draft.recipients.length) {
        const fallbackRecipients = automaticEmailResult?.status === 'invalid'
          ? automaticEmailResult.invalidRecipients
          : automaticEmailResult?.recipients ?? [];
        this.paymentComplementEmailRecipientsInput = fallbackRecipients.join(', ');
      }
      this.paymentComplementEmailSubject = draft.subject ?? '';
      this.paymentComplementEmailBody = draft.body ?? '';
      this.showPaymentComplementEmailComposer.set(true);
    } catch (error) {
      const message = resolvePaymentComplementEmailDraftError(error);
      this.paymentComplementEmailDraftError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.repUtilityActionKey.set(null);
    }
  }

  protected closePaymentComplementEmailComposer(): void {
    if (this.sendingPaymentComplementEmail()) {
      return;
    }

    this.forceClosePaymentComplementEmailComposer();
  }

  protected hasValidPaymentComplementEmailRecipients(): boolean {
    const recipients = parseRecipients(this.paymentComplementEmailRecipientsInput);
    return recipients.length > 0 && recipients.every(isValidEmail);
  }

  protected paymentComplementEmailAttachments(): string[] {
    const draftAttachments = this.paymentComplementEmailDraft()?.attachments ?? [];
    if (draftAttachments.length) {
      return draftAttachments.map((attachment) => attachment.fileName);
    }

    const complement = this.selectedPaymentComplementForEmail();
    if (!complement) {
      return [];
    }

    return [buildRepFileName(complement, 'xml'), buildRepFileName(complement, 'pdf')];
  }

  protected async sendPaymentComplementEmail(): Promise<void> {
    const complement = this.selectedPaymentComplementForEmail();
    if (!complement || this.sendingPaymentComplementEmail()) {
      return;
    }

    const recipients = parseRecipients(this.paymentComplementEmailRecipientsInput);
    if (!recipients.length) {
      this.paymentComplementEmailRecipientsError.set('Captura al menos un destinatario.');
      return;
    }

    if (!recipients.every(isValidEmail)) {
      this.paymentComplementEmailRecipientsError.set('Captura únicamente correos válidos para continuar.');
      return;
    }

    if (!this.permissionService.canStampFiscal()) {
      this.paymentComplementEmailDraftError.set('No tienes permisos para enviar este complemento por correo.');
      return;
    }

    this.sendingPaymentComplementEmail.set(true);
    this.paymentComplementEmailRecipientsError.set(null);
    this.paymentComplementEmailDraftError.set(null);

    try {
      await firstValueFrom(this.api.sendPaymentComplementEmail(complement.paymentComplementId, {
        recipients,
        subject: normalizeOptionalText(this.paymentComplementEmailSubject),
        body: normalizeOptionalText(this.paymentComplementEmailBody),
      }));
      this.feedbackService.show('success', 'Complemento de pago enviado por correo.');
      this.forceClosePaymentComplementEmailComposer();
    } catch (error) {
      this.paymentComplementEmailDraftError.set(resolvePaymentComplementEmailSendError(error));
    } finally {
      this.sendingPaymentComplementEmail.set(false);
    }
  }

  protected buildSeriesFolio(item: InternalRepBaseDocumentItemResponse): string {
    const series = item.series?.trim();
    const folio = item.folio?.trim();

    if (series && folio) {
      return `${series}-${folio}`;
    }

    return series || folio || '—';
  }

  protected formatUtc(value: string): string {
    return new Date(value).toLocaleString('es-MX', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'UTC'
    });
  }

  protected formatOptionalUtc(value?: string | null): string {
    return value ? this.formatUtc(value) : '—';
  }

  protected visibleAlerts(alerts: RepOperationalAlertResponse[]): RepOperationalAlertResponse[] {
    return alerts.slice(0, 3);
  }

  protected getPrimarySeverity(source: { alerts?: RepOperationalAlertResponse[] | null }): string | null {
    return resolvePrimarySeverity(source.alerts ?? []);
  }

  protected getAlerts(source: { alerts?: RepOperationalAlertResponse[] | null }): RepOperationalAlertResponse[] {
    return source.alerts ?? [];
  }

  protected getRecommendedActionLabel(action?: string | null): string {
    return action ? getDisplayLabel(action) : 'Sin acción disponible';
  }

  protected timelineMetadataEntries(item: RepBaseDocumentTimelineEntryResponse): string[] {
    return Object.entries(item.metadata ?? {})
      .filter(([, value]) => value)
      .map(([key, value]) => `${key}: ${value}`);
  }

  protected async applyQuickView(quickView: string): Promise<void> {
    this.quickViewFilter = this.quickViewFilter === quickView ? '' : quickView;
    this.page.set(1);
    await this.load();
  }

  protected async clearOperationalFilters(): Promise<void> {
    this.alertCodeFilter = '';
    this.severityFilter = '';
    this.nextRecommendedActionFilter = '';
    this.quickViewFilter = '';
    this.page.set(1);
    await this.load();
  }

  protected countForQuickView(code: string): number {
    return this.summaryCounts().quickViewCounts.find((item) => item.code === code)?.count ?? 0;
  }

  protected hasOperationalFilters(): boolean {
    return Boolean(this.alertCodeFilter || this.severityFilter || this.nextRecommendedActionFilter || this.quickViewFilter);
  }

  protected isSelected(fiscalDocumentId: number): boolean {
    return this.selectedIds().includes(fiscalDocumentId);
  }

  protected selectedCount(): number {
    return this.selectedIds().length;
  }

  protected allVisibleSelected(): boolean {
    const currentItems = this.items();
    return currentItems.length > 0 && currentItems.every((item) => this.isSelected(item.fiscalDocumentId));
  }

  protected toggleSelection(fiscalDocumentId: number, checked: boolean): void {
    if (checked) {
      this.selectedIds.set([...new Set([...this.selectedIds(), fiscalDocumentId])]);
      return;
    }

    this.selectedIds.set(this.selectedIds().filter((id) => id !== fiscalDocumentId));
  }

  protected toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.selectedIds.set(this.items().map((item) => item.fiscalDocumentId));
      return;
    }

    this.selectedIds.set([]);
  }

  protected clearBulkRefreshResult(): void {
    this.bulkRefreshResult.set(null);
    this.bulkRefreshError.set(null);
  }

  protected isGroupedPaymentSelected(accountsReceivablePaymentId: number): boolean {
    return this.groupedPaymentIds().includes(accountsReceivablePaymentId);
  }

  protected groupedPaymentSelectionCount(): number {
    return this.groupedPaymentIds().length;
  }

  protected toggleGroupedPaymentSelection(accountsReceivablePaymentId: number, checked: boolean): void {
    if (checked) {
      this.groupedPaymentIds.set([...new Set([...this.groupedPaymentIds(), accountsReceivablePaymentId])]);
      return;
    }

    this.groupedPaymentIds.set(this.groupedPaymentIds().filter((id) => id !== accountsReceivablePaymentId));
  }

  protected clearGroupedPaymentSelection(): void {
    this.groupedPaymentIds.set([]);
  }

  protected async refreshSelectedDocuments(): Promise<void> {
    if (!this.selectedCount()) {
      return;
    }

    await this.executeBulkRefresh('Selected');
  }

  protected async refreshFilteredDocuments(): Promise<void> {
    if (!this.items().length) {
      return;
    }

    await this.executeBulkRefresh('Filtered');
  }

  private async handleSuccessfulPaymentRegistration(
    fiscalDocumentId: number,
    result: RegisterInternalRepBaseDocumentPaymentResponse
  ): Promise<void> {
    const paymentIdLabel = result.accountsReceivablePaymentId ?? 'nuevo';
    const successMessage = `Pago ${paymentIdLabel} aplicado. Saldo pendiente: ${result.remainingBalance.toFixed(2)}.`;
    if (result.warningMessages.length) {
      this.feedbackService.show('warning', result.warningMessages.join(' | '));
    }
    this.feedbackService.show('success', successMessage);
    this.cancelRegisterPaymentForm();
    await this.loadDetail(fiscalDocumentId);
    await this.load();
  }

  private async handleSuccessfulPrepare(
    fiscalDocumentId: number,
    result: PrepareInternalRepBaseDocumentPaymentComplementResponse
  ): Promise<void> {
    const complementLabel = result.paymentComplementDocumentId ?? 'existente';
    const statusLabel = result.status ? getDisplayLabel(result.status) : 'preparado';
    if (result.warningMessages.length) {
      this.feedbackService.show('warning', result.warningMessages.join(' | '));
    }

    this.feedbackService.show('success', `REP ${complementLabel} ${statusLabel.toLowerCase()}.`);
    this.clearGroupedPaymentSelection();
    await this.loadDetail(fiscalDocumentId);
    await this.load();
  }

  private async handleSuccessfulStamp(
    fiscalDocumentId: number,
    result: StampInternalRepBaseDocumentPaymentComplementResponse
  ): Promise<void> {
    const successMessage = buildPaymentComplementStampFeedbackMessage(
      result.email,
      'Complemento de pago timbrado correctamente.',
    );

    if (result.warningMessages.length) {
      this.feedbackService.show('warning', result.warningMessages.join(' | '));
    }

    this.feedbackService.show('success', successMessage);
    await this.loadDetail(fiscalDocumentId);
    await this.load();

    if (shouldOpenPaymentComplementEmailComposerAfterStamp(result.email.status)) {
      const updatedComplement = this.findSelectedPaymentComplement(result.paymentComplementDocumentId);
      if (updatedComplement) {
        await this.openPaymentComplementEmailComposer(updatedComplement, result.email);
      }
    }
  }

  private async loadDetail(fiscalDocumentId: number, openRegisterPayment = false): Promise<void> {
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.selectedDetail.set(null);
    this.paymentError.set(null);
    this.repActionError.set(null);
    this.clearGroupedPaymentSelection();

    try {
      const detail = await firstValueFrom(this.api.getInternalBaseDocumentByFiscalDocumentId(fiscalDocumentId));
      this.selectedDetail.set(detail);

      if (openRegisterPayment && detail.summary.isEligible && detail.summary.outstandingBalance > 0) {
        this.openRegisterPaymentForm();
      } else {
        this.showRegisterPaymentForm.set(false);
      }
    } catch (error) {
      this.showRegisterPaymentForm.set(false);
      const message = extractApiErrorMessage(error, 'No fue posible cargar el contexto del CFDI base.');
      this.detailError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.loadingDetail.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchInternalBaseDocuments({
        page: this.page(),
        pageSize: this.pageSize(),
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        eligible: parseBooleanFilter(this.eligibleFilter),
        blocked: parseBooleanFilter(this.blockedFilter),
        withOutstandingBalance: parseBooleanFilter(this.outstandingFilter),
        hasRepEmitted: parseBooleanFilter(this.repEmittedFilter),
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null,
        quickView: this.quickViewFilter || null
      }));

      this.items.set(response.items);
      this.selectedIds.set([]);
      this.summaryCounts.set(response.summaryCounts ?? createEmptySummaryCounts());
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible cargar la bandeja REP interna.'));
      this.summaryCounts.set(createEmptySummaryCounts());
    } finally {
      this.loading.set(false);
    }
  }

  private async executeBulkRefresh(mode: string): Promise<void> {
    this.bulkRefreshing.set(true);
    this.bulkRefreshError.set(null);

    try {
      const result = await firstValueFrom(this.api.bulkRefreshInternalBaseDocuments({
        mode,
        documents: this.selectedIds().map((sourceId) => ({ sourceType: 'Internal', sourceId })),
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        eligible: parseBooleanFilter(this.eligibleFilter),
        blocked: parseBooleanFilter(this.blockedFilter),
        withOutstandingBalance: parseBooleanFilter(this.outstandingFilter),
        hasRepEmitted: parseBooleanFilter(this.repEmittedFilter),
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null,
        quickView: this.quickViewFilter || null
      }));

      this.bulkRefreshResult.set(result);
      this.selectedIds.set([]);

      const refreshedIds = new Set(result.items.filter((item) => item.attempted).map((item) => item.sourceId));
      const detail = this.selectedDetail();

      if (detail && refreshedIds.has(detail.summary.fiscalDocumentId)) {
        await this.loadDetail(detail.summary.fiscalDocumentId, this.showRegisterPaymentForm());
      }

      await this.load();
      this.feedbackService.show(
        result.failedCount > 0 ? 'warning' : 'success',
        `Refresh masivo interno ejecutado: ${result.refreshedCount} actualizados, ${result.noChangesCount} sin cambios, ${result.blockedCount} bloqueados, ${result.failedCount} fallidos.`
      );
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible ejecutar el refresh masivo interno.');
      this.bulkRefreshError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.bulkRefreshing.set(false);
    }
  }

  private getSelectedGroupedPaymentIdsExcluding(anchorPaymentId: number): number[] {
    return this.groupedPaymentIds()
      .filter((paymentId) => paymentId !== anchorPaymentId)
      .sort((left, right) => left - right);
  }

  private findSelectedPaymentComplement(
    paymentComplementId: number | null | undefined,
  ): InternalRepBaseDocumentPaymentComplementResponse | null {
    if (!paymentComplementId) {
      return null;
    }

    return this.selectedDetail()?.issuedReps.find((item) => item.paymentComplementId === paymentComplementId) ?? null;
  }

  private forceClosePaymentComplementEmailComposer(): void {
    this.showPaymentComplementEmailComposer.set(false);
    this.sendingPaymentComplementEmail.set(false);
    this.paymentComplementEmailDraft.set(null);
    this.paymentComplementEmailDraftError.set(null);
    this.paymentComplementEmailRecipientsError.set(null);
    this.selectedPaymentComplementForEmail.set(null);
    this.paymentComplementEmailRecipientsInput = '';
    this.paymentComplementEmailSubject = '';
    this.paymentComplementEmailBody = '';
  }
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

function normalizeOptionalText(value: string): string | null {
  const normalized = value.trim();
  return normalized ? normalized : null;
}

function triggerBlobDownload(blob: Blob, fileName: string): void {
  const objectUrl = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 30000);
}

function getFileNameFromContentDisposition(disposition: string | null): string | null {
  if (!disposition) {
    return null;
  }

  const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1].replace(/"/g, ''));
  }

  const fileNameMatch = /filename="?([^"]+)"?/i.exec(disposition);
  return fileNameMatch?.[1] ?? null;
}

function buildRepFileName(
  complement: Pick<InternalRepBaseDocumentPaymentComplementResponse, 'paymentComplementId' | 'uuid'>,
  extension: 'xml' | 'pdf',
): string {
  const token = complement.uuid ?? complement.paymentComplementId;
  return `REP_${token}.${extension}`;
}

function resolvePaymentComplementStampDetailError(error: unknown): string {
  if (getErrorStatus(error) === 404) {
    return 'No se encontró el detalle de timbrado del complemento de pago.';
  }

  return extractApiErrorMessage(error, 'No fue posible consultar el detalle de timbrado del REP.');
}

function resolvePaymentComplementDownloadError(error: unknown, fileType: 'xml' | 'pdf'): string {
  const status = getErrorStatus(error);
  if (fileType === 'xml') {
    if (status === 404) {
      return 'No se encontró el XML del complemento de pago.';
    }

    return extractApiErrorMessage(error, 'No fue posible descargar el XML del REP.');
  }

  if (status === 404) {
    return 'No se encontró el PDF del complemento de pago.';
  }

  if (status === 409) {
    return 'PDF no disponible para este REP.';
  }

  return extractApiErrorMessage(error, 'No fue posible descargar el PDF del REP.');
}

function resolvePaymentComplementEmailDraftError(error: unknown): string {
  const status = getErrorStatus(error);
  if (status === 404) {
    return 'No se encontró el complemento de pago.';
  }

  if (status === 409) {
    return 'El complemento no está timbrado o no tiene XML/PDF disponible.';
  }

  return extractApiErrorMessage(error, 'No fue posible cargar el borrador de correo del REP.');
}

function resolvePaymentComplementEmailSendError(error: unknown): string {
  const status = getErrorStatus(error);
  switch (status) {
    case 400:
      return extractApiErrorMessage(error, 'Verifica los destinatarios del correo.');
    case 403:
      return 'No tienes permisos para enviar este complemento por correo.';
    case 404:
      return 'No se encontró el complemento de pago.';
    case 409:
      return extractApiErrorMessage(error, 'El complemento no está timbrado o no tiene XML/PDF disponible.');
    case 503:
      return extractApiErrorMessage(error, 'No se pudo enviar el correo. Intenta más tarde.');
    default:
      return extractApiErrorMessage(error, 'No fue posible enviar el complemento de pago por correo.');
  }
}

function getErrorStatus(error: unknown): number | null {
  return typeof error === 'object' && error !== null && 'status' in error && typeof (error as { status?: unknown }).status === 'number'
    ? ((error as { status: number }).status)
    : null;
}

function parseBooleanFilter(value: string): boolean | null {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return null;
}

function todayInputValue(): string {
  return new Date().toISOString().slice(0, 10);
}

function createEmptySummaryCounts(): RepOperationalSummaryCountsResponse {
  return {
    infoCount: 0,
    warningCount: 0,
    errorCount: 0,
    criticalCount: 0,
    blockedCount: 0,
    alertCounts: [],
    nextRecommendedActionCounts: [],
    quickViewCounts: []
  };
}

function resolvePrimarySeverity(alerts: RepOperationalAlertResponse[]): string | null {
  for (const severity of REP_OPERATIONAL_SEVERITY_PRIORITY) {
    if (alerts.some((alert) => alert.severity === severity)) {
      return severity;
    }
  }

  return null;
}

const REP_OPERATIONAL_ALERT_OPTIONS = [
  'AppliedPaymentsWithoutStampedRep',
  'PreparedRepPendingStamp',
  'RepStampingRejected',
  'RepCancellationRejected',
  'BlockedOperation',
  'CancelledBaseDocument',
  'ValidationBlocked',
  'SatValidationUnavailable',
  'UnsupportedCurrency',
  'DuplicateExternalInvoice',
  'StampedRepAvailable'
];

const REP_OPERATIONAL_SEVERITY_OPTIONS = ['info', 'warning', 'error', 'critical'];
const REP_OPERATIONAL_SEVERITY_PRIORITY = ['critical', 'error', 'warning', 'info'];
const REP_RECOMMENDED_ACTION_OPTIONS = ['RegisterPayment', 'PrepareRep', 'StampRep', 'RefreshRepStatus', 'CancelRep', 'ViewDetail', 'Blocked', 'NoAction'];
const REP_QUICK_VIEW_OPTIONS = ['PendingStamp', 'WithError', 'Blocked', 'AppliedPaymentWithoutStampedRep', 'PendingRefresh', 'Stamped'];
