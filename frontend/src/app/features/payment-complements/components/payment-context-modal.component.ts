import { DecimalPipe, NgClass } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StatusBadgeComponent, StatusBadgeTone } from '../../../shared/components/status-badge.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import {
  InternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentItemResponse,
  InternalRepBaseDocumentPaymentApplicationResponse,
  InternalRepBaseDocumentPaymentComplementResponse,
  InternalRepBaseDocumentPaymentHistoryResponse,
  RepBaseDocumentTimelineEntryResponse,
} from '../models/payment-complements.models';
import { PaymentContextFieldGridComponent, PaymentContextFieldItem } from './payment-context-field-grid.component';
import { PaymentContextTabItem, PaymentContextTabsComponent } from './payment-context-tabs.component';

type MainTabId = 'summary' | 'eligibility' | 'payments' | 'timeline' | 'technical';
type PaymentsTabId = 'history' | 'applications' | 'reps';

interface PriorityFlag {
  label: string;
  tone: StatusBadgeTone;
  message: string;
}

interface GroupedPaymentToggleEvent {
  accountsReceivablePaymentId: number;
  checked: boolean;
}

@Component({
  selector: 'app-payment-context-modal',
  imports: [
    DecimalPipe,
    FormsModule,
    NgClass,
    PaymentContextFieldGridComponent,
    PaymentContextTabsComponent,
    StatusBadgeComponent,
  ],
  template: `
    @if (open()) {
      <section class="modal-backdrop" (click)="closeRequested.emit()">
        <section
          class="modal-shell"
          role="dialog"
          aria-modal="true"
          aria-labelledby="payment-context-title"
          (click)="$event.stopPropagation()"
        >
          <header class="modal-header">
            <div class="header-copy">
              <div>
                <p class="eyebrow">Complementos de pago</p>
                <h3 id="payment-context-title">Contexto del CFDI base</h3>
              </div>

              @if (summary(); as currentSummary) {
                <div class="header-chips">
                  <app-status-badge [label]="currentSummary.repOperationalStatus" [tone]="repStatusTone(currentSummary)" />
                  <app-status-badge [label]="currentSummary.fiscalStatus" [tone]="fiscalStatusTone(currentSummary.fiscalStatus)" />
                  <span class="metric-chip">Saldo {{ formatAmountWithCurrency(currentSummary.outstandingBalance, currentSummary.currencyCode) }}</span>
                  <app-status-badge [label]="currentSummary.nextRecommendedAction" [tone]="actionTone(currentSummary.nextRecommendedAction)" />
                </div>
              }
            </div>

            <div class="header-actions">
              @if (canRegisterPayment(summary())) {
                <button type="button" (click)="registerPaymentRequested.emit()">Registrar pago</button>
              }
              <button type="button" class="secondary" (click)="closeRequested.emit()">Cerrar</button>
            </div>
          </header>

          <div class="modal-body">
            @if (loading()) {
              <section class="empty-panel">
                <p class="helper">Cargando contexto del CFDI...</p>
              </section>
            } @else if (error()) {
              <section class="empty-panel">
                <p class="error">{{ error() }}</p>
              </section>
            } @else if (detail(); as detail) {
              <section class="executive-grid">
                <article class="executive-card">
                  <div class="card-header">
                    <h4>CFDI</h4>
                    <span class="mini-pill">#{{ detail.summary.fiscalDocumentId }}</span>
                  </div>
                  <app-payment-context-field-grid [fields]="executiveCfdiFields()" [minColumnWidth]="130" />
                </article>

                <article class="executive-card">
                  <div class="card-header">
                    <h4>Importes</h4>
                    <span class="mini-pill">{{ detail.summary.currencyCode }}</span>
                  </div>
                  <app-payment-context-field-grid [fields]="executiveAmountFields()" [minColumnWidth]="120" />
                </article>

                <article class="executive-card">
                  <div class="card-header">
                    <h4>Estado REP</h4>
                    <app-status-badge [label]="detail.summary.repOperationalStatus" [tone]="repStatusTone(detail.summary)" />
                  </div>
                  <app-payment-context-field-grid [fields]="executiveRepFields()" [minColumnWidth]="120" />
                </article>

                <article class="executive-card">
                  <div class="card-header">
                    <h4>Elegibilidad</h4>
                    <app-status-badge [label]="detail.summary.eligibility.status" [tone]="eligibilityTone(detail.summary.eligibility.status)" />
                  </div>
                  <app-payment-context-field-grid [fields]="executiveEligibilityFields()" [minColumnWidth]="130" />
                </article>

                <article class="executive-card executive-card-alerts">
                  <div class="card-header">
                    <h4>Alertas</h4>
                    <span class="mini-pill">{{ alerts().length }}</span>
                  </div>

                  @if (priorityFlags().length) {
                    <div class="priority-flag-list">
                      @for (flag of priorityFlags(); track flag.label) {
                        <div class="priority-flag" [ngClass]="flag.tone" [attr.title]="flag.message">
                          <strong>{{ flag.label }}</strong>
                          <span>{{ flag.message }}</span>
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="helper">Sin alertas activas.</p>
                  }
                </article>
              </section>

              <div class="tabs-shell">
                <app-payment-context-tabs
                  [tabs]="mainTabs"
                  [activeTab]="activeMainTab()"
                  [ariaLabel]="'Secciones del contexto del CFDI base'"
                  [idPrefix]="'payment-context-main'"
                  (tabChanged)="activeMainTab.set(asMainTab($event))"
                />
              </div>

              @if (activeMainTab() === 'summary') {
                <section
                  class="tab-panel"
                  role="tabpanel"
                  id="payment-context-main-panel-summary"
                  aria-labelledby="payment-context-main-tab-summary"
                >
                  <div class="panel-grid">
                    <article class="panel-card">
                      <div class="panel-header">
                        <h4>Resumen fiscal</h4>
                      </div>
                      <app-payment-context-field-grid [fields]="fiscalSummaryFields()" [minColumnWidth]="150" />
                    </article>

                    <article class="panel-card">
                      <div class="panel-header">
                        <h4>Resumen operativo</h4>
                      </div>
                      <app-payment-context-field-grid [fields]="operationalSummaryFields()" [minColumnWidth]="150" />
                    </article>

                    <article class="panel-card">
                      <div class="panel-header">
                        <h4>Seguimiento operativo</h4>
                        <app-status-badge [label]="detail.summary.nextRecommendedAction" [tone]="actionTone(detail.summary.nextRecommendedAction)" />
                      </div>
                      <app-payment-context-field-grid [fields]="followUpFields()" [minColumnWidth]="150" />
                    </article>

                    <article class="panel-card">
                      <div class="panel-header">
                        <h4>Alertas activas</h4>
                      </div>

                      @if (priorityFlags().length) {
                        <div class="alert-emphasis-list">
                          @for (flag of priorityFlags(); track flag.label) {
                            <div class="alert-emphasis" [ngClass]="flag.tone">
                              <strong>{{ flag.label }}</strong>
                              <p>{{ flag.message }}</p>
                            </div>
                          }
                        </div>
                      }

                      @if (alerts().length) {
                        <ul class="alert-list">
                          @for (alert of alerts(); track alert.code + '-' + alert.message) {
                            <li class="alert-item" [ngClass]="severityToneClass(alert.severity)">
                              <div class="item-header">
                                <app-status-badge [label]="alert.code" [tone]="alertTone(alert.severity)" />
                                <span class="item-meta">{{ getDisplayLabel(alert.severity) }}</span>
                              </div>
                              <p>{{ alert.message }}</p>
                            </li>
                          }
                        </ul>
                      } @else if (!priorityFlags().length) {
                        <p class="helper">Sin alertas activas.</p>
                      }
                    </article>
                  </div>
                </section>
              }

              @if (activeMainTab() === 'eligibility') {
                <section
                  class="tab-panel"
                  role="tabpanel"
                  id="payment-context-main-panel-eligibility"
                  aria-labelledby="payment-context-main-tab-eligibility"
                >
                  <article class="panel-card eligibility-hero">
                    <div class="panel-header">
                      <div>
                        <h4>Explicacion de elegibilidad</h4>
                        <p class="helper">Primero se muestran las reglas problemáticas. Las satisfechas quedan agrupadas al final para reducir ruido visual.</p>
                      </div>
                      <app-status-badge
                        [label]="detail.summary.eligibility.status"
                        [tone]="eligibilityTone(detail.summary.eligibility.status)"
                      />
                    </div>

                    <p class="primary-reason" [attr.title]="detail.summary.eligibility.primaryReasonMessage">
                      {{ detail.summary.eligibility.primaryReasonMessage }}
                    </p>
                    <app-payment-context-field-grid [fields]="eligibilityOverviewFields()" [minColumnWidth]="160" />

                    @if (canEnsureAccountsReceivable(detail)) {
                      <div class="inline-callout info">
                        <div>
                          <strong>Cuenta por cobrar pendiente</strong>
                          <p>Este CFDI ya cumple el patrón fiscal del flujo diferido, pero aún requiere la cuenta operativa para saldo, parcialidades y aplicaciones.</p>
                        </div>
                        <button
                          type="button"
                          [disabled]="ensuringAccountsReceivable()"
                          (click)="ensureAccountsReceivableRequested.emit(detail.summary.fiscalDocumentId)"
                        >
                          {{ ensuringAccountsReceivable() ? 'Habilitando...' : 'Habilitar cuenta por cobrar' }}
                        </button>
                      </div>
                    }
                  </article>

                  <article class="panel-card">
                    <div class="panel-header">
                      <h4>Reglas con atención</h4>
                      <span class="mini-pill">{{ problematicSignals().length }}</span>
                    </div>

                    @if (problematicSignals().length) {
                      <ul class="rule-list">
                        @for (signal of problematicSignals(); track signal.code + '-' + signal.message) {
                          <li class="rule-item">
                            <app-status-badge [label]="signal.severity" [tone]="signalTone(signal.severity)" />
                            <div>
                              <strong>{{ getDisplayLabel(signal.code) }}</strong>
                              <p>{{ signal.message }}</p>
                            </div>
                          </li>
                        }
                      </ul>
                    } @else {
                      <p class="helper">No hay reglas faltantes, fallidas o en advertencia para este CFDI.</p>
                    }
                  </article>

                  <article class="panel-card">
                    <details class="rule-disclosure" [open]="problematicSignals().length === 0 && satisfiedSignals().length > 0">
                      <summary>
                        Reglas satisfechas
                        <span class="mini-pill">{{ satisfiedSignals().length }}</span>
                      </summary>

                      @if (satisfiedSignals().length) {
                        <ul class="rule-list compact">
                          @for (signal of satisfiedSignals(); track signal.code + '-' + signal.message) {
                            <li class="rule-item">
                              <app-status-badge [label]="signal.severity" [tone]="signalTone(signal.severity)" />
                              <div>
                                <strong>{{ getDisplayLabel(signal.code) }}</strong>
                                <p>{{ signal.message }}</p>
                              </div>
                            </li>
                          }
                        </ul>
                      } @else {
                        <p class="helper">No hay reglas satisfechas registradas para esta evaluación.</p>
                      }
                    </details>
                  </article>
                </section>
              }

              @if (activeMainTab() === 'payments') {
                <section
                  class="tab-panel"
                  role="tabpanel"
                  id="payment-context-main-panel-payments"
                  aria-labelledby="payment-context-main-tab-payments"
                >
                  <article class="panel-card">
                    <div class="panel-header">
                      <div>
                        <h4>Registro de pago</h4>
                        <p class="helper">El pago se registra y se aplica completo al CFDI base actual. Desde el historial puedes preparar, timbrar o refrescar REP sin salir del contexto.</p>
                      </div>
                      @if (!showRegisterPaymentForm() && canRegisterPayment(detail.summary)) {
                        <button type="button" (click)="registerPaymentRequested.emit()">Registrar pago</button>
                      }
                    </div>

                    <div class="status-card-grid">
                      <div class="status-card">
                        <span class="status-card-label">Estado de cobro</span>
                        <strong>{{ detail.summary.outstandingBalance > 0 ? 'Con saldo pendiente' : 'Sin saldo pendiente' }}</strong>
                        <small>{{ formatAmountWithCurrency(detail.summary.outstandingBalance, detail.summary.currencyCode) }}</small>
                      </div>
                      <div class="status-card">
                        <span class="status-card-label">Pago registrado</span>
                        <strong>{{ detail.summary.registeredPaymentCount }}</strong>
                        <small>{{ detail.summary.registeredPaymentCount === 1 ? 'movimiento' : 'movimientos' }}</small>
                      </div>
                      <div class="status-card">
                        <span class="status-card-label">REP emitidos</span>
                        <strong>{{ detail.summary.stampedPaymentComplementCount }}</strong>
                        <small>{{ detail.summary.paymentComplementCount }} ligados</small>
                      </div>
                      <div class="status-card">
                        <span class="status-card-label">Accion recomendada</span>
                        <strong>{{ getDisplayLabel(detail.summary.nextRecommendedAction) }}</strong>
                        <small>{{ detail.summary.accountsReceivableStatus ? getDisplayLabel(detail.summary.accountsReceivableStatus) : 'Sin estatus CxC' }}</small>
                      </div>
                    </div>

                    @if (priorityFlags().length) {
                      <div class="priority-chip-row">
                        @for (flag of priorityFlags(); track flag.label) {
                          <span class="priority-chip" [ngClass]="flag.tone">{{ flag.label }}</span>
                        }
                      </div>
                    }

                    @if (showRegisterPaymentForm()) {
                      <form class="payment-form" (ngSubmit)="registerPaymentSubmitted.emit()">
                        <label>
                          <span>Fecha de pago</span>
                          <input
                            [ngModel]="paymentDate()"
                            (ngModelChange)="paymentDateChange.emit($event)"
                            name="paymentDate"
                            type="date"
                            [disabled]="submittingPayment()"
                          />
                        </label>
                        <label>
                          <span>Forma SAT</span>
                          <input
                            [ngModel]="paymentFormSat()"
                            (ngModelChange)="paymentFormSatChange.emit($event)"
                            name="paymentFormSat"
                            maxlength="3"
                            [disabled]="submittingPayment()"
                          />
                        </label>
                        <label>
                          <span>Monto</span>
                          <input
                            [ngModel]="paymentAmount()"
                            (ngModelChange)="paymentAmountChange.emit($event)"
                            name="paymentAmount"
                            type="number"
                            min="0"
                            step="0.01"
                            [disabled]="submittingPayment()"
                          />
                        </label>
                        <label>
                          <span>Referencia</span>
                          <input
                            [ngModel]="paymentReference()"
                            (ngModelChange)="paymentReferenceChange.emit($event)"
                            name="paymentReference"
                            [disabled]="submittingPayment()"
                          />
                        </label>
                        <label class="wide">
                          <span>Notas</span>
                          <textarea
                            [ngModel]="paymentNotes()"
                            (ngModelChange)="paymentNotesChange.emit($event)"
                            name="paymentNotes"
                            rows="3"
                            [disabled]="submittingPayment()"
                          ></textarea>
                        </label>

                        <div class="wide helper">
                          Saldo disponible para aplicar: {{ detail.summary.outstandingBalance | number: '1.2-2' }}
                        </div>

                        @if (paymentError()) {
                          <p class="error wide">{{ paymentError() }}</p>
                        }

                        <div class="wide form-actions">
                          <button type="submit" [disabled]="submittingPayment()">
                            {{ submittingPayment() ? 'Aplicando...' : 'Confirmar pago' }}
                          </button>
                          <button
                            type="button"
                            class="secondary"
                            (click)="registerPaymentCancelled.emit()"
                            [disabled]="submittingPayment()"
                          >
                            Cancelar
                          </button>
                        </div>
                      </form>
                    } @else if (!canRegisterPayment(detail.summary)) {
                      <div class="inline-callout neutral">
                        <div>
                          <strong>No disponible para registrar pago</strong>
                          <p>{{ buildRegisterPaymentBlockedMessage(detail) }}</p>
                        </div>
                        @if (canEnsureAccountsReceivable(detail)) {
                          <button
                            type="button"
                            [disabled]="ensuringAccountsReceivable()"
                            (click)="ensureAccountsReceivableRequested.emit(detail.summary.fiscalDocumentId)"
                          >
                            {{ ensuringAccountsReceivable() ? 'Habilitando...' : 'Habilitar cuenta por cobrar' }}
                          </button>
                        }
                      </div>
                    }
                  </article>

                  @if (repActionError()) {
                    <section class="empty-panel">
                      <p class="error">{{ repActionError() }}</p>
                    </section>
                  }

                  <div class="tabs-shell nested">
                    <app-payment-context-tabs
                      [tabs]="paymentTabs"
                      [activeTab]="activePaymentsTab()"
                      [ariaLabel]="'Secciones de pagos y REP'"
                      [idPrefix]="'payment-context-payments'"
                      [compact]="true"
                      (tabChanged)="activePaymentsTab.set(asPaymentsTab($event))"
                    />
                  </div>

                  @if (activePaymentsTab() === 'history') {
                    <article
                      class="panel-card"
                      role="tabpanel"
                      id="payment-context-payments-panel-history"
                      aria-labelledby="payment-context-payments-tab-history"
                    >
                      <div class="panel-header">
                        <div>
                          <h4>Pagos registrados</h4>
                          <p class="helper">Los pagos elegibles pueden agruparse para preparar un solo REP cuando todavía no están ligados a un complemento.</p>
                        </div>
                        <div class="inline-actions">
                          @if (groupedPaymentSelectionCount() > 0) {
                            <span class="helper">{{ groupedPaymentSelectionCount() }} pago(s) seleccionado(s)</span>
                          }
                          <button
                            type="button"
                            class="secondary small"
                            [disabled]="groupedPaymentSelectionCount() < 2 || preparingComplement() !== null || stampingComplement() !== null"
                            (click)="prepareSelectedPaymentComplementRequested.emit()"
                          >
                            {{ preparingComplement() === groupedPrepareToken() ? 'Preparando...' : 'Preparar REP agrupado' }}
                          </button>
                          <button
                            type="button"
                            class="secondary small"
                            [disabled]="!groupedPaymentSelectionCount() || preparingComplement() !== null || stampingComplement() !== null"
                            (click)="groupedPaymentSelectionCleared.emit()"
                          >
                            Limpiar seleccion
                          </button>
                        </div>
                      </div>

                      @if (!detail.paymentHistory.length) {
                        <p class="helper">Todavía no hay pagos registrados relacionados con este CFDI dentro del sistema.</p>
                      } @else {
                        <div class="table-wrap">
                          <table class="compact-table">
                            <thead>
                              <tr>
                                <th>Agrupar</th>
                                <th>PaymentId</th>
                                <th>Pago</th>
                                <th>Importes</th>
                                <th>REP</th>
                                <th>Accion</th>
                                <th>Detalle</th>
                              </tr>
                            </thead>
                            <tbody>
                              @for (payment of detail.paymentHistory; track payment.accountsReceivablePaymentId + '-' + payment.createdAtUtc) {
                                <tr>
                                  <td>
                                    @if (canPrepareComplement(payment)) {
                                      <input
                                        type="checkbox"
                                        [checked]="isGroupedPaymentSelected(payment.accountsReceivablePaymentId)"
                                        [disabled]="preparingComplement() !== null || stampingComplement() !== null"
                                        (change)="emitGroupedPaymentToggle(payment.accountsReceivablePaymentId, $any($event.target).checked)"
                                      />
                                    } @else {
                                      <span class="helper">—</span>
                                    }
                                  </td>
                                  <td class="mono">#{{ payment.accountsReceivablePaymentId }}</td>
                                  <td>
                                    <div class="stacked-cell">
                                      <strong>{{ formatUtc(payment.paymentDateUtc) }}</strong>
                                      <span>{{ payment.paymentFormSat }}</span>
                                    </div>
                                  </td>
                                  <td>
                                    <div class="stacked-cell">
                                      <span>Monto {{ payment.paymentAmount | number: '1.2-2' }}</span>
                                      <span>Aplicado {{ payment.amountAppliedToDocument | number: '1.2-2' }}</span>
                                      <span>Remanente {{ payment.remainingPaymentAmount | number: '1.2-2' }}</span>
                                    </div>
                                  </td>
                                  <td>
                                    <div class="stacked-cell">
                                      <span>{{ payment.paymentComplementUuid || (payment.paymentComplementId ? '#' + payment.paymentComplementId : '—') }}</span>
                                      <span>{{ payment.paymentComplementStatus ? getDisplayLabel(payment.paymentComplementStatus) : 'Sin REP ligado' }}</span>
                                    </div>
                                  </td>
                                  <td>
                                    @if (canPrepareComplement(payment)) {
                                      <button
                                        type="button"
                                        class="small"
                                        [disabled]="preparingComplement() !== null || stampingComplement() !== null"
                                        (click)="preparePaymentComplementRequested.emit(payment)"
                                      >
                                        {{ preparingComplement() === payment.accountsReceivablePaymentId ? 'Preparando...' : 'Preparar REP' }}
                                      </button>
                                    } @else {
                                      <span class="helper">—</span>
                                    }
                                  </td>
                                  <td class="detail-cell">
                                    <details>
                                      <summary>Ver mas</summary>
                                      <app-payment-context-field-grid
                                        [fields]="paymentHistoryDetailFields(payment)"
                                        [minColumnWidth]="140"
                                      />
                                    </details>
                                  </td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      }
                    </article>
                  }

                  @if (activePaymentsTab() === 'applications') {
                    <article
                      class="panel-card"
                      role="tabpanel"
                      id="payment-context-payments-panel-applications"
                      aria-labelledby="payment-context-payments-tab-applications"
                    >
                      <div class="panel-header">
                        <div>
                          <h4>Aplicaciones de pago</h4>
                          <p class="helper">Se priorizan parcialidad, importes aplicados y variacion de saldo. Los detalles adicionales quedan por fila.</p>
                        </div>
                      </div>

                      @if (!detail.paymentApplications.length) {
                        <p class="helper">Todavía no hay pagos aplicados a este CFDI dentro del sistema.</p>
                      } @else {
                        <div class="table-wrap">
                          <table class="compact-table">
                            <thead>
                              <tr>
                                <th>PaymentId</th>
                                <th>Pago</th>
                                <th>Parcialidad</th>
                                <th>Importes</th>
                                <th>Saldos</th>
                                <th>Detalle</th>
                              </tr>
                            </thead>
                            <tbody>
                              @for (application of detail.paymentApplications; track application.accountsReceivablePaymentId + '-' + application.applicationSequence) {
                                <tr>
                                  <td class="mono">#{{ application.accountsReceivablePaymentId }}</td>
                                  <td>
                                    <div class="stacked-cell">
                                      <strong>{{ formatUtc(application.paymentDateUtc) }}</strong>
                                      <span>{{ application.paymentFormSat }}</span>
                                    </div>
                                  </td>
                                  <td>{{ application.applicationSequence }}</td>
                                  <td>
                                    <div class="stacked-cell">
                                      <span>Monto {{ application.paymentAmount | number: '1.2-2' }}</span>
                                      <span>Aplicado {{ application.appliedAmount | number: '1.2-2' }}</span>
                                      <span>Remanente {{ application.remainingPaymentAmount | number: '1.2-2' }}</span>
                                    </div>
                                  </td>
                                  <td>
                                    <div class="stacked-cell">
                                      <span>Anterior {{ application.previousBalance | number: '1.2-2' }}</span>
                                      <span>Nuevo {{ application.newBalance | number: '1.2-2' }}</span>
                                    </div>
                                  </td>
                                  <td class="detail-cell">
                                    <details>
                                      <summary>Ver mas</summary>
                                      <app-payment-context-field-grid
                                        [fields]="paymentApplicationDetailFields(application)"
                                        [minColumnWidth]="140"
                                      />
                                    </details>
                                  </td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      }
                    </article>
                  }

                  @if (activePaymentsTab() === 'reps') {
                    <article
                      class="panel-card"
                      role="tabpanel"
                      id="payment-context-payments-panel-reps"
                      aria-labelledby="payment-context-payments-tab-reps"
                    >
                      <div class="panel-header">
                        <div>
                          <h4>REP emitidos y relacionados</h4>
                          <p class="helper">Se destacan estado, proveedor, fechas operativas y montos. La trazabilidad tecnica adicional queda en el detalle por fila.</p>
                        </div>
                      </div>

                      @if (!detail.issuedReps.length) {
                        <p class="helper">Aún no hay REP ligados a este CFDI base.</p>
                      } @else {
                        <div class="table-wrap">
                          <table class="compact-table">
                            <thead>
                              <tr>
                                <th>Complemento</th>
                                <th>PaymentId</th>
                                <th>Estado</th>
                                <th>Parcialidad</th>
                                <th>Fechas</th>
                                <th>Importes</th>
                                <th>Accion</th>
                                <th>Detalle</th>
                              </tr>
                            </thead>
                            <tbody>
                              @for (complement of detail.issuedReps; track complement.paymentComplementId) {
                                <tr>
                                  <td class="mono">#{{ complement.paymentComplementId }}</td>
                                  <td class="mono">#{{ complement.accountsReceivablePaymentId }}</td>
                                  <td>
                                    <div class="stacked-cell">
                                      <strong>{{ getDisplayLabel(complement.status) }}</strong>
                                      <span>{{ complement.providerName || '—' }}</span>
                                    </div>
                                  </td>
                                  <td>{{ complement.installmentNumber }}</td>
                                  <td>
                                    <div class="stacked-cell">
                                      <span>Pago {{ formatUtc(complement.paymentDateUtc) }}</span>
                                      <span>Emision {{ formatOptionalUtc(complement.issuedAtUtc) }}</span>
                                    </div>
                                  </td>
                                  <td>
                                    <div class="stacked-cell">
                                      <span>Monto {{ complement.paidAmount | number: '1.2-2' }}</span>
                                      <span>Saldo ant. {{ complement.previousBalance | number: '1.2-2' }}</span>
                                      <span>Saldo rem. {{ complement.remainingBalance | number: '1.2-2' }}</span>
                                    </div>
                                  </td>
                                  <td>
                                    <div class="table-actions">
                                      @if (canStampComplement(complement)) {
                                        <button
                                          type="button"
                                          class="small"
                                          [disabled]="repActionDisabled()"
                                          (click)="stampPaymentComplementRequested.emit(complement)"
                                        >
                                          {{
                                            stampingComplement() === complement.paymentComplementId
                                              ? (complement.status === 'StampingRejected' ? 'Reintentando...' : 'Timbrando...')
                                              : (complement.status === 'StampingRejected' ? 'Reintentar timbrado' : 'Timbrar REP')
                                          }}
                                        </button>
                                      }
                                      <button
                                        type="button"
                                        class="secondary small"
                                        [disabled]="repActionDisabled() || !canViewComplementStamp(complement)"
                                        [title]="detailButtonTitle(complement)"
                                        (click)="viewPaymentComplementStampRequested.emit(complement)"
                                      >
                                        {{ repUtilityActionLabel(complement, 'detail', 'Detalle', 'Cargando...') }}
                                      </button>
                                      <button
                                        type="button"
                                        class="secondary small"
                                        [disabled]="repActionDisabled() || !canDownloadComplementXml(complement)"
                                        [title]="xmlButtonTitle(complement)"
                                        (click)="downloadPaymentComplementXmlRequested.emit(complement)"
                                      >
                                        {{ repUtilityActionLabel(complement, 'xml', 'XML', 'Descargando...') }}
                                      </button>
                                      <button
                                        type="button"
                                        class="secondary small"
                                        [disabled]="repActionDisabled() || !canDownloadComplementPdf(complement)"
                                        [title]="pdfButtonTitle(complement)"
                                        (click)="downloadPaymentComplementPdfRequested.emit(complement)"
                                      >
                                        {{ repUtilityActionLabel(complement, 'pdf', 'PDF', 'Descargando...') }}
                                      </button>
                                      <button
                                        type="button"
                                        class="secondary small"
                                        [disabled]="repActionDisabled() || !canEmailComplement(complement)"
                                        [title]="emailButtonTitle(complement)"
                                        (click)="emailPaymentComplementRequested.emit(complement)"
                                      >
                                        {{ repUtilityActionLabel(complement, 'email', 'Correo', 'Cargando...') }}
                                      </button>
                                      @if (canRefreshComplement(complement)) {
                                        <button
                                          type="button"
                                          class="secondary small"
                                          [disabled]="repActionDisabled()"
                                          (click)="refreshPaymentComplementRequested.emit(complement)"
                                        >
                                          {{ refreshingComplement() === complement.paymentComplementId ? 'Refrescando...' : 'Refrescar' }}
                                        </button>
                                      }
                                      @if (canCancelComplement(complement)) {
                                        <button
                                          type="button"
                                          class="secondary small"
                                          [disabled]="repActionDisabled()"
                                          (click)="cancelPaymentComplementRequested.emit(complement)"
                                        >
                                          {{ cancellingComplement() === complement.paymentComplementId ? 'Cancelando...' : 'Cancelar' }}
                                        </button>
                                      }
                                      @if (!canStampComplement(complement) && !canRefreshComplement(complement) && !canCancelComplement(complement) && !hasRepUtilityActions(complement)) {
                                        <span class="helper">—</span>
                                      }
                                    </div>
                                  </td>
                                  <td class="detail-cell">
                                    <details>
                                      <summary>Ver mas</summary>
                                      <app-payment-context-field-grid
                                        [fields]="issuedRepDetailFields(complement)"
                                        [minColumnWidth]="140"
                                      />
                                    </details>
                                  </td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      }
                    </article>
                  }
                </section>
              }

              @if (activeMainTab() === 'timeline') {
                <section
                  class="tab-panel"
                  role="tabpanel"
                  id="payment-context-main-panel-timeline"
                  aria-labelledby="payment-context-main-tab-timeline"
                >
                  <article class="panel-card">
                    <div class="panel-header">
                      <div>
                        <h4>Timeline operativo</h4>
                        <p class="helper">Se conserva el orden cronológico existente del flujo REP. Cada evento muestra su fuente, referencia y metadatos relevantes.</p>
                      </div>
                    </div>

                    @if (!(detail.timeline ?? []).length) {
                      <p class="helper">Todavía no hay eventos cronológicos suficientes para este CFDI.</p>
                    } @else {
                      <div class="timeline-list">
                        @for (event of detail.timeline ?? []; track event.eventType + '-' + event.occurredAtUtc + '-' + (event.referenceId ?? 0)) {
                          <article class="timeline-item">
                            <header>
                              <div>
                                <strong>{{ event.title }}</strong>
                                <div class="timeline-badges">
                                  @if (event.severity) {
                                    <app-status-badge [label]="event.severity" [tone]="alertTone(event.severity)" />
                                  }
                                  @if (event.status) {
                                    <span class="timeline-chip">{{ getDisplayLabel(event.status) }}</span>
                                  }
                                  <span class="timeline-chip">{{ getDisplayLabel(event.eventType) }}</span>
                                </div>
                              </div>
                              <span class="item-meta">{{ formatUtc(event.occurredAtUtc) }}</span>
                            </header>
                            <p>{{ event.description }}</p>
                            <small>
                              Fuente {{ getDisplayLabel(event.sourceType) }}
                              @if (event.referenceId) {
                                · Ref #{{ event.referenceId }}
                              }
                              @if (event.referenceUuid) {
                                · UUID {{ event.referenceUuid }}
                              }
                            </small>
                            @if (timelineMetadataEntries(event).length) {
                              <div class="timeline-meta-list">
                                @for (entry of timelineMetadataEntries(event); track entry) {
                                  <span class="timeline-chip">{{ entry }}</span>
                                }
                              </div>
                            }
                          </article>
                        }
                      </div>
                    }
                  </article>
                </section>
              }

              @if (activeMainTab() === 'technical') {
                <section
                  class="tab-panel"
                  role="tabpanel"
                  id="payment-context-main-panel-technical"
                  aria-labelledby="payment-context-main-tab-technical"
                >
                  <div class="panel-grid">
                    <article class="panel-card">
                      <div class="panel-header">
                        <h4>Datos tecnicos del CFDI</h4>
                      </div>
                      <app-payment-context-field-grid [fields]="technicalDocumentFields()" [minColumnWidth]="160" />
                    </article>

                    <article class="panel-card">
                      <div class="panel-header">
                        <h4>Snapshot operativo persistido</h4>
                      </div>
                      @if (operationalState()) {
                        <app-payment-context-field-grid [fields]="technicalOperationalFields()" [minColumnWidth]="160" />
                      } @else {
                        <p class="helper">Todavía no existe snapshot operativo persistido para este CFDI.</p>
                      }
                    </article>
                  </div>

                  <article class="panel-card">
                    <div class="panel-header">
                      <h4>Trazabilidad de pagos</h4>
                    </div>
                    @if (!detail.paymentHistory.length) {
                      <p class="helper">Sin registros tecnicos de pagos para este CFDI.</p>
                    } @else {
                      <div class="technical-record-list">
                        @for (payment of detail.paymentHistory; track payment.accountsReceivablePaymentId + '-' + payment.createdAtUtc) {
                          <article class="technical-record">
                            <div class="card-header">
                              <h5>Pago #{{ payment.accountsReceivablePaymentId }}</h5>
                              <span class="mini-pill">{{ payment.paymentComplementId ? 'REP ligado' : 'Sin REP' }}</span>
                            </div>
                            <app-payment-context-field-grid [fields]="paymentHistoryTechnicalFields(payment)" [minColumnWidth]="150" />
                          </article>
                        }
                      </div>
                    }
                  </article>

                  <article class="panel-card">
                    <div class="panel-header">
                      <h4>Trazabilidad de aplicaciones</h4>
                    </div>
                    @if (!detail.paymentApplications.length) {
                      <p class="helper">Sin aplicaciones tecnicas registradas.</p>
                    } @else {
                      <div class="technical-record-list">
                        @for (application of detail.paymentApplications; track application.accountsReceivablePaymentId + '-' + application.applicationSequence) {
                          <article class="technical-record">
                            <div class="card-header">
                              <h5>Aplicacion #{{ application.applicationSequence }}</h5>
                              <span class="mini-pill">Pago #{{ application.accountsReceivablePaymentId }}</span>
                            </div>
                            <app-payment-context-field-grid [fields]="paymentApplicationTechnicalFields(application)" [minColumnWidth]="150" />
                          </article>
                        }
                      </div>
                    }
                  </article>

                  <article class="panel-card">
                    <div class="panel-header">
                      <h4>Trazabilidad de REP</h4>
                    </div>
                    @if (!detail.issuedReps.length) {
                      <p class="helper">Sin REP tecnicos relacionados todavía.</p>
                    } @else {
                      <div class="technical-record-list">
                        @for (complement of detail.issuedReps; track complement.paymentComplementId) {
                          <article class="technical-record">
                            <div class="card-header">
                              <h5>REP #{{ complement.paymentComplementId }}</h5>
                              <app-status-badge [label]="complement.status" [tone]="complementTone(complement.status)" />
                            </div>
                            <app-payment-context-field-grid [fields]="issuedRepTechnicalFields(complement)" [minColumnWidth]="150" />
                          </article>
                        }
                      </div>
                    }
                  </article>
                </section>
              }
            }
          </div>
        </section>
      </section>
    }
  `,
  styles: [
    `
      .modal-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(24, 37, 51, 0.42);
        display: grid;
        place-items: center;
        padding: 1rem;
        z-index: 50;
      }

      .modal-shell {
        width: min(1220px, 100%);
        max-height: calc(100vh - 2rem);
        overflow: hidden;
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        background: #fff;
        box-shadow: 0 24px 60px rgba(24, 37, 51, 0.24);
        display: flex;
        flex-direction: column;
      }

      .modal-header {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: flex-start;
        padding: 1rem 1rem 0.95rem;
        border-bottom: 1px solid #ece5d7;
        background: linear-gradient(180deg, #fbf8f1 0%, #fff 100%);
        flex-shrink: 0;
      }

      .header-copy {
        display: grid;
        gap: 0.75rem;
      }

      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 0.72rem;
        color: #8a6a32;
      }

      h3,
      h4,
      h5 {
        margin: 0;
      }

      .header-chips,
      .header-actions,
      .priority-chip-row,
      .inline-actions,
      .timeline-badges,
      .timeline-meta-list {
        display: flex;
        flex-wrap: wrap;
        gap: 0.55rem;
        align-items: center;
      }

      .metric-chip,
      .mini-pill,
      .priority-chip,
      .timeline-chip {
        display: inline-flex;
        align-items: center;
        border-radius: 999px;
        padding: 0.28rem 0.7rem;
        font-size: 0.78rem;
        font-weight: 600;
      }

      .metric-chip,
      .mini-pill,
      .timeline-chip {
        background: #f2efe7;
        color: #5a4d35;
        border: 1px solid #ddd2bc;
      }

      .priority-chip.success,
      .priority-flag.success,
      .alert-emphasis.success,
      .alert-item.success {
        background: #eefbf1;
        color: #215b31;
        border-color: #a8dcb3;
      }

      .priority-chip.info,
      .priority-flag.info,
      .alert-emphasis.info,
      .alert-item.info {
        background: #e5ebff;
        color: #24498a;
        border-color: #c8d8ff;
      }

      .priority-chip.warning,
      .priority-flag.warning,
      .alert-emphasis.warning,
      .alert-item.warning {
        background: #fff7e8;
        color: #725000;
        border-color: #efd18c;
      }

      .priority-chip.danger,
      .priority-flag.danger,
      .alert-emphasis.danger,
      .alert-item.danger {
        background: #fff0f0;
        color: #7a2020;
        border-color: #ebb1b1;
      }

      .modal-body {
        overflow: auto;
        padding: 1rem;
        display: grid;
        gap: 1rem;
      }

      .executive-grid,
      .panel-grid,
      .status-card-grid {
        display: grid;
        gap: 1rem;
      }

      .executive-grid {
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      }

      .panel-grid {
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      }

      .status-card-grid {
        grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
        margin-bottom: 0.85rem;
      }

      .executive-card,
      .panel-card,
      .empty-panel,
      .technical-record {
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        background: #fff;
      }

      .executive-card,
      .panel-card,
      .technical-record {
        padding: 0.95rem 1rem;
      }

      .executive-card {
        background: #fcfaf4;
      }

      .executive-card-alerts {
        gap: 0.65rem;
        display: grid;
      }

      .card-header,
      .panel-header {
        display: flex;
        justify-content: space-between;
        gap: 0.75rem;
        align-items: flex-start;
        margin-bottom: 0.85rem;
      }

      .panel-header p,
      .card-header p,
      .helper,
      .error,
      .primary-reason,
      .alert-item p,
      .alert-emphasis p,
      .rule-item p,
      .timeline-item p,
      .timeline-item small,
      .inline-callout p {
        margin: 0;
      }

      .helper {
        color: #5f6b76;
      }

      .error {
        color: #7a2020;
      }

      .tabs-shell {
        position: sticky;
        top: 0;
        z-index: 1;
        background: linear-gradient(180deg, #fff 75%, rgba(255, 255, 255, 0.92) 100%);
        padding-bottom: 0.2rem;
      }

      .tabs-shell.nested {
        position: static;
        padding-bottom: 0;
      }

      .tab-panel {
        display: grid;
        gap: 1rem;
      }

      .priority-flag-list,
      .alert-list,
      .alert-emphasis-list,
      .rule-list,
      .timeline-list,
      .technical-record-list {
        display: grid;
        gap: 0.75rem;
      }

      .priority-flag,
      .alert-item,
      .alert-emphasis,
      .rule-item,
      .timeline-item,
      .inline-callout {
        border: 1px solid #ece5d7;
        border-radius: 0.9rem;
      }

      .priority-flag {
        display: grid;
        gap: 0.25rem;
        padding: 0.7rem 0.8rem;
      }

      .priority-flag span {
        font-size: 0.88rem;
      }

      .alert-list,
      .rule-list {
        list-style: none;
        padding: 0;
      }

      .alert-item,
      .alert-emphasis {
        padding: 0.8rem 0.9rem;
        background: #fff;
      }

      .alert-emphasis {
        display: grid;
        gap: 0.25rem;
      }

      .item-header {
        display: flex;
        justify-content: space-between;
        gap: 0.75rem;
        align-items: center;
        margin-bottom: 0.35rem;
      }

      .item-meta {
        color: #5f6b76;
        font-size: 0.82rem;
      }

      .primary-reason {
        font-size: 1rem;
        font-weight: 600;
        color: #182533;
        margin-bottom: 0.9rem;
      }

      .rule-item {
        display: grid;
        grid-template-columns: auto 1fr;
        gap: 0.75rem;
        align-items: flex-start;
        padding: 0.8rem 0.9rem;
        background: #fcfaf4;
      }

      .rule-disclosure summary {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 0.75rem;
        cursor: pointer;
        font-weight: 600;
        list-style: none;
      }

      .rule-disclosure summary::-webkit-details-marker {
        display: none;
      }

      .rule-disclosure[open] summary {
        margin-bottom: 0.85rem;
      }

      .inline-callout {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: center;
        padding: 0.85rem 0.95rem;
        margin-top: 0.95rem;
      }

      .inline-callout.info {
        background: #eef5ff;
      }

      .inline-callout.neutral {
        background: #f7f4ed;
      }

      .status-card {
        display: grid;
        gap: 0.25rem;
        border: 1px solid #ece5d7;
        border-radius: 0.9rem;
        padding: 0.8rem 0.9rem;
        background: #fcfaf4;
      }

      .status-card-label {
        color: #6b7784;
        font-size: 0.78rem;
      }

      .status-card strong {
        color: #182533;
      }

      .status-card small {
        color: #5f6b76;
      }

      .payment-form {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: 1rem;
        align-items: end;
        margin-top: 0.95rem;
      }

      .wide {
        grid-column: 1 / -1;
      }

      label {
        display: grid;
        gap: 0.35rem;
      }

      input,
      textarea,
      button {
        font: inherit;
      }

      input,
      textarea {
        border: 1px solid #c9d1da;
        border-radius: 0.8rem;
        padding: 0.75rem 0.9rem;
      }

      textarea {
        resize: vertical;
      }

      button {
        border: none;
        border-radius: 0.8rem;
        padding: 0.75rem 1rem;
        background: #182533;
        color: #fff;
        cursor: pointer;
      }

      button.secondary {
        background: #d8c49b;
        color: #182533;
      }

      button.small {
        padding: 0.45rem 0.7rem;
        font-size: 0.88rem;
      }

      button:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }

      .form-actions,
      .table-actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.65rem;
        align-items: center;
      }

      .table-wrap {
        overflow-x: auto;
      }

      .compact-table {
        width: 100%;
        border-collapse: collapse;
        min-width: 880px;
      }

      .compact-table th,
      .compact-table td {
        text-align: left;
        padding: 0.7rem 0.5rem;
        border-bottom: 1px solid #ece5d7;
        vertical-align: top;
      }

      .mono {
        font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
        font-size: 0.86rem;
      }

      .stacked-cell {
        display: grid;
        gap: 0.2rem;
      }

      .detail-cell details {
        min-width: 180px;
      }

      .detail-cell summary {
        cursor: pointer;
        color: #24498a;
        font-weight: 600;
      }

      .detail-cell app-payment-context-field-grid {
        display: block;
        margin-top: 0.75rem;
      }

      .timeline-item {
        padding: 0.9rem 1rem;
        border-left: 4px solid #8a6a32;
        background: #fcfaf4;
        display: grid;
        gap: 0.5rem;
      }

      .timeline-item header {
        display: flex;
        justify-content: space-between;
        gap: 0.8rem;
        align-items: flex-start;
      }

      .technical-record-list {
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      }

      .technical-record {
        display: grid;
        gap: 0.85rem;
        background: #fcfaf4;
      }

      .empty-panel {
        padding: 1rem;
      }

      @media (max-width: 860px) {
        .modal-header,
        .card-header,
        .panel-header,
        .inline-callout,
        .timeline-item header {
          flex-direction: column;
          align-items: stretch;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentContextModalComponent {
  readonly open = input(false);
  readonly detail = input<InternalRepBaseDocumentDetailResponse | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly showRegisterPaymentForm = input(false);
  readonly submittingPayment = input(false);
  readonly ensuringAccountsReceivable = input(false);
  readonly paymentError = input<string | null>(null);
  readonly repActionError = input<string | null>(null);
  readonly preparingComplement = input<number | null>(null);
  readonly stampingComplement = input<number | null>(null);
  readonly refreshingComplement = input<number | null>(null);
  readonly cancellingComplement = input<number | null>(null);
  readonly repUtilityActionKey = input<string | null>(null);
  readonly canSendPaymentComplementEmail = input(true);
  readonly groupedPaymentIds = input<number[]>([]);
  readonly groupedPrepareToken = input(-1);

  readonly paymentDate = input('');
  readonly paymentFormSat = input('');
  readonly paymentAmount = input<number | null>(null);
  readonly paymentReference = input('');
  readonly paymentNotes = input('');

  readonly paymentDateChange = output<string>();
  readonly paymentFormSatChange = output<string>();
  readonly paymentAmountChange = output<number | null>();
  readonly paymentReferenceChange = output<string>();
  readonly paymentNotesChange = output<string>();

  readonly closeRequested = output<void>();
  readonly registerPaymentRequested = output<void>();
  readonly registerPaymentCancelled = output<void>();
  readonly registerPaymentSubmitted = output<void>();
  readonly ensureAccountsReceivableRequested = output<number>();
  readonly groupedPaymentToggled = output<GroupedPaymentToggleEvent>();
  readonly groupedPaymentSelectionCleared = output<void>();
  readonly prepareSelectedPaymentComplementRequested = output<void>();
  readonly preparePaymentComplementRequested = output<InternalRepBaseDocumentPaymentHistoryResponse>();
  readonly stampPaymentComplementRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();
  readonly viewPaymentComplementStampRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();
  readonly downloadPaymentComplementXmlRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();
  readonly downloadPaymentComplementPdfRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();
  readonly emailPaymentComplementRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();
  readonly refreshPaymentComplementRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();
  readonly cancelPaymentComplementRequested = output<InternalRepBaseDocumentPaymentComplementResponse>();

  protected readonly getDisplayLabel = getDisplayLabel;
  protected readonly mainTabs: PaymentContextTabItem[] = [
    { id: 'summary', label: 'Resumen' },
    { id: 'eligibility', label: 'Elegibilidad' },
    { id: 'payments', label: 'Pagos y REP' },
    { id: 'timeline', label: 'Timeline' },
    { id: 'technical', label: 'Datos tecnicos' },
  ];
  protected readonly paymentTabs: PaymentContextTabItem[] = [
    { id: 'history', label: 'Pagos registrados' },
    { id: 'applications', label: 'Aplicaciones' },
    { id: 'reps', label: 'REP relacionados' },
  ];

  protected readonly activeMainTab = signal<MainTabId>('summary');
  protected readonly activePaymentsTab = signal<PaymentsTabId>('history');
  protected readonly summary = computed(() => this.detail()?.summary ?? null);
  protected readonly operationalState = computed(() => this.detail()?.operationalState ?? this.summary()?.operationalState ?? null);
  protected readonly alerts = computed(() => this.summary()?.alerts ?? []);
  protected readonly problematicSignals = computed(() =>
    (this.summary()?.eligibility.secondarySignals ?? []).filter((signal) => signal.severity !== 'Satisfied'),
  );
  protected readonly satisfiedSignals = computed(() =>
    (this.summary()?.eligibility.secondarySignals ?? []).filter((signal) => signal.severity === 'Satisfied'),
  );
  protected readonly priorityFlags = computed(() => {
    const currentSummary = this.summary();
    const flags: PriorityFlag[] = [];

    if (!currentSummary) {
      return flags;
    }

    if (currentSummary.hasRepWithError || this.detail()?.issuedReps.some((item) => item.status === 'StampingRejected')) {
      flags.push({
        label: 'REP rechazado',
        tone: 'danger',
        message: 'Existe un REP rechazado o con error que requiere revision.',
      });
    }

    if (currentSummary.hasAppliedPaymentsWithoutStampedRep) {
      flags.push({
        label: getDisplayLabel('AppliedPaymentsWithoutStampedRep'),
        tone: 'warning',
        message: 'Hay pagos aplicados sin REP timbrado.',
      });
    }

    if (currentSummary.hasPreparedRepPendingStamp || this.operationalState()?.repPendingFlag) {
      flags.push({
        label: 'REP pendiente',
        tone: 'warning',
        message: 'Existe un REP pendiente de timbrar o por revisar.',
      });
    }

    if (currentSummary.outstandingBalance <= 0) {
      flags.push({
        label: 'Sin saldo pendiente',
        tone: 'info',
        message: 'El CFDI ya no tiene saldo pendiente por cobrar.',
      });
    }

    return flags;
  });

  protected readonly executiveCfdiFields = computed(() => {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'Serie/Folio', value: this.buildSeriesFolio(currentSummary) },
      { label: 'UUID', value: this.abbreviate(currentSummary.uuid), title: this.displayText(currentSummary.uuid), mono: true },
      {
        label: 'Receptor',
        value: `${this.displayText(currentSummary.receiverRfc)} · ${this.displayText(currentSummary.receiverLegalName)}`,
        title: `${this.displayText(currentSummary.receiverRfc)} · ${this.displayText(currentSummary.receiverLegalName)}`,
        wide: true,
      },
    ] satisfies PaymentContextFieldItem[];
  });

  protected readonly executiveAmountFields = computed(() => {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'Total', value: this.formatAmount(currentSummary.total) },
      { label: 'Pagado', value: this.formatAmount(currentSummary.paidTotal) },
      { label: 'Saldo', value: this.formatAmount(currentSummary.outstandingBalance) },
    ] satisfies PaymentContextFieldItem[];
  });

  protected readonly executiveRepFields = computed(() => {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'Estado actual', value: getDisplayLabel(currentSummary.repOperationalStatus) },
      { label: 'REP ligados', value: `${currentSummary.paymentComplementCount}` },
      { label: 'REP emitidos', value: `${currentSummary.stampedPaymentComplementCount}` },
      { label: 'REP pendiente', value: currentSummary.hasPreparedRepPendingStamp || this.operationalState()?.repPendingFlag ? 'Si' : 'No' },
    ] satisfies PaymentContextFieldItem[];
  });

  protected readonly executiveEligibilityFields = computed(() => {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'Estado', value: getDisplayLabel(currentSummary.eligibility.status) },
      {
        label: 'Motivo principal',
        value: currentSummary.eligibility.primaryReasonMessage,
        title: currentSummary.eligibility.primaryReasonMessage,
        wide: true,
      },
      { label: 'Codigo', value: currentSummary.eligibility.primaryReasonCode, mono: true },
    ] satisfies PaymentContextFieldItem[];
  });

  constructor() {
    let wasOpen = false;

    effect(() => {
      const isOpen = this.open();
      if (isOpen && !wasOpen) {
        this.activeMainTab.set('summary');
        this.activePaymentsTab.set('history');
      }

      wasOpen = isOpen;
    });

    effect(() => {
      if (this.open() && this.showRegisterPaymentForm()) {
        this.activeMainTab.set('payments');
        this.activePaymentsTab.set('history');
      }
    });
  }

  protected fiscalSummaryFields(): PaymentContextFieldItem[] {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'FiscalDocumentId', value: currentSummary.fiscalDocumentId, mono: true },
      { label: 'BillingDocumentId', value: currentSummary.billingDocumentId, mono: true },
      { label: 'SalesOrderId', value: currentSummary.salesOrderId, mono: true },
      { label: 'UUID', value: this.displayText(currentSummary.uuid), title: this.displayText(currentSummary.uuid), mono: true, wide: true },
      { label: 'Serie/Folio', value: this.buildSeriesFolio(currentSummary) },
      { label: 'Emision', value: this.formatUtc(currentSummary.issuedAtUtc) },
      {
        label: 'Receptor',
        value: `${this.displayText(currentSummary.receiverRfc)} · ${this.displayText(currentSummary.receiverLegalName)}`,
        title: `${this.displayText(currentSummary.receiverRfc)} · ${this.displayText(currentSummary.receiverLegalName)}`,
        wide: true,
      },
      { label: 'Metodo SAT', value: this.displayText(currentSummary.paymentMethodSat) },
      { label: 'Forma SAT', value: this.displayText(currentSummary.paymentFormSat) },
      { label: 'Moneda', value: this.displayText(currentSummary.currencyCode) },
      { label: 'Estado fiscal', value: getDisplayLabel(currentSummary.fiscalStatus) },
    ];
  }

  protected operationalSummaryFields(): PaymentContextFieldItem[] {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'AR Invoice', value: currentSummary.accountsReceivableInvoiceId, mono: true },
      { label: 'Estado REP', value: getDisplayLabel(currentSummary.repOperationalStatus) },
      { label: 'Total', value: this.formatAmount(currentSummary.total) },
      { label: 'Pagado', value: this.formatAmount(currentSummary.paidTotal) },
      { label: 'Saldo', value: this.formatAmount(currentSummary.outstandingBalance) },
      { label: 'Pagos registrados', value: currentSummary.registeredPaymentCount },
      { label: 'REP ligados', value: currentSummary.paymentComplementCount },
      { label: 'REP emitidos', value: currentSummary.stampedPaymentComplementCount },
      { label: 'Ultimo REP', value: this.formatOptionalUtc(currentSummary.lastRepIssuedAtUtc) },
      {
        label: 'Estatus CxC',
        value: currentSummary.accountsReceivableStatus ? getDisplayLabel(currentSummary.accountsReceivableStatus) : '—',
      },
    ];
  }

  protected followUpFields(): PaymentContextFieldItem[] {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'Accion recomendada', value: getDisplayLabel(currentSummary.nextRecommendedAction) },
      { label: 'Operacion bloqueada', value: currentSummary.hasBlockedOperation ? 'Si' : 'No' },
      { label: 'Pago sin REP timbrado', value: currentSummary.hasAppliedPaymentsWithoutStampedRep ? 'Si' : 'No' },
      { label: 'REP pendiente de timbrar', value: currentSummary.hasPreparedRepPendingStamp ? 'Si' : 'No' },
      { label: 'REP con error', value: currentSummary.hasRepWithError ? 'Si' : 'No' },
    ];
  }

  protected eligibilityOverviewFields(): PaymentContextFieldItem[] {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'Estado general', value: getDisplayLabel(currentSummary.eligibility.status) },
      { label: 'Codigo', value: currentSummary.eligibility.primaryReasonCode, mono: true },
      { label: 'Fecha de evaluacion', value: this.formatUtc(currentSummary.eligibility.evaluatedAtUtc) },
    ];
  }

  protected technicalDocumentFields(): PaymentContextFieldItem[] {
    const currentSummary = this.summary();
    if (!currentSummary) {
      return [];
    }

    return [
      { label: 'FiscalDocumentId', value: currentSummary.fiscalDocumentId, mono: true },
      { label: 'BillingDocumentId', value: currentSummary.billingDocumentId, mono: true },
      { label: 'SalesOrderId', value: currentSummary.salesOrderId, mono: true },
      { label: 'AR Invoice', value: currentSummary.accountsReceivableInvoiceId, mono: true },
      { label: 'FiscalStampId', value: currentSummary.fiscalStampId, mono: true },
      { label: 'UUID completo', value: this.displayText(currentSummary.uuid), title: this.displayText(currentSummary.uuid), mono: true, wide: true },
      { label: 'Metodo SAT', value: this.displayText(currentSummary.paymentMethodSat) },
      { label: 'Forma SAT', value: this.displayText(currentSummary.paymentFormSat) },
      { label: 'Moneda', value: this.displayText(currentSummary.currencyCode) },
      { label: 'Elegibilidad', value: getDisplayLabel(currentSummary.eligibility.status) },
      { label: 'Codigo principal', value: currentSummary.eligibility.primaryReasonCode, mono: true },
      { label: 'Evaluado', value: this.formatUtc(currentSummary.eligibility.evaluatedAtUtc) },
      { label: 'Motivo principal', value: currentSummary.eligibility.primaryReasonMessage, wide: true },
    ];
  }

  protected technicalOperationalFields(): PaymentContextFieldItem[] {
    const state = this.operationalState();
    if (!state) {
      return [];
    }

    return [
      { label: 'Ultima evaluacion', value: this.formatUtc(state.lastEligibilityEvaluatedAtUtc) },
      { label: 'Estatus persistido', value: getDisplayLabel(state.lastEligibilityStatus) },
      { label: 'Motivo persistido', value: state.lastPrimaryReasonMessage, wide: true },
      { label: 'Codigo persistido', value: state.lastPrimaryReasonCode, mono: true },
      { label: 'REP pendiente', value: state.repPendingFlag ? 'Si' : 'No' },
      { label: 'Total pagado aplicado', value: this.formatAmount(state.totalPaidApplied) },
      { label: 'Conteo REP', value: state.repCount },
      { label: 'Ultimo REP emitido', value: this.formatOptionalUtc(state.lastRepIssuedAtUtc) },
    ];
  }

  protected paymentHistoryDetailFields(payment: InternalRepBaseDocumentPaymentHistoryResponse): PaymentContextFieldItem[] {
    return [
      { label: 'Referencia', value: payment.reference },
      { label: 'Notas', value: payment.notes, wide: true, subdued: true },
      { label: 'Creado', value: this.formatUtc(payment.createdAtUtc) },
      { label: 'Complemento ligado', value: payment.paymentComplementId ? `#${payment.paymentComplementId}` : '—', mono: true },
      { label: 'UUID REP', value: this.displayText(payment.paymentComplementUuid), mono: true, wide: true },
    ];
  }

  protected paymentApplicationDetailFields(application: InternalRepBaseDocumentPaymentApplicationResponse): PaymentContextFieldItem[] {
    return [
      { label: 'Referencia', value: application.reference },
      { label: 'Notas', value: application.notes, wide: true, subdued: true },
      { label: 'Creado', value: this.formatUtc(application.createdAtUtc) },
    ];
  }

  protected issuedRepDetailFields(complement: InternalRepBaseDocumentPaymentComplementResponse): PaymentContextFieldItem[] {
    return [
      { label: 'UUID', value: this.displayText(complement.uuid), mono: true, wide: true },
      { label: 'Timbrado', value: this.formatOptionalUtc(complement.stampedAtUtc) },
      { label: 'Cancelacion', value: this.formatOptionalUtc(complement.cancelledAtUtc) },
    ];
  }

  protected paymentHistoryTechnicalFields(payment: InternalRepBaseDocumentPaymentHistoryResponse): PaymentContextFieldItem[] {
    return [
      { label: 'PaymentId', value: payment.accountsReceivablePaymentId, mono: true },
      { label: 'Fecha', value: this.formatUtc(payment.paymentDateUtc) },
      { label: 'Forma', value: payment.paymentFormSat },
      { label: 'Monto', value: this.formatAmount(payment.paymentAmount) },
      { label: 'Aplicado al CFDI', value: this.formatAmount(payment.amountAppliedToDocument) },
      { label: 'Remanente', value: this.formatAmount(payment.remainingPaymentAmount) },
      { label: 'Referencia', value: payment.reference },
      { label: 'Notas', value: payment.notes, wide: true, subdued: true },
      { label: 'REP ligado', value: payment.paymentComplementId ? `#${payment.paymentComplementId}` : '—', mono: true },
      { label: 'UUID REP', value: this.displayText(payment.paymentComplementUuid), mono: true, wide: true },
      { label: 'Estatus REP', value: payment.paymentComplementStatus ? getDisplayLabel(payment.paymentComplementStatus) : '—' },
      { label: 'Creado', value: this.formatUtc(payment.createdAtUtc) },
    ];
  }

  protected paymentApplicationTechnicalFields(application: InternalRepBaseDocumentPaymentApplicationResponse): PaymentContextFieldItem[] {
    return [
      { label: 'PaymentId', value: application.accountsReceivablePaymentId, mono: true },
      { label: 'Fecha', value: this.formatUtc(application.paymentDateUtc) },
      { label: 'Forma', value: application.paymentFormSat },
      { label: 'Parcialidad', value: application.applicationSequence },
      { label: 'Monto pago', value: this.formatAmount(application.paymentAmount) },
      { label: 'Aplicado', value: this.formatAmount(application.appliedAmount) },
      { label: 'Saldo anterior', value: this.formatAmount(application.previousBalance) },
      { label: 'Saldo nuevo', value: this.formatAmount(application.newBalance) },
      { label: 'Remanente', value: this.formatAmount(application.remainingPaymentAmount) },
      { label: 'Referencia', value: application.reference },
      { label: 'Notas', value: application.notes, wide: true, subdued: true },
      { label: 'Creado', value: this.formatUtc(application.createdAtUtc) },
    ];
  }

  protected issuedRepTechnicalFields(complement: InternalRepBaseDocumentPaymentComplementResponse): PaymentContextFieldItem[] {
    return [
      { label: 'Complemento', value: complement.paymentComplementId, mono: true },
      { label: 'PaymentId', value: complement.accountsReceivablePaymentId, mono: true },
      { label: 'Estado', value: getDisplayLabel(complement.status) },
      { label: 'UUID', value: this.displayText(complement.uuid), mono: true, wide: true },
      { label: 'Proveedor', value: complement.providerName },
      { label: 'Parcialidad', value: complement.installmentNumber },
      { label: 'Fecha pago', value: this.formatUtc(complement.paymentDateUtc) },
      { label: 'Emision', value: this.formatOptionalUtc(complement.issuedAtUtc) },
      { label: 'Timbrado', value: this.formatOptionalUtc(complement.stampedAtUtc) },
      { label: 'Cancelacion', value: this.formatOptionalUtc(complement.cancelledAtUtc) },
      { label: 'Saldo anterior', value: this.formatAmount(complement.previousBalance) },
      { label: 'Monto', value: this.formatAmount(complement.paidAmount) },
      { label: 'Saldo remanente', value: this.formatAmount(complement.remainingBalance) },
    ];
  }

  protected timelineMetadataEntries(item: RepBaseDocumentTimelineEntryResponse): string[] {
    return Object.entries(item.metadata ?? {})
      .filter(([, value]) => value)
      .map(([key, value]) => `${key}: ${value}`);
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

  protected canRegisterPayment(summary: InternalRepBaseDocumentItemResponse | null): boolean {
    return Boolean(summary?.isEligible && (summary.outstandingBalance ?? 0) > 0);
  }

  protected canPrepareComplement(payment: InternalRepBaseDocumentPaymentHistoryResponse): boolean {
    return !payment.paymentComplementId && payment.amountAppliedToDocument > 0;
  }

  protected canStampComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.status === 'ReadyForStamping' || complement.status === 'StampingRejected';
  }

  protected canViewComplementStamp(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    if (!complement.paymentComplementId) {
      return false;
    }

    return Boolean(
      complement.uuid ||
      complement.stampedAtUtc ||
      complement.cancelledAtUtc ||
      complement.status === 'StampingRejected',
    );
  }

  protected canDownloadComplementXml(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    if (complement.canDownloadXml === true) {
      return true;
    }

    if (complement.canDownloadXml === false) {
      return false;
    }

    return this.canViewComplementStamp(complement) && this.isStampedOrCancelled(complement);
  }

  protected canDownloadComplementPdf(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.canDownloadPdf === true || complement.canGeneratePdf === true;
  }

  protected canEmailComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.canEmail === true && this.canSendPaymentComplementEmail();
  }

  protected canRefreshComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return ['Stamped', 'CancellationRequested', 'CancellationRejected', 'Cancelled'].includes(complement.status);
  }

  protected canCancelComplement(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.status === 'Stamped' || complement.status === 'CancellationRejected';
  }

  protected hasRepUtilityActions(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return (
      this.canViewComplementStamp(complement) ||
      this.canDownloadComplementXml(complement) ||
      this.canDownloadComplementPdf(complement) ||
      complement.canEmail === true
    );
  }

  protected repActionDisabled(): boolean {
    return (
      this.preparingComplement() !== null ||
      this.stampingComplement() !== null ||
      this.refreshingComplement() !== null ||
      this.cancellingComplement() !== null ||
      this.repUtilityActionKey() !== null
    );
  }

  protected repUtilityActionLabel(
    complement: InternalRepBaseDocumentPaymentComplementResponse,
    action: 'detail' | 'xml' | 'pdf' | 'email',
    idleLabel: string,
    busyLabel: string,
  ): string {
    return this.repUtilityActionInProgress(complement, action) ? busyLabel : idleLabel;
  }

  protected detailButtonTitle(complement: InternalRepBaseDocumentPaymentComplementResponse): string {
    return this.canViewComplementStamp(complement)
      ? 'Ver detalle del timbrado REP.'
      : 'Detalle de timbrado no disponible para este REP.';
  }

  protected xmlButtonTitle(complement: InternalRepBaseDocumentPaymentComplementResponse): string {
    return this.canDownloadComplementXml(complement)
      ? 'Descargar XML del REP.'
      : 'XML no disponible para este REP.';
  }

  protected pdfButtonTitle(complement: InternalRepBaseDocumentPaymentComplementResponse): string {
    return this.canDownloadComplementPdf(complement)
      ? 'Descargar PDF del REP.'
      : 'PDF no disponible para este REP.';
  }

  protected emailButtonTitle(complement: InternalRepBaseDocumentPaymentComplementResponse): string {
    if (complement.canEmail !== true) {
      return 'El REP no está listo para envío.';
    }

    return this.canSendPaymentComplementEmail()
      ? 'Enviar el REP por correo al cliente.'
      : 'No tienes permisos para enviar este complemento por correo.';
  }

  protected isGroupedPaymentSelected(accountsReceivablePaymentId: number): boolean {
    return this.groupedPaymentIds().includes(accountsReceivablePaymentId);
  }

  protected groupedPaymentSelectionCount(): number {
    return this.groupedPaymentIds().length;
  }

  protected emitGroupedPaymentToggle(accountsReceivablePaymentId: number, checked: boolean): void {
    this.groupedPaymentToggled.emit({ accountsReceivablePaymentId, checked });
  }

  protected repStatusTone(summary: InternalRepBaseDocumentItemResponse): StatusBadgeTone {
    if (summary.hasRepWithError || summary.isBlocked) {
      return 'danger';
    }

    if (summary.isEligible) {
      return 'success';
    }

    if (summary.hasPreparedRepPendingStamp || summary.hasAppliedPaymentsWithoutStampedRep) {
      return 'warning';
    }

    return 'neutral';
  }

  protected fiscalStatusTone(status: string): StatusBadgeTone {
    if (status === 'Stamped') {
      return 'success';
    }

    if (status === 'Cancelled') {
      return 'danger';
    }

    if (status === 'Pending') {
      return 'warning';
    }

    return 'neutral';
  }

  protected actionTone(action?: string | null): StatusBadgeTone {
    switch (action) {
      case 'StampRep':
      case 'PrepareRep':
      case 'RegisterPayment':
      case 'RefreshRepStatus':
        return 'warning';
      case 'Blocked':
      case 'CancelRep':
        return 'danger';
      case 'NoAction':
        return 'neutral';
      default:
        return 'info';
    }
  }

  protected eligibilityTone(status?: string | null): StatusBadgeTone {
    switch (status) {
      case 'Eligible':
        return 'success';
      case 'Blocked':
      case 'Failed':
        return 'danger';
      case 'Missing':
      case 'Warning':
      case 'Ineligible':
        return 'warning';
      default:
        return 'info';
    }
  }

  protected signalTone(severity?: string | null): StatusBadgeTone {
    switch (severity) {
      case 'Satisfied':
        return 'success';
      case 'Failed':
        return 'danger';
      case 'Missing':
      case 'Warning':
        return 'warning';
      default:
        return 'info';
    }
  }

  protected alertTone(severity?: string | null): StatusBadgeTone {
    switch (severity) {
      case 'critical':
      case 'error':
        return 'danger';
      case 'warning':
        return 'warning';
      case 'info':
      default:
        return 'info';
    }
  }

  protected complementTone(status: string): StatusBadgeTone {
    switch (status) {
      case 'Stamped':
        return 'success';
      case 'StampingRejected':
      case 'CancellationRejected':
        return 'danger';
      case 'ReadyForStamping':
      case 'CancellationRequested':
        return 'warning';
      default:
        return 'neutral';
    }
  }

  protected severityToneClass(severity?: string | null): string {
    switch (severity) {
      case 'critical':
      case 'error':
        return 'danger';
      case 'warning':
        return 'warning';
      case 'info':
      default:
        return 'info';
    }
  }

  protected asMainTab(value: string): MainTabId {
    return this.mainTabs.some((tab) => tab.id === value) ? (value as MainTabId) : 'summary';
  }

  protected asPaymentsTab(value: string): PaymentsTabId {
    return this.paymentTabs.some((tab) => tab.id === value) ? (value as PaymentsTabId) : 'history';
  }

  protected formatUtc(value: string): string {
    return new Date(value).toLocaleString('es-MX', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'UTC',
    });
  }

  protected formatOptionalUtc(value?: string | null): string {
    return value ? this.formatUtc(value) : '—';
  }

  protected formatAmount(value: number): string {
    return new Intl.NumberFormat('es-MX', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  protected formatAmountWithCurrency(value: number, currencyCode: string): string {
    return `${this.formatAmount(value)} ${currencyCode}`;
  }

  private repUtilityActionInProgress(
    complement: InternalRepBaseDocumentPaymentComplementResponse,
    action: 'detail' | 'xml' | 'pdf' | 'email',
  ): boolean {
    return this.repUtilityActionKey() === `${action}:${complement.paymentComplementId}`;
  }

  private isStampedOrCancelled(complement: InternalRepBaseDocumentPaymentComplementResponse): boolean {
    return complement.status === 'Stamped' || complement.status === 'Cancelled';
  }

  protected buildSeriesFolio(item: { series?: string | null; folio?: string | null }): string {
    const series = item.series?.trim();
    const folio = item.folio?.trim();

    if (series && folio) {
      return `${series}-${folio}`;
    }

    return series || folio || '—';
  }

  protected abbreviate(value?: string | null): string {
    const currentValue = this.displayText(value);
    if (currentValue === '—' || currentValue.length <= 18) {
      return currentValue;
    }

    return `${currentValue.slice(0, 8)}...${currentValue.slice(-8)}`;
  }

  protected displayText(value?: string | number | null): string {
    if (value === null || value === undefined) {
      return '—';
    }

    if (typeof value === 'number') {
      return Number.isNaN(value) ? '—' : `${value}`;
    }

    const trimmed = value.trim();
    return trimmed ? trimmed : '—';
  }
}
