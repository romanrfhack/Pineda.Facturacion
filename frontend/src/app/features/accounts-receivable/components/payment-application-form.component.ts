import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApplyAccountsReceivablePaymentRequest, ApplyAccountsReceivablePaymentRowRequest } from '../models/accounts-receivable.models';

@Component({
  selector: 'app-payment-application-form',
  imports: [FormsModule],
  template: `
    <section class="panel">
      <h3>Aplicar pago a esta cuenta</h3>
      <p class="helper">Vincula el pago recibido contra la cuenta actual. El remanente no aplicado permanecerá disponible para uso posterior.</p>

      <div class="context-grid">
        <div>
          <span>Cuenta destino</span>
          <strong>#{{ currentInvoiceId() || 'N/D' }}</strong>
        </div>
        <div>
          <span>CFDI / folio</span>
          <strong>{{ invoiceLabel() }}</strong>
        </div>
        <div>
          <span>Saldo pendiente actual</span>
          <strong>{{ formatAmount(outstandingBalance()) }}</strong>
        </div>
        <div>
          <span>Monto del pago</span>
          <strong>{{ formatAmount(paymentAmount()) }}</strong>
        </div>
        <div>
          <span>Monto ya aplicado</span>
          <strong>{{ formatAmount(appliedAmount()) }}</strong>
        </div>
        <div>
          <span>Remanente disponible</span>
          <strong>{{ formatAmount(remainingAmount()) }}</strong>
        </div>
        <div>
          <span>Máximo aplicable a esta cuenta</span>
          <strong>{{ formatAmount(maxApplicable()) }}</strong>
        </div>
        <div>
          <span>Remanente después de aplicar</span>
          <strong>{{ formatAmount(remainingAfterApply()) }}</strong>
        </div>
      </div>

      <label class="amount-field">
        <span>Monto a aplicar a esta cuenta</span>
        <div class="amount-input-wrap">
          <input
            [(ngModel)]="draftAppliedAmountText"
            name="appliedAmount"
            type="text"
            inputmode="decimal"
            autocomplete="off"
            [disabled]="loading() || maxApplicable() <= 0"
            (ngModelChange)="onAmountChange($event)"
            (blur)="normalizeOnBlur()"
            required />
          <span class="currency-suffix">MXN</span>
        </div>
      </label>

      @if (maxApplicable() <= 0) {
        <p class="helper">No hay saldo pendiente o remanente disponible para aplicar en esta cuenta.</p>
      } @else if (requestedAmountExceeded()) {
        <p class="helper warning">El monto capturado excedía el máximo aplicable y se ajustó a {{ formatAmount(maxApplicable()) }}.</p>
      }

      <div class="actions">
        <button type="button" (click)="submitRows()" [disabled]="loading() || maxApplicable() <= 0 || draftAppliedAmount <= 0"> {{ loading() ? 'Aplicando...' : 'Aplicar pago a esta cuenta' }} </button>
      </div>
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; display:grid; gap:1rem; }
    .helper { margin:0; color:#5f6b76; }
    .helper.warning { color:#8a5a00; }
    .context-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.85rem; padding:0.9rem; border:1px solid #ece3d3; border-radius:0.9rem; background:#fffdf8; }
    .context-grid div { display:grid; gap:0.25rem; min-width:0; }
    .context-grid span { font-size:0.8rem; color:#5f6b76; }
    .context-grid strong { color:#182533; }
    .amount-field { display:grid; gap:0.35rem; }
    .amount-input-wrap { display:grid; grid-template-columns:minmax(0, 1fr) auto; gap:0.55rem; align-items:center; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .currency-suffix { color:#5f6b76; font-size:0.84rem; font-weight:600; }
    .actions { display:flex; gap:0.75rem; flex-wrap:wrap; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentApplicationFormComponent {
  readonly loading = input(false);
  readonly currentInvoiceId = input<number | null>(null);
  readonly invoiceLabel = input('Sin CFDI');
  readonly outstandingBalance = input(0);
  readonly paymentAmount = input(0);
  readonly appliedAmount = input(0);
  readonly remainingAmount = input(0);
  readonly submit = output<ApplyAccountsReceivablePaymentRequest>();
  protected readonly requestedAmountExceeded = signal(false);
  protected draftAppliedAmount = 0;
  protected draftAppliedAmountText = '0.00';

  protected readonly maxApplicable = computed(() =>
    this.roundMoney(Math.max(0, Math.min(this.outstandingBalance(), this.remainingAmount()))));

  constructor() {
    this.resetDraftAmount();
  }

  protected onAmountChange(value: string | number): void {
    const sanitizedValue = String(value ?? '').replace(/,/g, '').trim();
    const numericValue = Number(sanitizedValue);
    if (!Number.isFinite(numericValue) || numericValue <= 0) {
      this.draftAppliedAmount = 0;
      this.draftAppliedAmountText = sanitizedValue;
      this.requestedAmountExceeded.set(false);
      return;
    }

    const cappedValue = this.roundMoney(Math.min(numericValue, this.maxApplicable()));
    this.requestedAmountExceeded.set(numericValue > cappedValue);
    this.draftAppliedAmount = cappedValue;
    this.draftAppliedAmountText = numericValue > cappedValue
      ? this.formatEditableAmount(cappedValue)
      : sanitizedValue;
  }

  protected normalizeOnBlur(): void {
    this.draftAppliedAmountText = this.draftAppliedAmount > 0
      ? this.formatEditableAmount(this.draftAppliedAmount)
      : '0.00';
  }

  protected submitRows(): void {
    const accountsReceivableInvoiceId = Number(this.currentInvoiceId());
    if (!accountsReceivableInvoiceId || this.draftAppliedAmount <= 0) {
      return;
    }

    this.submit.emit({
      applications: [
        {
          accountsReceivableInvoiceId,
          appliedAmount: this.roundMoney(this.draftAppliedAmount)
        } satisfies ApplyAccountsReceivablePaymentRowRequest
      ]
    });
  }

  ngOnChanges(): void {
    this.resetDraftAmount();
  }

  protected formatAmount(value: number): string {
    return new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(value || 0);
  }

  protected remainingAfterApply(): number {
    return this.roundMoney(Math.max(0, this.remainingAmount() - this.draftAppliedAmount));
  }

  private resetDraftAmount(): void {
    this.draftAppliedAmount = this.maxApplicable();
    this.draftAppliedAmountText = this.formatEditableAmount(this.draftAppliedAmount);
    this.requestedAmountExceeded.set(false);
  }

  private roundMoney(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }

  private formatEditableAmount(value: number): string {
    return this.roundMoney(value).toFixed(2);
  }
}
