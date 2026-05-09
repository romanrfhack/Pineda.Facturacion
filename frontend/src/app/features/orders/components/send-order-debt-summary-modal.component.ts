import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { FiscalReceiver, FiscalReceiverSearchItem } from '../../catalogs/models/catalogs.models';
import { normalizeOrderCurrency, summarizeOrderSelection } from '../application/order-selection-summary';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import {
  LegacyOrderListItem,
  OrderDebtSummaryFormat,
  OrderDebtSummaryIncludeOptionsRequest,
  OrderDebtSummaryPreviewResponse,
  OrderDebtSummaryRequest,
  OrderDebtSummaryTotalByCurrencyResponse,
  SendOrderDebtSummaryResponse,
} from '../models/orders.models';

type SummaryStep = 1 | 2 | 3;

@Component({
  selector: 'app-send-order-debt-summary-modal',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    @if (open()) {
      <section class="modal-backdrop" (click)="requestClose()">
        <section
          class="modal-card order-summary-modal"
          role="dialog"
          aria-modal="true"
          aria-label="Enviar resumen de adeudos"
          (click)="$event.stopPropagation()"
        >
          <header class="modal-header">
            <div>
              <p class="eyebrow">Órdenes</p>
              <h3>Enviar resumen de adeudos</h3>
              @if (selectedReceiver(); as receiver) {
                <p class="helper">
                  {{ receiver.legalName }} · {{ receiver.rfc }}
                </p>
              }
            </div>
            <button type="button" class="secondary compact" (click)="requestClose()" [disabled]="sending()">
              Cerrar
            </button>
          </header>

          <nav class="stepper" aria-label="Pasos del resumen de adeudos">
            <button type="button" [class.active]="step() === 1" (click)="goToStep(1)" [disabled]="sending()">1. Órdenes seleccionadas</button>
            <button type="button" [class.active]="step() === 2" (click)="goToStep(2)" [disabled]="!canLeaveSelection() || sending()">2. Envío</button>
            <button type="button" [class.active]="step() === 3" (click)="goToStep(3)" [disabled]="!preview() || sending()">3. Vista previa</button>
          </nav>

          @if (errorMessage()) {
            <section class="status-panel status-panel-warning">{{ errorMessage() }}</section>
          }

          @if (step() === 1) {
            <section class="modal-step">
              <section class="summary-grid compact-summary">
                <article class="summary-card">
                  <strong>Órdenes seleccionadas</strong>
                  <div class="summary-card-value">{{ selectionSummary().count }}</div>
                </article>
                <article class="summary-card">
                  <strong>Total acumulado</strong>
                  <div class="summary-card-value">{{ formatSelectionTotals() }}</div>
                </article>
                <article class="summary-card">
                  <strong>Moneda</strong>
                  <div class="summary-card-value">{{ selectionCurrencyLabel() }}</div>
                </article>
              </section>

              @if (selectionSummary().totalsByCurrency.length > 1) {
                <div class="currency-strip">
                  @for (total of selectionSummary().totalsByCurrency; track total.currencyCode) {
                    <span>{{ total.currencyCode }} · {{ total.amount | currency: total.currencyCode : 'symbol' : '1.2-2' }}</span>
                  }
                </div>
              }

              <section class="receiver-panel">
                <label class="wide-field">
                  <span>Receptor / cliente</span>
                  <input
                    [ngModel]="receiverQuery"
                    (ngModelChange)="onReceiverQueryChange($event)"
                    type="text"
                    placeholder="Buscar por RFC o razón social"
                    autocomplete="off"
                  />
                </label>

                @if (searchingReceivers()) {
                  <p class="helper">Buscando receptores...</p>
                }

                @if (receiverSearchResults().length) {
                  <div class="lookup-results">
                    @for (receiver of receiverSearchResults(); track receiver.id) {
                      <button type="button" class="lookup-item" (click)="selectReceiver(receiver)">
                        <div>
                          <strong>{{ receiver.legalName }}</strong>
                          <div class="subtle">{{ receiver.rfc }} · #{{ receiver.id }}</div>
                        </div>
                        <span>Seleccionar</span>
                      </button>
                    }
                  </div>
                }

                @if (selectedReceiver(); as receiver) {
                  <section class="receiver-card">
                    <article><strong>Razón social / nombre</strong><span>{{ receiver.legalName }}</span></article>
                    <article><strong>RFC</strong><span>{{ receiver.rfc || 'No disponible' }}</span></article>
                    <article><strong>Correo</strong><span>{{ receiver.email || 'Captura manual requerida' }}</span></article>
                    <article><strong>Datos fiscales</strong><span>{{ formatReceiverFiscalData(receiver) }}</span></article>
                  </section>
                }
              </section>

              <div class="table-wrap">
                <table class="portfolio receiver-workspace-table">
                  <thead>
                    <tr>
                      <th>Orden / Nota / Pedido</th>
                      <th>Fecha</th>
                      <th>Cliente</th>
                      <th>Moneda</th>
                      <th>Total</th>
                      <th>Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (order of selectedOrders(); track order.legacyOrderId) {
                      <tr>
                        <td data-label="Orden">
                          <strong>{{ formatOrderLabel(order) }}</strong>
                          <div class="subtle">{{ order.legacyOrderId }}</div>
                        </td>
                        <td data-label="Fecha">{{ order.orderDateUtc | date: 'yyyy-MM-dd' }}</td>
                        <td data-label="Cliente">{{ order.customerName }}</td>
                        <td data-label="Moneda">{{ normalizeCurrency(order.currencyCode) }}</td>
                        <td data-label="Total">{{ order.total | currency: normalizeCurrency(order.currencyCode) : 'symbol' : '1.2-2' }}</td>
                        <td data-label="Estado">
                          <span class="badge">{{ formatBillingStatus(order) }}</span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </section>
          }

          @if (step() === 2) {
            <section class="modal-step">
              <div class="filter-grid">
                <label class="wide-field">
                  <span>Para</span>
                  <input type="text" [(ngModel)]="toInput" placeholder="correo@cliente.com" />
                </label>
                <label>
                  <span>CC</span>
                  <input type="text" [(ngModel)]="ccInput" placeholder="opcional" />
                </label>
                <label>
                  <span>CCO</span>
                  <input type="text" [(ngModel)]="bccInput" placeholder="opcional" />
                </label>
                <label class="wide-field">
                  <span>Asunto</span>
                  <input type="text" [(ngModel)]="subject" />
                </label>
                <label class="wide-field">
                  <span>Mensaje inicial</span>
                  <textarea rows="4" [(ngModel)]="message"></textarea>
                </label>
                <label>
                  <span>Formato</span>
                  <select [(ngModel)]="format">
                    <option value="html">Correo HTML con resumen</option>
                  </select>
                </label>
              </div>

              <section class="checkbox-grid">
                <label><input type="checkbox" [(ngModel)]="includeOptions.includeOrderTable" /> Incluir tabla de órdenes/notas</label>
                <label><input type="checkbox" [(ngModel)]="includeOptions.includeTotals" /> Incluir totales</label>
                <label><input type="checkbox" [(ngModel)]="includeOptions.includeReceiverFiscalData" /> Datos fiscales del receptor</label>
                <label><input type="checkbox" [(ngModel)]="includeOptions.includeIssuerData" /> Datos del emisor</label>
                <label><input type="checkbox" [(ngModel)]="includeOptions.includePaymentInstructions" /> Instrucciones y seguimiento</label>
                <label><input type="checkbox" [(ngModel)]="includeOptions.includeBillingStatus" /> Mostrar estado de facturación/timbrado</label>
              </section>
            </section>
          }

          @if (step() === 3) {
            <section class="modal-step">
              @if (previewing()) {
                <p class="helper">Generando vista previa...</p>
              } @else if (preview(); as currentPreview) {
                <section class="final-summary">
                  <article><strong>Destinatarios</strong><span>{{ currentPreview.finalSummary?.to?.join(', ') || 'Sin destinatario' }}</span></article>
                  <article><strong>Órdenes</strong><span>{{ currentPreview.finalSummary?.orderCount ?? selectionSummary().count }}</span></article>
                  <article><strong>{{ previewTotalsLabel(currentPreview.finalSummary?.totalsByCurrency || []) }}</strong><span>{{ formatPreviewTotals(currentPreview.finalSummary?.totalsByCurrency || []) }}</span></article>
                  <article><strong>Formato</strong><span>{{ formatLabel(currentPreview.finalSummary?.format || format) }}</span></article>
                </section>

                <section class="email-preview" [innerHTML]="previewHtml()"></section>
              } @else {
                <p class="helper">Genera la vista previa para confirmar el envío.</p>
              }
            </section>
          }

          <footer class="modal-footer">
            <button type="button" class="secondary" (click)="back()" [disabled]="step() === 1 || sending() || previewing()">Atrás</button>
            @if (step() < 3) {
              <button type="button" (click)="next()" [disabled]="previewing() || sending()">
                {{ step() === 2 ? 'Generar vista previa' : 'Siguiente' }}
              </button>
            } @else {
              <button type="button" (click)="send()" [disabled]="sending() || previewing() || !preview()?.success">
                {{ sending() ? 'Enviando...' : 'Enviar resumen' }}
              </button>
            }
          </footer>
        </section>
      </section>
    }
  `,
  styles: [
    `
      .modal-backdrop { position:fixed; inset:0; background:rgba(24,37,51,.52); display:grid; place-items:center; padding:1rem; z-index:70; }
      .modal-card { width:min(1180px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24,37,51,.24); }
      .modal-header, .modal-footer, .actions { display:flex; justify-content:space-between; gap:.75rem; align-items:flex-start; flex-wrap:wrap; }
      .modal-footer { justify-content:flex-end; border-top:1px solid #ece3d3; padding-top:1rem; }
      .eyebrow { margin:0; text-transform:uppercase; letter-spacing:.12em; font-size:.72rem; color:#8a6a32; }
      h3 { margin:.25rem 0 0; }
      .helper, .subtle { color:#5f6b76; margin:.25rem 0 0; }
      .stepper { display:grid; grid-template-columns:repeat(3, 1fr); gap:.5rem; }
      .stepper button { background:#eef1f4; color:#182533; }
      .stepper button.active { background:#182533; color:#fff; }
      button, a { border:none; border-radius:.8rem; padding:.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; justify-content:center; align-items:center; gap:.5rem; }
      button.secondary { background:#eef1f4; color:#182533; }
      button.compact { padding:.55rem .8rem; }
      button:disabled { opacity:.58; cursor:not-allowed; }
      .modal-step { display:grid; gap:1rem; }
      .summary-grid, .checkbox-grid, .final-summary, .receiver-card { display:grid; gap:.75rem; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); }
      .summary-card, .final-summary article, .receiver-card article { border:1px solid #ece3d3; border-radius:.85rem; padding:.85rem; background:#fffdf8; display:grid; gap:.35rem; }
      .summary-card-value { font-size:1.02rem; font-weight:700; }
      .currency-strip { display:flex; flex-wrap:wrap; gap:.5rem; }
      .currency-strip span, .badge { display:inline-flex; padding:.3rem .55rem; border-radius:999px; font-size:.78rem; background:#eef1f4; color:#243444; }
      .table-wrap { overflow:auto; }
      .portfolio { width:100%; border-collapse:collapse; }
      .portfolio th, .portfolio td { text-align:left; padding:.7rem; border-top:1px solid #ece3d3; vertical-align:top; }
      .filter-grid { display:grid; gap:.9rem; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); }
      label { display:grid; gap:.35rem; }
      label span { font-size:.82rem; color:#495766; }
      input, select, textarea { border:1px solid #d8d1c2; border-radius:.75rem; padding:.7rem .85rem; background:#fffdf8; font:inherit; }
      .wide-field { grid-column:1 / -1; }
      .status-panel { border-radius:.9rem; padding:.9rem 1rem; border:1px solid #ecd9aa; background:#fff8ea; color:#4d3a16; }
      .email-preview { border:1px solid #d8d1c2; border-radius:1rem; background:#f7f4ed; max-height:480px; overflow:auto; }
      .lookup-results { display:grid; gap:.5rem; }
      .lookup-item { background:#fffdf8; border:1px solid #ece3d3; justify-content:space-between; text-align:left; }
      .receiver-panel { display:grid; gap:1rem; }
      @media (max-width:720px) {
        .stepper { grid-template-columns:1fr; }
        .modal-header, .modal-footer { flex-direction:column; align-items:stretch; }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SendOrderDebtSummaryModalComponent {
  private readonly ordersApi = inject(OrdersApiService);
  private readonly fiscalReceiversApi = inject(FiscalReceiversApiService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly open = input(false);
  readonly selectedOrders = input<readonly LegacyOrderListItem[]>([]);
  readonly closed = output<void>();
  readonly sent = output<SendOrderDebtSummaryResponse>();

  protected readonly step = signal<SummaryStep>(1);
  protected readonly searchingReceivers = signal(false);
  protected readonly previewing = signal(false);
  protected readonly sending = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly receiverSearchResults = signal<FiscalReceiverSearchItem[]>([]);
  protected readonly selectedReceiver = signal<FiscalReceiver | null>(null);
  protected readonly preview = signal<OrderDebtSummaryPreviewResponse | null>(null);
  protected readonly previewHtml = computed<SafeHtml>(() =>
    this.sanitizer.bypassSecurityTrustHtml(this.preview()?.html || ''),
  );
  protected readonly selectionSummary = computed(() => summarizeOrderSelection(this.selectedOrders()));

  protected receiverQuery = '';
  protected toInput = '';
  protected ccInput = '';
  protected bccInput = '';
  protected subject = '';
  protected message = '';
  protected format: OrderDebtSummaryFormat = 'html';
  protected readonly includeOptions: OrderDebtSummaryIncludeOptionsRequest = {
    includeOrderTable: true,
    includeTotals: true,
    includeReceiverFiscalData: true,
    includeIssuerData: true,
    includePaymentInstructions: true,
    includeBillingStatus: true,
  };

  private receiverSearchRequestId = 0;

  private readonly loadEffect = effect(() => {
    if (this.open()) {
      untracked(() => this.resetStateForOpen());
    }
  });

  protected formatOrderLabel(order: LegacyOrderListItem): string {
    return order.legacyOrderType?.trim()
      ? `${order.legacyOrderId} (${order.legacyOrderType.trim()})`
      : order.legacyOrderId;
  }

  protected normalizeCurrency(currencyCode?: string | null): string {
    return normalizeOrderCurrency(currencyCode);
  }

  protected formatSelectionTotals(): string {
    return this.formatTotals(this.selectionSummary().totalsByCurrency.map((total) => ({
      currencyCode: total.currencyCode,
      orderCount: this.selectedOrders().filter((order) => normalizeOrderCurrency(order.currencyCode) === total.currencyCode).length,
      total: total.amount,
    })));
  }

  protected selectionCurrencyLabel(): string {
    return this.selectionSummary().totalsByCurrency.length === 1
      ? this.selectionSummary().totalsByCurrency[0].currencyCode
      : 'Múltiples';
  }

  protected formatBillingStatus(order: LegacyOrderListItem): string {
    if (order.fiscalDocumentId || order.fiscalDocumentStatus) {
      return order.fiscalDocumentStatus?.trim()
        ? `Fiscal: ${order.fiscalDocumentStatus.trim()}`
        : 'Con comprobante fiscal';
    }

    if (order.billingDocumentId || order.billingDocumentStatus) {
      return order.billingDocumentStatus?.trim()
        ? `Facturación: ${order.billingDocumentStatus.trim()}`
        : 'Con documento de facturación';
    }

    if (order.isImported || order.importStatus) {
      return 'Importada';
    }

    return 'Pendiente';
  }

  protected formatReceiverFiscalData(receiver: FiscalReceiver): string {
    const parts = [
      receiver.fiscalRegimeCode ? `Régimen ${receiver.fiscalRegimeCode}` : null,
      receiver.postalCode ? `CP ${receiver.postalCode}` : null,
    ].filter(Boolean);

    return parts.length > 0 ? parts.join(' · ') : 'Sin datos fiscales adicionales';
  }

  protected canLeaveSelection(): boolean {
    return this.selectedOrders().length > 0
      && !!this.selectedReceiver()
      && this.getCustomerSelectionValidationError() === null;
  }

  protected async goToStep(step: SummaryStep): Promise<void> {
    if (step === 2 && !this.validateSelection()) {
      return;
    }

    if (step === 3) {
      if (!this.validateSelection() || !this.validateEmailConfiguration()) {
        return;
      }

      if (!this.preview()) {
        await this.generatePreview();
      }
    }

    this.step.set(step);
  }

  protected async next(): Promise<void> {
    if (this.step() === 1) {
      if (this.validateSelection()) {
        this.step.set(2);
      }
      return;
    }

    if (this.step() === 2 && this.validateSelection() && this.validateEmailConfiguration()) {
      await this.generatePreview();
    }
  }

  protected back(): void {
    this.errorMessage.set(null);
    this.step.set(this.step() === 3 ? 2 : 1);
  }

  protected requestClose(): void {
    if (this.sending()) {
      return;
    }

    this.closed.emit();
  }

  protected async onReceiverQueryChange(value: string): Promise<void> {
    this.receiverQuery = value;
    this.preview.set(null);

    if (this.selectedReceiver()?.legalName !== value && this.selectedReceiver()?.rfc !== value) {
      this.selectedReceiver.set(null);
    }

    const query = value.trim();
    if (query.length < 2) {
      this.receiverSearchResults.set([]);
      this.searchingReceivers.set(false);
      return;
    }

    const requestId = ++this.receiverSearchRequestId;
    this.searchingReceivers.set(true);

    try {
      const receivers = await firstValueFrom(this.fiscalReceiversApi.search(query));
      if (requestId !== this.receiverSearchRequestId) {
        return;
      }

      this.receiverSearchResults.set(receivers);
    } catch (error) {
      if (requestId === this.receiverSearchRequestId) {
        this.errorMessage.set(extractApiErrorMessage(error));
        this.receiverSearchResults.set([]);
      }
    } finally {
      if (requestId === this.receiverSearchRequestId) {
        this.searchingReceivers.set(false);
      }
    }
  }

  protected async selectReceiver(receiver: FiscalReceiverSearchItem): Promise<void> {
    this.errorMessage.set(null);
    this.preview.set(null);
    try {
      const detail = await firstValueFrom(this.fiscalReceiversApi.getByRfc(receiver.rfc));
      this.selectedReceiver.set(detail);
      this.receiverQuery = detail.legalName;
      this.receiverSearchResults.set([]);
      this.toInput = detail.email?.trim() ?? '';
      this.subject = `Resumen de notas pendientes - ${detail.legalName}`;
      this.message =
        `Estimado ${detail.legalName}, compartimos el resumen de notas/órdenes pendientes para su revisión. Favor de indicarnos cuáles desea que facturemos y confirmar cualquier aclaración sobre pago o datos fiscales.`;
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    }
  }

  protected async send(): Promise<void> {
    if (this.sending() || !this.preview()?.success || !this.validateSelection() || !this.validateEmailConfiguration()) {
      return;
    }

    this.sending.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.ordersApi.sendOrderDebtSummary(this.buildRequest()));
      if (!response.success) {
        this.errorMessage.set(response.errorMessage || 'No fue posible enviar el resumen.');
        return;
      }

      this.sent.emit(response);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.sending.set(false);
    }
  }

  protected formatPreviewTotals(totals: readonly OrderDebtSummaryTotalByCurrencyResponse[]): string {
    return this.formatTotals(totals);
  }

  protected previewTotalsLabel(totals: readonly OrderDebtSummaryTotalByCurrencyResponse[]): string {
    return totals.length > 1 ? 'Totales por moneda' : 'Total';
  }

  protected formatLabel(format: string): string {
    return format.toLowerCase() === 'html' ? 'Correo HTML con resumen' : format;
  }

  private resetStateForOpen(): void {
    this.step.set(1);
    this.errorMessage.set(null);
    this.preview.set(null);
    this.receiverSearchResults.set([]);
    this.searchingReceivers.set(false);
    this.selectedReceiver.set(null);
    this.receiverQuery = '';
    this.toInput = '';
    this.ccInput = '';
    this.bccInput = '';
    this.subject = '';
    this.message = '';
    this.format = 'html';
    this.includeOptions.includeOrderTable = true;
    this.includeOptions.includeTotals = true;
    this.includeOptions.includeReceiverFiscalData = true;
    this.includeOptions.includeIssuerData = true;
    this.includeOptions.includePaymentInstructions = true;
    this.includeOptions.includeBillingStatus = true;
  }

  private async generatePreview(): Promise<void> {
    this.previewing.set(true);
    this.errorMessage.set(null);
    this.preview.set(null);

    try {
      const response = await firstValueFrom(this.ordersApi.previewOrderDebtSummary(this.buildRequest()));
      if (!response.success) {
        this.errorMessage.set(response.errorMessage || 'No fue posible generar la vista previa.');
        return;
      }

      this.preview.set(response);
      this.step.set(3);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.previewing.set(false);
    }
  }

  private validateSelection(): boolean {
    if (!this.selectedOrders().length) {
      this.errorMessage.set('Selecciona al menos una orden para continuar.');
      return false;
    }

    if (!this.selectedReceiver()) {
      this.errorMessage.set('Selecciona un receptor para continuar.');
      return false;
    }

    const customerValidationError = this.getCustomerSelectionValidationError();
    if (customerValidationError) {
      this.errorMessage.set(customerValidationError);
      return false;
    }

    this.errorMessage.set(null);
    return true;
  }

  private validateEmailConfiguration(): boolean {
    if (!parseRecipients(this.toInput).length) {
      this.errorMessage.set('Captura al menos un correo válido en Para.');
      return false;
    }

    const invalid = [this.toInput, this.ccInput, this.bccInput]
      .flatMap((value) => parseInvalidRecipients(value));
    if (invalid.length) {
      this.errorMessage.set(`Correo inválido: ${invalid.join(', ')}`);
      return false;
    }

    if (!this.subject.trim()) {
      this.errorMessage.set('Captura el asunto del correo.');
      return false;
    }

    if (!this.message.trim()) {
      this.errorMessage.set('Captura el mensaje inicial.');
      return false;
    }

    this.errorMessage.set(null);
    return true;
  }

  private buildRequest(): OrderDebtSummaryRequest {
    return {
      legacyOrderIds: this.selectedOrders().map((order) => order.legacyOrderId),
      receiverId: this.selectedReceiver()!.id,
      to: parseRecipients(this.toInput),
      cc: parseRecipients(this.ccInput),
      bcc: parseRecipients(this.bccInput),
      subject: this.subject.trim(),
      message: this.message.trim(),
      format: this.format,
      options: { ...this.includeOptions },
    };
  }

  private getCustomerSelectionValidationError(): string | null {
    const orders = this.selectedOrders();
    const selectedReceiver = this.selectedReceiver();
    if (!orders.length || !selectedReceiver) {
      return null;
    }

    const orderRfcs = distinctValues(orders.map((order) => normalizeRfc(order.customerRfc)));
    if (orderRfcs.length > 1) {
      return MIXED_CUSTOMERS_ERROR_MESSAGE;
    }

    const anyMissingRfc = orders.some((order) => normalizeRfc(order.customerRfc) === null);
    if (anyMissingRfc) {
      const customerIds = orders.map((order) => normalizeStableCustomerId(order.customerLegacyId));
      if (customerIds.every((customerId) => customerId !== null)) {
        if (distinctValues(customerIds).length > 1) {
          return MIXED_CUSTOMERS_ERROR_MESSAGE;
        }
      } else {
        const customerNames = orders.map((order) => normalizeCustomerName(order.customerName));
        if (customerNames.some((customerName) => customerName === null)) {
          return CUSTOMER_IDENTITY_UNAVAILABLE_ERROR_MESSAGE;
        }

        if (distinctValues(customerNames).length > 1) {
          return MIXED_CUSTOMERS_ERROR_MESSAGE;
        }
      }
    }

    if (orderRfcs.length === 1) {
      const receiverRfc = normalizeRfc(selectedReceiver.rfc);
      if (orderRfcs[0] !== receiverRfc) {
        return RECEIVER_RFC_MISMATCH_ERROR_MESSAGE;
      }
    }

    return null;
  }

  private formatTotals(totals: readonly { currencyCode: string; total?: number; amount?: number }[]): string {
    if (!totals.length) {
      return '0.00 MXN';
    }

    return totals
      .map((total) => {
        const currencyCode = normalizeOrderCurrency(total.currencyCode);
        const amount = typeof total.total === 'number' ? total.total : total.amount ?? 0;
        return new Intl.NumberFormat('es-MX', {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        }).format(amount) + ` ${currencyCode}`;
      })
      .join(' · ');
  }
}

function parseRecipients(value: string): string[] {
  return splitRecipients(value).filter((recipient) => isValidEmail(recipient));
}

function parseInvalidRecipients(value: string): string[] {
  return splitRecipients(value).filter((recipient) => !isValidEmail(recipient));
}

function splitRecipients(value: string): string[] {
  return value
    .split(/[;,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

const MIXED_CUSTOMERS_ERROR_MESSAGE = 'No se puede enviar el resumen porque la selección contiene órdenes de distintos clientes. Selecciona únicamente órdenes del mismo cliente.';
const RECEIVER_RFC_MISMATCH_ERROR_MESSAGE = 'El RFC del receptor seleccionado no coincide con el RFC de las órdenes seleccionadas.';
const CUSTOMER_IDENTITY_UNAVAILABLE_ERROR_MESSAGE = 'No se puede enviar el resumen porque no hay datos suficientes para validar que todas las órdenes pertenezcan al mismo cliente.';

function distinctValues(values: readonly (string | null)[]): string[] {
  return Array.from(new Set(values.filter((value): value is string => value !== null)));
}

function normalizeRfc(value: string | null | undefined): string | null {
  const normalized = (value ?? '').replace(/\s+/g, '').toUpperCase();
  return normalized ? normalized : null;
}

function normalizeStableCustomerId(value: string | null | undefined): string | null {
  const normalized = (value ?? '').replace(/\s+/g, '').toUpperCase();
  return normalized && normalized !== '0' ? normalized : null;
}

function normalizeCustomerName(value: string | null | undefined): string | null {
  const normalized = (value ?? '').trim().replace(/\s+/g, ' ').toUpperCase();
  return normalized ? normalized : null;
}
