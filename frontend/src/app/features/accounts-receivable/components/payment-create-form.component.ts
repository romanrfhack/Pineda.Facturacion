import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CreateAccountsReceivablePaymentRequest } from '../models/accounts-receivable.models';

@Component({
  selector: 'app-payment-create-form',
  imports: [FormsModule],
  template: `
    <section class="panel">
      <h3>Crear pago</h3>
      <form class="form-grid" (ngSubmit)="submit.emit(model)">
        <label><span>Fecha de pago</span><input [(ngModel)]="model.paymentDateUtc" name="paymentDateUtc" type="datetime-local" required /></label>
        <label><span>Forma de pago SAT</span><input [(ngModel)]="model.paymentFormSat" name="paymentFormSat" required /></label>
        <label><span>Monto</span><input [(ngModel)]="model.amount" name="amount" type="number" min="0.01" step="0.01" required /></label>
        <label><span>Referencia</span><input [(ngModel)]="model.reference" name="reference" /></label>
        <label><span>Notas</span><input [(ngModel)]="model.notes" name="notes" /></label>
        <button type="submit" [disabled]="loading()"> {{ loading() ? 'Guardando...' : 'Crear pago' }} </button>
      </form>
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; } label { display:grid; gap:0.35rem; } input, button { font:inherit; } input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; } button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentCreateFormComponent {
  readonly loading = input(false);
  readonly submit = output<CreateAccountsReceivablePaymentRequest>();

  protected readonly model: CreateAccountsReceivablePaymentRequest = {
    paymentDateUtc: new Date().toISOString().slice(0, 16),
    paymentFormSat: '03',
    amount: 0,
    reference: '',
    notes: ''
  };
}
