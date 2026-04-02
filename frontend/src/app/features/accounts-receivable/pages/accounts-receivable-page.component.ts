import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivablePaymentResponse,
  AccountsReceivablePortfolioItemResponse,
  AccountsReceivablePaymentSummaryItemResponse,
  ApplyAccountsReceivablePaymentRequest,
  CreateAccountsReceivablePaymentRequest,
  SearchAccountsReceivablePortfolioRequest,
  SearchAccountsReceivablePaymentsRequest
} from '../models/accounts-receivable.models';
import { AccountsReceivableCardComponent } from '../components/accounts-receivable-card.component';
import { PaymentCreateFormComponent } from '../components/payment-create-form.component';
import { PaymentApplicationFormComponent } from '../components/payment-application-form.component';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';

@Component({
  selector: 'app-accounts-receivable-page',
  imports: [RouterLink, FormsModule, CurrencyPipe, DatePipe, AccountsReceivableCardComponent, PaymentCreateFormComponent, PaymentApplicationFormComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Cuentas por cobrar</p>
        <h2>{{ detailMode() ? 'Cuenta por cobrar, pagos y aplicaciones' : 'Cartera operativa mínima' }}</h2>
      </header>

      @if (!detailMode()) {
        <section class="card filters">
          <div class="filter-grid">
            <label>
              <span>Receptor o RFC</span>
              <input type="text" [(ngModel)]="filters.receiverQuery" placeholder="Nombre o RFC" />
            </label>

            <label>
              <span>FiscalReceiverId</span>
              <input type="number" [(ngModel)]="filters.fiscalReceiverIdText" placeholder="Id receptor" />
            </label>

            <label>
              <span>Estatus</span>
              <select [(ngModel)]="filters.status">
                <option value="">Todos</option>
                <option value="Open">Open</option>
                <option value="PartiallyPaid">PartiallyPaid</option>
                <option value="Paid">Paid</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </label>

            <label>
              <span>Vencimiento desde</span>
              <input type="date" [(ngModel)]="filters.dueDateFrom" />
            </label>

            <label>
              <span>Vencimiento hasta</span>
              <input type="date" [(ngModel)]="filters.dueDateTo" />
            </label>

            <label>
              <span>Saldo pendiente</span>
              <select [(ngModel)]="filters.pendingBalance">
                <option value="">Todos</option>
                <option value="true">Solo con saldo</option>
                <option value="false">Solo sin saldo</option>
              </select>
            </label>
          </div>

          <div class="actions">
            <button type="button" (click)="applyPortfolioFilters()" [disabled]="loading()">Filtrar</button>
            <button type="button" class="secondary" (click)="resetPortfolioFilters()" [disabled]="loading()">Limpiar</button>
          </div>
        </section>

        <section class="card">
          <div class="section-head">
            <h3>Cartera</h3>
            <p class="helper">{{ portfolioItems().length }} cuenta(s)</p>
          </div>

          @if (!portfolioItems().length) {
            <p class="helper">No hay cuentas por cobrar con los filtros actuales.</p>
          } @else {
            <div class="table-wrap">
              <table class="portfolio">
                <thead>
                  <tr>
                    <th>Receptor</th>
                    <th>Factura</th>
                    <th>Total</th>
                    <th>Saldo</th>
                    <th>Vencimiento</th>
                    <th>Atraso</th>
                    <th>Estatus</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of portfolioItems(); track item.accountsReceivableInvoiceId) {
                    <tr>
                      <td>
                        <strong>{{ item.receiverLegalName || 'Sin receptor' }}</strong>
                        <div class="subtle">{{ item.receiverRfc || 'Sin RFC' }}</div>
                      </td>
                      <td>
                        <div>{{ formatFiscalLabel(item) }}</div>
                        <div class="subtle">{{ item.fiscalUuid || 'UUID pendiente' }}</div>
                      </td>
                      <td>{{ item.total | currency:'MXN':'symbol':'1.2-2' }}</td>
                      <td>{{ item.outstandingBalance | currency:'MXN':'symbol':'1.2-2' }}</td>
                      <td>{{ item.dueAtUtc ? (item.dueAtUtc | date:'yyyy-MM-dd') : 'Sin vencimiento' }}</td>
                      <td>{{ item.daysPastDue }}</td>
                      <td><span class="badge" [attr.data-status]="item.status">{{ item.status }}</span></td>
                      <td>
                        @if (item.fiscalDocumentId) {
                          <a [routerLink]="['/app/accounts-receivable']" [queryParams]="{ fiscalDocumentId: item.fiscalDocumentId }">Ver detalle</a>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <section class="card filters">
          <div class="section-head compact">
            <h3>Pagos</h3>
            <p class="helper">{{ paymentItems().length }} pago(s)</p>
          </div>

          <div class="filter-grid">
            <label>
              <span>FiscalReceiverId</span>
              <input type="number" [(ngModel)]="paymentFilters.fiscalReceiverIdText" placeholder="Id receptor" />
            </label>

            <label>
              <span>Estatus pago</span>
              <select [(ngModel)]="paymentFilters.operationalStatus">
                <option value="">Todos</option>
                <option value="CapturedUnapplied">CapturedUnapplied</option>
                <option value="PartiallyApplied">PartiallyApplied</option>
                <option value="FullyApplied">FullyApplied</option>
              </select>
            </label>

            <label>
              <span>Recibido desde</span>
              <input type="date" [(ngModel)]="paymentFilters.receivedFrom" />
            </label>

            <label>
              <span>Recibido hasta</span>
              <input type="date" [(ngModel)]="paymentFilters.receivedTo" />
            </label>

            <label>
              <span>Disponible</span>
              <select [(ngModel)]="paymentFilters.hasUnappliedAmount">
                <option value="">Todos</option>
                <option value="true">Con disponible</option>
                <option value="false">Sin disponible</option>
              </select>
            </label>

            <label>
              <span>FiscalDocumentId</span>
              <input type="number" [(ngModel)]="paymentFilters.linkedFiscalDocumentIdText" placeholder="Documento ligado" />
            </label>
          </div>

          <div class="actions">
            <button type="button" (click)="applyPaymentFilters()" [disabled]="loading()">Filtrar pagos</button>
            <button type="button" class="secondary" (click)="resetPaymentFilters()" [disabled]="loading()">Limpiar pagos</button>
          </div>

          @if (!paymentItems().length) {
            <p class="helper">No hay pagos con los filtros actuales.</p>
          } @else {
            <div class="table-wrap">
              <table class="portfolio">
                <thead>
                  <tr>
                    <th>Pago</th>
                    <th>Monto</th>
                    <th>Aplicado</th>
                    <th>Disponible</th>
                    <th>Estatus</th>
                    <th>REP</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of paymentItems(); track item.paymentId) {
                    <tr>
                      <td>
                        <strong>#{{ item.paymentId }}</strong>
                        <div class="subtle">{{ item.receivedAtUtc | date:'yyyy-MM-dd' }}</div>
                        <div class="subtle">{{ item.reference || 'Sin referencia' }}</div>
                      </td>
                      <td>{{ item.amount | currency:'MXN':'symbol':'1.2-2' }}</td>
                      <td>{{ item.appliedAmount | currency:'MXN':'symbol':'1.2-2' }}</td>
                      <td>{{ item.unappliedAmount | currency:'MXN':'symbol':'1.2-2' }}</td>
                      <td>
                        <span class="badge" [attr.data-status]="item.operationalStatus">{{ item.operationalStatus }}</span>
                        <div class="subtle">{{ item.applicationsCount }} aplicacion(es)</div>
                      </td>
                      <td>
                        <span class="badge" [attr.data-status]="item.repStatus">{{ item.repStatus }}</span>
                        <div class="subtle">
                          @if (item.repFiscalizedAmount > 0) {
                            Fiscalizado {{ item.repFiscalizedAmount | currency:'MXN':'symbol':'1.2-2' }}
                          } @else if (item.repReservedAmount > 0) {
                            Reservado {{ item.repReservedAmount | currency:'MXN':'symbol':'1.2-2' }}
                          } @else {
                            Sin REP
                          }
                        </div>
                      </td>
                      <td>
                        <a [routerLink]="['/app/accounts-receivable']" [queryParams]="{ paymentId: item.paymentId }">Ver detalle</a>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
      }

      @if (detailMode() && fiscalDocumentId()) {
        <section class="card">
          <h3>Cuenta por cobrar del documento fiscal #{{ fiscalDocumentId() }}</h3>
          @if (permissionService.canManagePayments()) {
            <button type="button" (click)="createInvoice()" [disabled]="loading()">Crear cuenta por cobrar</button>
          }
        </section>
      }

      @if (detailMode() && invoice(); as currentInvoice) {
        <app-accounts-receivable-card [invoice]="currentInvoice" />
      }

      @if (detailMode() && permissionService.canManagePayments()) {
        <app-payment-create-form [loading]="loading()" (submit)="createPayment($event)" />
      }

      @if (detailMode() && payment(); as currentPayment) {
        <section class="card">
          <h3>Pago #{{ currentPayment.id }}</h3>
          <p class="helper">Monto {{ currentPayment.amount }} MXN · Remanente {{ currentPayment.remainingAmount }} MXN</p>
          <div class="detail-badges">
            <span class="badge" [attr.data-status]="currentPayment.operationalStatus">{{ currentPayment.operationalStatus }}</span>
            <span class="badge" [attr.data-status]="currentPayment.repStatus">{{ currentPayment.repStatus }}</span>
          </div>
          @if (currentPayment.repFiscalizedAmount > 0 || currentPayment.repReservedAmount > 0) {
            <p class="helper">
              Fiscalizado {{ currentPayment.repFiscalizedAmount }} MXN · Reservado REP {{ currentPayment.repReservedAmount }} MXN
            </p>
          }
          @if (currentPayment.applications.length) {
            <table class="applications">
              <thead>
                <tr>
                  <th>Cuenta por cobrar</th>
                  <th>Aplicado</th>
                  <th>Saldo previo</th>
                  <th>Saldo nuevo</th>
                </tr>
              </thead>
              <tbody>
                @for (application of currentPayment.applications; track application.id) {
                  <tr>
                    <td>{{ application.accountsReceivableInvoiceId }}</td>
                    <td>{{ application.appliedAmount }}</td>
                    <td>{{ application.previousBalance }}</td>
                    <td>{{ application.newBalance }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
          @if (permissionService.canManagePayments()) {
            <app-payment-application-form [loading]="loading()" (submit)="applyPayment($event)" />
            <div class="links">
              <a [routerLink]="['/app/payment-complements']" [queryParams]="{ paymentId: currentPayment.id }">Abrir flujo de complemento de pago</a>
            </div>
          }
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2, h3 { margin:0; }
    .helper { color:#5f6b76; margin:0.35rem 0 0; }
    .section-head { display:flex; justify-content:space-between; gap:1rem; align-items:flex-end; margin-bottom:1rem; }
    .section-head.compact { margin-bottom:0.75rem; }
    .filter-grid { display:grid; gap:0.9rem; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); }
    label { display:grid; gap:0.35rem; }
    label span { font-size:0.82rem; color:#495766; }
    input, select { border:1px solid #d8d1c2; border-radius:0.75rem; padding:0.7rem 0.85rem; background:#fffdf8; }
    button, a { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; justify-content:center; }
    button.secondary { background:#eef1f4; color:#182533; }
    .actions { display:flex; gap:0.75rem; margin-top:1rem; }
    .table-wrap { overflow:auto; }
    .portfolio, .applications { width:100%; border-collapse:collapse; }
    .portfolio th, .portfolio td, .applications th, .applications td { text-align:left; padding:0.7rem; border-top:1px solid #ece3d3; vertical-align:top; }
    .subtle { color:#71808f; font-size:0.84rem; margin-top:0.2rem; }
    .badge { display:inline-flex; padding:0.3rem 0.55rem; border-radius:999px; font-size:0.78rem; background:#eef1f4; color:#243444; }
    .badge[data-status='Open'] { background:#fff2d8; color:#8a5a00; }
    .badge[data-status='PartiallyPaid'] { background:#dff2ea; color:#116149; }
    .badge[data-status='Paid'] { background:#dde8ff; color:#24498a; }
    .badge[data-status='Cancelled'] { background:#fde3e3; color:#8a2d2d; }
    .badge[data-status='CapturedUnapplied'] { background:#fff2d8; color:#8a5a00; }
    .badge[data-status='PartiallyApplied'] { background:#dff2ea; color:#116149; }
    .badge[data-status='FullyApplied'] { background:#dde8ff; color:#24498a; }
    .badge[data-status='NoApplications'] { background:#f1ece2; color:#745b31; }
    .badge[data-status='PendingApplications'] { background:#fff2d8; color:#8a5a00; }
    .badge[data-status='ReadyToPrepare'] { background:#dff2ea; color:#116149; }
    .badge[data-status='Prepared'] { background:#e5ebff; color:#324d8f; }
    .badge[data-status='Stamped'] { background:#dde8ff; color:#24498a; }
    .detail-badges { display:flex; gap:0.5rem; margin-top:0.75rem; flex-wrap:wrap; }
    .links { margin-top:1rem; }
    @media (max-width: 720px) {
      .actions { flex-direction:column; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountsReceivablePageComponent {
  private readonly api = inject(AccountsReceivableApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected readonly fiscalDocumentId = signal<number | null>(parseNumber(this.route.snapshot.queryParamMap.get('fiscalDocumentId')));
  protected readonly paymentId = signal<number | null>(parseNumber(this.route.snapshot.queryParamMap.get('paymentId')));
  protected readonly detailMode = computed(() => this.fiscalDocumentId() !== null || this.paymentId() !== null);
  protected readonly invoice = signal<AccountsReceivableInvoiceResponse | null>(null);
  protected readonly payment = signal<AccountsReceivablePaymentResponse | null>(null);
  protected readonly portfolioItems = signal<AccountsReceivablePortfolioItemResponse[]>([]);
  protected readonly paymentItems = signal<AccountsReceivablePaymentSummaryItemResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly filters = {
    receiverQuery: '',
    fiscalReceiverIdText: '',
    status: '',
    dueDateFrom: '',
    dueDateTo: '',
    pendingBalance: 'true'
  };
  protected readonly paymentFilters = {
    fiscalReceiverIdText: '',
    operationalStatus: '',
    receivedFrom: '',
    receivedTo: '',
    hasUnappliedAmount: '',
    linkedFiscalDocumentIdText: ''
  };

  constructor() {
    if (this.detailMode()) {
      if (this.fiscalDocumentId()) {
        void this.loadInvoice(this.fiscalDocumentId()!);
      }
      if (this.paymentId()) {
        void this.loadPayment(this.paymentId()!);
      }
    } else {
      void this.loadPortfolio();
      void this.loadPayments();
    }
  }

  protected formatFiscalLabel(item: AccountsReceivablePortfolioItemResponse): string {
    const series = item.fiscalSeries?.trim();
    const folio = item.fiscalFolio?.trim();
    if (series || folio) {
      return [series, folio].filter(Boolean).join('-');
    }

    if (item.fiscalDocumentId) {
      return `CFDI #${item.fiscalDocumentId}`;
    }

    return `CxC #${item.accountsReceivableInvoiceId}`;
  }

  protected async applyPortfolioFilters(): Promise<void> {
    await this.run(async () => {
      await this.loadPortfolio();
    });
  }

  protected async resetPortfolioFilters(): Promise<void> {
    this.filters.receiverQuery = '';
    this.filters.fiscalReceiverIdText = '';
    this.filters.status = '';
    this.filters.dueDateFrom = '';
    this.filters.dueDateTo = '';
    this.filters.pendingBalance = 'true';
    await this.applyPortfolioFilters();
  }

  protected async applyPaymentFilters(): Promise<void> {
    await this.run(async () => {
      await this.loadPayments();
    });
  }

  protected async resetPaymentFilters(): Promise<void> {
    this.paymentFilters.fiscalReceiverIdText = '';
    this.paymentFilters.operationalStatus = '';
    this.paymentFilters.receivedFrom = '';
    this.paymentFilters.receivedTo = '';
    this.paymentFilters.hasUnappliedAmount = '';
    this.paymentFilters.linkedFiscalDocumentIdText = '';
    await this.applyPaymentFilters();
  }

  protected async createInvoice(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.api.createInvoiceFromFiscalDocument(fiscalDocumentId));
      if (response.accountsReceivableInvoice) {
        this.invoice.set(response.accountsReceivableInvoice);
        this.feedbackService.show('success', 'Cuenta por cobrar creada.');
      }
    });
  }

  protected async createPayment(request: CreateAccountsReceivablePaymentRequest): Promise<void> {
    await this.run(async () => {
      const payload = {
        ...request,
        paymentDateUtc: new Date(request.paymentDateUtc).toISOString()
      };
      const response = await firstValueFrom(this.api.createPayment(payload));
      if (response.payment) {
        this.payment.set(response.payment);
        this.feedbackService.show('success', 'Pago registrado.');
      }
    });
  }

  protected async applyPayment(request: ApplyAccountsReceivablePaymentRequest): Promise<void> {
    const currentPayment = this.payment();
    if (!currentPayment) {
      this.feedbackService.show('error', 'Crea o carga un pago antes de aplicarlo.');
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.api.applyPayment(currentPayment.id, request));
      if (response.payment) {
        this.payment.set(response.payment);
      }

      if (this.fiscalDocumentId()) {
        await this.loadInvoice(this.fiscalDocumentId()!);
      }

      this.feedbackService.show('success', 'Aplicación de pago registrada.');
    });
  }

  private async loadPortfolio(): Promise<void> {
    const request = this.buildPortfolioRequest();
    const response = await firstValueFrom(this.api.searchPortfolio(request));
    this.portfolioItems.set(response.items);
  }

  private async loadPayments(): Promise<void> {
    const response = await firstValueFrom(this.api.searchPayments(this.buildPaymentRequest()));
    this.paymentItems.set(response.items);
  }

  private buildPortfolioRequest(): SearchAccountsReceivablePortfolioRequest {
    const fiscalReceiverId = parseNumber(this.filters.fiscalReceiverIdText);
    const hasPendingBalance = parseNullableBoolean(this.filters.pendingBalance);

    return {
      fiscalReceiverId,
      receiverQuery: this.filters.receiverQuery.trim() || null,
      status: this.filters.status || null,
      dueDateFrom: this.filters.dueDateFrom || null,
      dueDateTo: this.filters.dueDateTo || null,
      hasPendingBalance
    };
  }

  private buildPaymentRequest(): SearchAccountsReceivablePaymentsRequest {
    return {
      fiscalReceiverId: parseNumber(this.paymentFilters.fiscalReceiverIdText),
      operationalStatus: this.paymentFilters.operationalStatus || null,
      receivedFrom: this.paymentFilters.receivedFrom || null,
      receivedTo: this.paymentFilters.receivedTo || null,
      hasUnappliedAmount: parseNullableBoolean(this.paymentFilters.hasUnappliedAmount),
      linkedFiscalDocumentId: parseNumber(this.paymentFilters.linkedFiscalDocumentIdText)
    };
  }

  private async loadInvoice(fiscalDocumentId: number): Promise<void> {
    try {
      this.invoice.set(await firstValueFrom(this.api.getInvoiceByFiscalDocumentId(fiscalDocumentId)));
    } catch {
      this.invoice.set(null);
    }
  }

  private async loadPayment(paymentId: number): Promise<void> {
    try {
      this.payment.set(await firstValueFrom(this.api.getPaymentById(paymentId)));
    } catch {
      this.payment.set(null);
    }
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    try {
      await operation();
    } catch (error) {
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loading.set(false);
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

function parseNullableBoolean(value: string): boolean | null {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return null;
}

function extractErrorMessage(error: unknown): string {
  return extractApiErrorMessage(error);
}
