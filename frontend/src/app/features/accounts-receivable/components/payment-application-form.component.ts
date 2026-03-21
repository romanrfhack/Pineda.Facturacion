import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApplyAccountsReceivablePaymentRequest, ApplyAccountsReceivablePaymentRowRequest } from '../models/accounts-receivable.models';

@Component({
  selector: 'app-payment-application-form',
  imports: [FormsModule],
  template: `
    <section class="panel">
      <h3>Aplicar pago</h3>
      @for (row of rows(); track $index) {
        <div class="row">
          <label><span>Id de cuenta por cobrar</span><input [(ngModel)]="row.accountsReceivableInvoiceId" [name]="'invoice-' + $index" type="number" min="1" required /></label>
          <label><span>Monto aplicado</span><input [(ngModel)]="row.appliedAmount" [name]="'amount-' + $index" type="number" min="0.01" step="0.01" required /></label>
          <button type="button" class="secondary" (click)="removeRow($index)" [disabled]="rows().length === 1">Quitar</button>
        </div>
      }
      <div class="actions">
        <button type="button" class="secondary" (click)="addRow()">Agregar fila</button>
        <button type="button" (click)="submitRows()" [disabled]="loading()"> {{ loading() ? 'Aplicando...' : 'Aplicar pago' }} </button>
      </div>
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .row { display:grid; grid-template-columns:repeat(3, minmax(0, 1fr)); gap:1rem; margin-bottom:0.75rem; align-items:end; } label { display:grid; gap:0.35rem; } input, button { font:inherit; } input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; } .actions { display:flex; gap:0.75rem; flex-wrap:wrap; } button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; } button.secondary { background:#d8c49b; color:#182533; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentApplicationFormComponent {
  readonly loading = input(false);
  readonly submit = output<ApplyAccountsReceivablePaymentRequest>();
  protected readonly rows = signal<ApplyAccountsReceivablePaymentRowRequest[]>([
    { accountsReceivableInvoiceId: 0, appliedAmount: 0 }
  ]);

  protected addRow(): void {
    this.rows.update((rows) => [...rows, { accountsReceivableInvoiceId: 0, appliedAmount: 0 }]);
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, currentIndex) => currentIndex !== index));
  }

  protected submitRows(): void {
    this.submit.emit({
      applications: this.rows().map((row) => ({
        accountsReceivableInvoiceId: Number(row.accountsReceivableInvoiceId),
        appliedAmount: Number(row.appliedAmount)
      }))
    });
  }
}
