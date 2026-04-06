import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccountsReceivablePortfolioItemResponse, ApplyAccountsReceivablePaymentRequest } from '../models/accounts-receivable.models';

@Component({
  selector: 'app-payment-remainder-application-form',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    <section class="panel">
      <div class="section-head">
        <div>
          <h3>Aplicar remanente a otras facturas del mismo receptor</h3>
          <p class="helper">Distribuye el remanente del pago entre otras cuentas abiertas del mismo receptor.</p>
        </div>
      </div>

      @if (!eligibleInvoices().length) {
        <p class="helper">No hay otras facturas elegibles del mismo receptor con saldo pendiente.</p>
      } @else {
        <div class="summary-grid">
          <div><span>Monto total del pago</span><strong>{{ paymentAmount() | currency:'MXN':'symbol':'1.2-2' }}</strong></div>
          <div><span>Monto ya aplicado</span><strong>{{ appliedAmount() | currency:'MXN':'symbol':'1.2-2' }}</strong></div>
          <div><span>Remanente disponible</span><strong>{{ remainingAmount() | currency:'MXN':'symbol':'1.2-2' }}</strong></div>
          <div><span>Total a aplicar en esta propuesta</span><strong>{{ totalProposed() | currency:'MXN':'symbol':'1.2-2' }}</strong></div>
          <div><span>Remanente después de esta propuesta</span><strong>{{ remainingAfterProposal() | currency:'MXN':'symbol':'1.2-2' }}</strong></div>
        </div>

        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Cuenta</th>
                <th>CFDI</th>
                <th>Emisión / vencimiento</th>
                <th>Saldo pendiente</th>
                <th>Monto a aplicar</th>
                <th>Saldo restante estimado</th>
              </tr>
            </thead>
            <tbody>
              @for (item of eligibleInvoices(); track item.accountsReceivableInvoiceId) {
                <tr>
                  <td>#{{ item.accountsReceivableInvoiceId }}</td>
                  <td>
                    <div>{{ formatFiscalLabel(item) }}</div>
                    <div class="subtle">{{ item.fiscalUuid || 'UUID pendiente' }}</div>
                  </td>
                  <td>
                    <div>{{ item.issuedAtUtc | date:'yyyy-MM-dd' }}</div>
                    <div class="subtle">{{ item.dueAtUtc ? (item.dueAtUtc | date:'yyyy-MM-dd') : 'Sin vencimiento' }}</div>
                  </td>
                  <td>{{ item.outstandingBalance | currency:'MXN':'symbol':'1.2-2' }}</td>
                  <td>
                    <input
                      [ngModel]="proposalText(item.accountsReceivableInvoiceId)"
                      (ngModelChange)="onProposalChange(item.accountsReceivableInvoiceId, item.outstandingBalance, $event)"
                      (blur)="normalizeProposal(item.accountsReceivableInvoiceId)"
                      type="text"
                      inputmode="decimal"
                      autocomplete="off"
                      [disabled]="loading() || remainingAmount() <= 0"
                    />
                  </td>
                  <td>{{ estimatedRemaining(item) | currency:'MXN':'symbol':'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (adjustmentMessage()) {
          <p class="helper warning">{{ adjustmentMessage() }}</p>
        }

        <div class="actions">
          <button type="button" (click)="submitProposal()" [disabled]="loading() || totalProposed() <= 0">Aplicar remanente seleccionado</button>
        </div>
      }
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; display:grid; gap:1rem; }
    .section-head { display:flex; justify-content:space-between; gap:1rem; align-items:flex-end; }
    .helper { margin:0; color:#5f6b76; }
    .helper.warning { color:#8a5a00; }
    .summary-grid { display:grid; gap:0.85rem; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); padding:0.9rem; border:1px solid #ece3d3; border-radius:0.9rem; background:#fffdf8; }
    .summary-grid div { display:grid; gap:0.25rem; }
    .summary-grid span { font-size:0.8rem; color:#5f6b76; }
    .summary-grid strong { color:#182533; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.7rem; border-top:1px solid #ece3d3; vertical-align:top; }
    .subtle { color:#71808f; font-size:0.84rem; margin-top:0.2rem; }
    input { width:100%; min-width:0; border:1px solid #c9d1da; border-radius:0.8rem; padding:0.7rem 0.85rem; box-sizing:border-box; font:inherit; }
    .actions { display:flex; gap:0.75rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; font:inherit; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentRemainderApplicationFormComponent {
  readonly loading = input(false);
  readonly eligibleInvoices = input<AccountsReceivablePortfolioItemResponse[]>([]);
  readonly paymentAmount = input(0);
  readonly appliedAmount = input(0);
  readonly remainingAmount = input(0);
  readonly submit = output<ApplyAccountsReceivablePaymentRequest>();

  protected readonly proposals = signal<Record<number, number>>({});
  protected readonly proposalTexts = signal<Record<number, string>>({});
  protected readonly adjustmentMessage = signal<string | null>(null);

  protected readonly totalProposed = computed(() =>
    this.roundMoney(Object.values(this.proposals()).reduce((sum, amount) => sum + amount, 0)));

  protected readonly remainingAfterProposal = computed(() =>
    this.roundMoney(Math.max(0, this.remainingAmount() - this.totalProposed())));

  ngOnChanges(): void {
    const nextTexts: Record<number, string> = {};
    for (const item of this.eligibleInvoices()) {
      nextTexts[item.accountsReceivableInvoiceId] = this.formatEditable(0);
    }

    this.proposals.set({});
    this.proposalTexts.set(nextTexts);
    this.adjustmentMessage.set(null);
  }

  protected onProposalChange(invoiceId: number, outstandingBalance: number, value: string): void {
    const sanitized = String(value ?? '').replace(/,/g, '').trim();
    const numericValue = Number(sanitized);
    if (!Number.isFinite(numericValue) || numericValue <= 0) {
      this.proposals.update((current) => ({ ...current, [invoiceId]: 0 }));
      this.proposalTexts.update((current) => ({ ...current, [invoiceId]: sanitized }));
      this.adjustmentMessage.set(null);
      return;
    }

    const proposals = { ...this.proposals() };
    const currentForRow = proposals[invoiceId] ?? 0;
    const proposedOtherRows = this.roundMoney(this.totalProposed() - currentForRow);
    const availableForRow = this.roundMoney(Math.max(0, this.remainingAmount() - proposedOtherRows));
    const cappedValue = this.roundMoney(Math.min(numericValue, outstandingBalance, availableForRow));

    proposals[invoiceId] = cappedValue;
    this.proposals.set(proposals);
    this.proposalTexts.update((current) => ({
      ...current,
      [invoiceId]: numericValue > cappedValue ? this.formatEditable(cappedValue) : sanitized
    }));

    this.adjustmentMessage.set(
      numericValue > cappedValue
        ? 'Se ajustó el monto para respetar el saldo pendiente de la factura y el remanente disponible del pago.'
        : null);
  }

  protected normalizeProposal(invoiceId: number): void {
    const amount = this.proposals()[invoiceId] ?? 0;
    this.proposalTexts.update((current) => ({
      ...current,
      [invoiceId]: this.formatEditable(amount)
    }));
  }

  protected proposalText(invoiceId: number): string {
    return this.proposalTexts()[invoiceId] ?? this.formatEditable(0);
  }

  protected estimatedRemaining(item: AccountsReceivablePortfolioItemResponse): number {
    const proposed = this.proposals()[item.accountsReceivableInvoiceId] ?? 0;
    return this.roundMoney(Math.max(0, item.outstandingBalance - proposed));
  }

  protected submitProposal(): void {
    const applications = this.eligibleInvoices()
      .map((item) => ({
        accountsReceivableInvoiceId: item.accountsReceivableInvoiceId,
        appliedAmount: this.roundMoney(this.proposals()[item.accountsReceivableInvoiceId] ?? 0)
      }))
      .filter((item) => item.appliedAmount > 0);

    if (!applications.length) {
      return;
    }

    this.submit.emit({ applications });
  }

  protected formatFiscalLabel(item: AccountsReceivablePortfolioItemResponse): string {
    const series = item.fiscalSeries?.trim();
    const folio = item.fiscalFolio?.trim();
    if (series || folio) {
      return [series, folio].filter(Boolean).join('-');
    }

    return item.fiscalDocumentId ? `CFDI #${item.fiscalDocumentId}` : `CxC #${item.accountsReceivableInvoiceId}`;
  }

  private roundMoney(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }

  private formatEditable(value: number): string {
    return this.roundMoney(value).toFixed(2);
  }
}
