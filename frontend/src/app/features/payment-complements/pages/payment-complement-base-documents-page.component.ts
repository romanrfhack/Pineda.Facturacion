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
  RegisterInternalRepBaseDocumentPaymentResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-base-documents-page',
  imports: [FormsModule, DecimalPipe],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Complementos de pago</p>
        <h2>Bandeja operativa de CFDI internos para REP</h2>
        <p class="helper">La unidad operativa es el CFDI base. En esta fase la bandeja registra y aplica pagos sobre CFDI internos; el timbrado REP desde esta vista queda para la siguiente fase.</p>
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

          @if (filtersError()) {
            <p class="error wide">{{ filtersError() }}</p>
          }

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>
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
                      <small class="row-reason">{{ item.eligibility.primaryReasonMessage }}</small>
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
              </section>

              <section class="nested-card">
                <div class="section-header">
                  <div>
                    <h4>Registro de pago</h4>
                    <p class="helper">El pago se registra y se aplica completo al CFDI base actual. Este flujo no deja remanente sin aplicar.</p>
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
                <h4>Historial de pagos registrados</h4>
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
                <h4>REP emitidos y relacionados</h4>
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
    .actions, .toolbar, .pagination, .modal-actions, .section-header, .row-actions { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
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
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly items = signal<InternalRepBaseDocumentItemResponse[]>([]);
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

  private async loadDetail(fiscalDocumentId: number, openRegisterPayment = false): Promise<void> {
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.selectedDetail.set(null);
    this.paymentError.set(null);

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
        hasRepEmitted: parseBooleanFilter(this.repEmittedFilter)
      }));

      this.items.set(response.items);
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible cargar la bandeja REP interna.'));
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
