import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import {
  InternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentItemResponse,
  InternalRepBaseDocumentPaymentComplementResponse,
  InternalRepBaseDocumentPaymentHistoryResponse,
  PrepareInternalRepBaseDocumentPaymentComplementResponse,
  RepOperationalAlertResponse,
  RepOperationalSummaryCountsResponse,
  RegisterInternalRepBaseDocumentPaymentResponse,
  StampInternalRepBaseDocumentPaymentComplementResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-base-documents-page',
  imports: [FormsModule, DecimalPipe],
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

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
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

      @if (showDetailModal()) {
        <section class="modal-backdrop" (click)="closeDetailModal()">
          <section class="modal-card detail-modal" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <div>
                <p class="eyebrow">Complementos de pago</p>
                <h3>Contexto del CFDI base</h3>
              </div>
              <div class="modal-actions">
                @if (selectedDetail()?.summary?.isEligible && (selectedDetail()?.summary?.outstandingBalance ?? 0) > 0) {
                  <button type="button" (click)="openRegisterPaymentForm()">Registrar pago</button>
                }
                <button type="button" class="secondary" (click)="closeDetailModal()">Cerrar</button>
              </div>
            </div>

            @if (loadingDetail()) {
              <p class="helper">Cargando contexto del CFDI...</p>
            } @else if (detailError()) {
              <p class="error">{{ detailError() }}</p>
            } @else if (selectedDetail(); as detail) {
              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Resumen fiscal</h4>
                  <dl>
                    <div><dt>FiscalDocumentId</dt><dd>{{ detail.summary.fiscalDocumentId }}</dd></div>
                    <div><dt>BillingDocumentId</dt><dd>{{ detail.summary.billingDocumentId ?? '—' }}</dd></div>
                    <div><dt>SalesOrderId</dt><dd>{{ detail.summary.salesOrderId ?? '—' }}</dd></div>
                    <div><dt>UUID</dt><dd>{{ detail.summary.uuid || '—' }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(detail.summary) }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(detail.summary.issuedAtUtc) }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ detail.summary.receiverRfc }} · {{ detail.summary.receiverLegalName }}</dd></div>
                    <div><dt>Método SAT</dt><dd>{{ detail.summary.paymentMethodSat }}</dd></div>
                    <div><dt>Forma SAT</dt><dd>{{ detail.summary.paymentFormSat }}</dd></div>
                    <div><dt>Moneda</dt><dd>{{ detail.summary.currencyCode }}</dd></div>
                    <div><dt>Estado fiscal</dt><dd>{{ getDisplayLabel(detail.summary.fiscalStatus) }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Resumen operativo</h4>
                  <dl>
                    <div><dt>AR Invoice</dt><dd>{{ detail.summary.accountsReceivableInvoiceId ?? '—' }}</dd></div>
                    <div><dt>Estado REP</dt><dd>{{ getDisplayLabel(detail.summary.repOperationalStatus) }}</dd></div>
                    <div><dt>Total</dt><dd>{{ detail.summary.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Pagado</dt><dd>{{ detail.summary.paidTotal | number:'1.2-2' }}</dd></div>
                    <div><dt>Saldo</dt><dd>{{ detail.summary.outstandingBalance | number:'1.2-2' }}</dd></div>
                    <div><dt>Pagos registrados</dt><dd>{{ detail.summary.registeredPaymentCount }}</dd></div>
                    <div><dt>REP ligados</dt><dd>{{ detail.summary.paymentComplementCount }}</dd></div>
                    <div><dt>REP emitidos</dt><dd>{{ detail.summary.stampedPaymentComplementCount }}</dd></div>
                    <div><dt>Último REP</dt><dd>{{ formatOptionalUtc(detail.summary.lastRepIssuedAtUtc) }}</dd></div>
                    <div><dt>Estatus CxC</dt><dd>{{ detail.summary.accountsReceivableStatus ? getDisplayLabel(detail.summary.accountsReceivableStatus) : '—' }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Explicación de elegibilidad</h4>
                  <div class="eligibility-box">
                    <span class="status-pill" [class.status-eligible]="detail.summary.isEligible" [class.status-blocked]="detail.summary.isBlocked" [class.status-ineligible]="!detail.summary.isEligible && !detail.summary.isBlocked">
                      {{ detail.summary.eligibility.status }}
                    </span>
                    <p class="eligibility-reason">{{ detail.summary.eligibility.primaryReasonMessage }}</p>
                    <p class="helper">Código: {{ detail.summary.eligibility.primaryReasonCode }} · Evaluado: {{ formatUtc(detail.summary.eligibility.evaluatedAtUtc) }}</p>
                  </div>

                  @if (detail.summary.eligibility.secondarySignals.length) {
                    <ul class="signal-list">
                      @for (signal of detail.summary.eligibility.secondarySignals; track signal.code + '-' + signal.message) {
                        <li class="signal-item">
                          <span class="signal-pill" [class.signal-positive]="signal.severity === 'Satisfied'" [class.signal-warning]="signal.severity !== 'Satisfied'">{{ signal.severity }}</span>
                          <div>
                            <strong>{{ signal.code }}</strong>
                            <p>{{ signal.message }}</p>
                          </div>
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="helper">No hay señales secundarias adicionales para este CFDI.</p>
                  }
                </article>

                <article class="summary-card">
                  <h4>Snapshot operativo persistido</h4>
                  @if (detail.operationalState; as operationalState) {
                    <dl>
                      <div><dt>Última evaluación</dt><dd>{{ formatUtc(operationalState.lastEligibilityEvaluatedAtUtc) }}</dd></div>
                      <div><dt>Estatus persistido</dt><dd>{{ operationalState.lastEligibilityStatus }}</dd></div>
                      <div><dt>Motivo persistido</dt><dd>{{ operationalState.lastPrimaryReasonMessage }}</dd></div>
                      <div><dt>Código persistido</dt><dd>{{ operationalState.lastPrimaryReasonCode }}</dd></div>
                      <div><dt>REP pendiente</dt><dd>{{ operationalState.repPendingFlag ? 'Sí' : 'No' }}</dd></div>
                      <div><dt>Total pagado aplicado</dt><dd>{{ operationalState.totalPaidApplied | number:'1.2-2' }}</dd></div>
                      <div><dt>Conteo REP</dt><dd>{{ operationalState.repCount }}</dd></div>
                      <div><dt>Último REP emitido</dt><dd>{{ formatOptionalUtc(operationalState.lastRepIssuedAtUtc) }}</dd></div>
                    </dl>
                  } @else {
                    <p class="helper">Todavía no existe snapshot operativo persistido para este CFDI.</p>
                  }
                </article>

                <article class="summary-card">
                  <h4>Seguimiento operativo</h4>
                  <dl>
                    <div><dt>Acción recomendada</dt><dd>{{ getRecommendedActionLabel(detail.summary.nextRecommendedAction) }}</dd></div>
                    <div><dt>Operación bloqueada</dt><dd>{{ detail.summary.hasBlockedOperation ? 'Sí' : 'No' }}</dd></div>
                    <div><dt>Pago sin REP timbrado</dt><dd>{{ detail.summary.hasAppliedPaymentsWithoutStampedRep ? 'Sí' : 'No' }}</dd></div>
                    <div><dt>REP pendiente de timbrar</dt><dd>{{ detail.summary.hasPreparedRepPendingStamp ? 'Sí' : 'No' }}</dd></div>
                    <div><dt>REP con error</dt><dd>{{ detail.summary.hasRepWithError ? 'Sí' : 'No' }}</dd></div>
                  </dl>

                  @if (getAlerts(detail.summary).length) {
                    <ul class="alert-list">
                      @for (alert of getAlerts(detail.summary); track alert.code + '-' + alert.message) {
                        <li class="alert-item" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-info]="alert.severity === 'info'">
                          <strong>{{ getDisplayLabel(alert.code) }}</strong>
                          <p>{{ alert.message }}</p>
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="helper">No hay alertas operativas activas para este CFDI.</p>
                  }
                </article>
              </section>

              <section class="nested-card">
                <div class="section-header">
                  <div>
                    <h4>Registro de pago</h4>
                    <p class="helper">El pago se registra y se aplica completo al CFDI base actual. Después puedes preparar y timbrar el REP sobre ese mismo pago desde el historial inferior.</p>
                  </div>
                  @if (!showRegisterPaymentForm()) {
                    <button
                      type="button"
                      [disabled]="!detail.summary.isEligible || detail.summary.outstandingBalance <= 0 || submittingPayment()"
                      (click)="openRegisterPaymentForm()">
                      Registrar pago
                    </button>
                  }
                </div>

                @if (showRegisterPaymentForm()) {
                  <form class="payment-form" (ngSubmit)="submitRegisterPayment()">
                    <label><span>Fecha de pago</span><input [(ngModel)]="paymentDate" name="paymentDate" type="date" [disabled]="submittingPayment()" /></label>
                    <label><span>Forma SAT</span><input [(ngModel)]="paymentFormSat" name="paymentFormSat" maxlength="3" [disabled]="submittingPayment()" /></label>
                    <label><span>Monto</span><input [(ngModel)]="paymentAmount" name="paymentAmount" type="number" min="0" step="0.01" [disabled]="submittingPayment()" /></label>
                    <label><span>Referencia</span><input [(ngModel)]="paymentReference" name="paymentReference" [disabled]="submittingPayment()" /></label>
                    <label class="wide"><span>Notas</span><textarea [(ngModel)]="paymentNotes" name="paymentNotes" rows="3" [disabled]="submittingPayment()"></textarea></label>

                    <div class="wide helper">
                      Saldo disponible para aplicar: {{ detail.summary.outstandingBalance | number:'1.2-2' }}
                    </div>

                    @if (paymentError()) {
                      <p class="error wide">{{ paymentError() }}</p>
                    }

                    <div class="actions wide">
                      <button type="submit" [disabled]="submittingPayment()">{{ submittingPayment() ? 'Aplicando...' : 'Confirmar pago' }}</button>
                      <button type="button" class="secondary" (click)="cancelRegisterPaymentForm()" [disabled]="submittingPayment()">Cancelar</button>
                    </div>
                  </form>
                } @else if (!detail.summary.isEligible || detail.summary.outstandingBalance <= 0) {
                  <p class="helper">El CFDI no puede recibir pagos desde esta vista: {{ detail.summary.eligibility.primaryReasonMessage }}</p>
                }
              </section>

              <section class="nested-card">
                <div class="section-header">
                  <div>
                    <h4>Historial de pagos registrados</h4>
                    <p class="helper">Cada pago aplicado al CFDI puede preparar un REP una sola vez. Si ya existe un complemento para el pago, se muestra como ligado.</p>
                  </div>
                </div>

                @if (repActionError()) {
                  <p class="error">{{ repActionError() }}</p>
                }

                @if (!detail.paymentHistory.length) {
                  <p class="helper">Todavía no hay pagos registrados relacionados con este CFDI dentro del sistema.</p>
                } @else {
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>PaymentId</th>
                          <th>Fecha</th>
                          <th>Forma</th>
                          <th>Monto pago</th>
                          <th>Aplicado al CFDI</th>
                          <th>Remanente del pago</th>
                          <th>Referencia</th>
                          <th>REP ligado</th>
                          <th>Estatus REP</th>
                          <th>Acción</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (payment of detail.paymentHistory; track payment.accountsReceivablePaymentId + '-' + payment.createdAtUtc) {
                          <tr>
                            <td>{{ payment.accountsReceivablePaymentId }}</td>
                            <td>{{ formatUtc(payment.paymentDateUtc) }}</td>
                            <td>{{ payment.paymentFormSat }}</td>
                            <td>{{ payment.paymentAmount | number:'1.2-2' }}</td>
                            <td>{{ payment.amountAppliedToDocument | number:'1.2-2' }}</td>
                            <td>{{ payment.remainingPaymentAmount | number:'1.2-2' }}</td>
                            <td>{{ payment.reference || '—' }}</td>
                            <td>{{ payment.paymentComplementUuid || (payment.paymentComplementId ? '#' + payment.paymentComplementId : '—') }}</td>
                            <td>{{ payment.paymentComplementStatus ? getDisplayLabel(payment.paymentComplementStatus) : '—' }}</td>
                            <td>
                              @if (canPrepareComplement(payment)) {
                                <button
                                  type="button"
                                  class="small"
                                  [disabled]="preparingComplement() || stampingComplement()"
                                  (click)="preparePaymentComplement(payment)">
                                  {{ preparingComplement() === payment.accountsReceivablePaymentId ? 'Preparando...' : 'Preparar REP' }}
                                </button>
                              } @else {
                                <span class="helper">—</span>
                              }
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              </section>

              <section class="nested-card">
                <h4>Aplicaciones de pago</h4>
                @if (!detail.paymentApplications.length) {
                  <p class="helper">Todavía no hay pagos aplicados a este CFDI dentro del sistema.</p>
                } @else {
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>PaymentId</th>
                          <th>Fecha</th>
                          <th>Forma</th>
                          <th>Monto pago</th>
                          <th>Parcialidad</th>
                          <th>Aplicado</th>
                          <th>Saldo anterior</th>
                          <th>Saldo nuevo</th>
                          <th>Remanente</th>
                          <th>Referencia</th>
                          <th>Notas</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (application of detail.paymentApplications; track application.accountsReceivablePaymentId + '-' + application.applicationSequence) {
                          <tr>
                            <td>{{ application.accountsReceivablePaymentId }}</td>
                            <td>{{ formatUtc(application.paymentDateUtc) }}</td>
                            <td>{{ application.paymentFormSat }}</td>
                            <td>{{ application.paymentAmount | number:'1.2-2' }}</td>
                            <td>{{ application.applicationSequence }}</td>
                            <td>{{ application.appliedAmount | number:'1.2-2' }}</td>
                            <td>{{ application.previousBalance | number:'1.2-2' }}</td>
                            <td>{{ application.newBalance | number:'1.2-2' }}</td>
                            <td>{{ application.remainingPaymentAmount | number:'1.2-2' }}</td>
                            <td>{{ application.reference || '—' }}</td>
                            <td>{{ application.notes || '—' }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              </section>

              <section class="nested-card">
                <div class="section-header">
                  <div>
                    <h4>REP emitidos y relacionados</h4>
                    <p class="helper">Los complementos preparados quedan listos para timbrado desde este mismo contexto operativo.</p>
                  </div>
                </div>

                @if (!detail.issuedReps.length) {
                  <p class="helper">Aún no hay REP ligados a este CFDI base.</p>
                } @else {
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Complemento</th>
                          <th>PaymentId</th>
                          <th>Estado</th>
                          <th>UUID</th>
                          <th>Proveedor</th>
                          <th>Parcialidad</th>
                          <th>Fecha pago</th>
                          <th>Emisión</th>
                          <th>Timbrado</th>
                          <th>Cancelación</th>
                          <th>Saldo anterior</th>
                          <th>Monto</th>
                          <th>Saldo remanente</th>
                          <th>Acción</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (complement of detail.issuedReps; track complement.paymentComplementId) {
                          <tr>
                            <td>{{ complement.paymentComplementId }}</td>
                            <td>{{ complement.accountsReceivablePaymentId }}</td>
                            <td>{{ getDisplayLabel(complement.status) }}</td>
                            <td>{{ complement.uuid || '—' }}</td>
                            <td>{{ complement.providerName || '—' }}</td>
                            <td>{{ complement.installmentNumber }}</td>
                            <td>{{ formatUtc(complement.paymentDateUtc) }}</td>
                            <td>{{ formatOptionalUtc(complement.issuedAtUtc) }}</td>
                            <td>{{ formatOptionalUtc(complement.stampedAtUtc) }}</td>
                            <td>{{ formatOptionalUtc(complement.cancelledAtUtc) }}</td>
                            <td>{{ complement.previousBalance | number:'1.2-2' }}</td>
                            <td>{{ complement.paidAmount | number:'1.2-2' }}</td>
                            <td>{{ complement.remainingBalance | number:'1.2-2' }}</td>
                            <td>
                              <div class="row-actions">
                                @if (canStampComplement(complement)) {
                                  <button
                                    type="button"
                                    class="small"
                                    [disabled]="stampingComplement() !== null || preparingComplement() !== null || refreshingComplement() !== null || cancellingComplement() !== null"
                                    (click)="stampPaymentComplement(complement)">
                                    {{ stampingComplement() === complement.paymentComplementId ? (complement.status === 'StampingRejected' ? 'Reintentando...' : 'Timbrando...') : (complement.status === 'StampingRejected' ? 'Reintentar timbrado' : 'Timbrar REP') }}
                                  </button>
                                }
                                @if (canRefreshComplement(complement)) {
                                  <button
                                    type="button"
                                    class="secondary small"
                                    [disabled]="stampingComplement() !== null || preparingComplement() !== null || refreshingComplement() !== null || cancellingComplement() !== null"
                                    (click)="refreshPaymentComplement(complement)">
                                    {{ refreshingComplement() === complement.paymentComplementId ? 'Refrescando...' : 'Refrescar' }}
                                  </button>
                                }
                                @if (canCancelComplement(complement)) {
                                  <button
                                    type="button"
                                    class="secondary small"
                                    [disabled]="stampingComplement() !== null || preparingComplement() !== null || refreshingComplement() !== null || cancellingComplement() !== null"
                                    (click)="cancelPaymentComplement(complement)">
                                    {{ cancellingComplement() === complement.paymentComplementId ? 'Cancelando...' : 'Cancelar' }}
                                  </button>
                                }
                                @if (!canStampComplement(complement) && !canRefreshComplement(complement) && !canCancelComplement(complement)) {
                                  <span class="helper">—</span>
                                }
                              </div>
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              </section>
            }
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
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.45rem 0.7rem; font-size:0.88rem; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .actions, .toolbar, .pagination, .modal-actions, .section-header, .row-actions, .quick-filters { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    .toolbar, .pagination { justify-content:space-between; }
    .section-header { justify-content:space-between; margin-bottom:0.75rem; }
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
    .modal-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.42); display:grid; place-items:center; padding:1rem; z-index:50; }
    .modal-card { width:min(1180px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .detail-modal { align-content:start; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .payment-form { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:1rem; align-items:end; }
    textarea { font:inherit; border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; resize:vertical; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(300px, 1fr)); gap:1rem; }
    dl { display:grid; gap:0.5rem; margin:0; }
    dl div { display:grid; gap:0.15rem; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0; font-weight:600; color:#182533; }
    @media (max-width: 720px) {
      .toolbar, .pagination, .modal-header { flex-direction:column; align-items:stretch; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementBaseDocumentsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);
  private readonly feedbackService = inject(FeedbackService);
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
  protected readonly summaryCounts = signal<RepOperationalSummaryCountsResponse>(createEmptySummaryCounts());
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filtersError = signal<string | null>(null);
  protected readonly showDetailModal = signal(false);
  protected readonly selectedDetail = signal<InternalRepBaseDocumentDetailResponse | null>(null);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly showRegisterPaymentForm = signal(false);
  protected readonly submittingPayment = signal(false);
  protected readonly paymentError = signal<string | null>(null);
  protected readonly preparingComplement = signal<number | null>(null);
  protected readonly stampingComplement = signal<number | null>(null);
  protected readonly refreshingComplement = signal<number | null>(null);
  protected readonly cancellingComplement = signal<number | null>(null);
  protected readonly repActionError = signal<string | null>(null);
  protected paymentDate = todayInputValue();
  protected paymentFormSat = '03';
  protected paymentAmount: number | null = null;
  protected paymentReference = '';
  protected paymentNotes = '';

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
      const result = await firstValueFrom(this.api.prepareInternalBaseDocumentPaymentComplement(detail.summary.fiscalDocumentId, {
        accountsReceivablePaymentId: payment.accountsReceivablePaymentId
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
    await this.loadDetail(fiscalDocumentId);
    await this.load();
  }

  private async handleSuccessfulStamp(
    fiscalDocumentId: number,
    result: StampInternalRepBaseDocumentPaymentComplementResponse
  ): Promise<void> {
    if (result.warningMessages.length) {
      this.feedbackService.show('warning', result.warningMessages.join(' | '));
    }

    const suffix = result.stampUuid ? ` UUID ${result.stampUuid}.` : '.';
    this.feedbackService.show('success', `REP ${result.paymentComplementDocumentId ?? 'preparado'} timbrado${suffix}`);
    await this.loadDetail(fiscalDocumentId);
    await this.load();
  }

  private async loadDetail(fiscalDocumentId: number, openRegisterPayment = false): Promise<void> {
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.selectedDetail.set(null);
    this.paymentError.set(null);
    this.repActionError.set(null);

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
