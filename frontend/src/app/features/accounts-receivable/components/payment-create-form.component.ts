import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { FiscalReceiverSatCatalogService } from '../../catalogs/application/fiscal-receiver-sat-catalog.service';
import { FiscalReceiverSatCatalogOption } from '../../catalogs/models/catalogs.models';
import { CreateAccountsReceivablePaymentRequest } from '../models/accounts-receivable.models';

export function formatDateTimeLocalValue(date: Date): string {
  const localDate = new Date(date.getTime() - (date.getTimezoneOffset() * 60_000));
  return localDate.toISOString().slice(0, 16);
}

@Component({
  selector: 'app-payment-create-form',
  imports: [FormsModule],
  template: `
    <section class="panel">
      <h3>Crear pago</h3>
      <form class="form-grid" #paymentForm="ngForm" (ngSubmit)="handleSubmit()">
        <label>
          <span>Fecha de pago</span>
          <input class="field-control" [(ngModel)]="model.paymentDateUtc" name="paymentDateUtc" type="datetime-local" required />
        </label>

        <label>
          <span>Forma de pago SAT</span>
          <select
            class="field-control payment-form-select"
            [(ngModel)]="model.paymentFormSat"
            name="paymentFormSat"
            [title]="selectedPaymentFormLabel()"
            [disabled]="loading() || loadingCatalog() || !paymentFormOptions().length"
            required>
            <option value="">Selecciona forma de pago</option>
            @for (option of paymentFormOptions(); track option.code) {
              <option [value]="option.code" [title]="option.code + ' - ' + option.description">
                {{ option.code }} - {{ option.description }}
              </option>
            }
          </select>
          @if (loadingCatalog()) {
            <small class="helper">Cargando catálogo SAT...</small>
          } @else if (catalogError()) {
            <small class="helper error">{{ catalogError() }}</small>
          } 
        </label>

        <label><span>Monto</span><input class="field-control" [(ngModel)]="model.amount" name="amount" type="number" min="0.01" step="0.01" required /></label>
        <label><span>Referencia</span><input class="field-control" [(ngModel)]="model.reference" name="reference" /></label>
        <label><span>Notas</span><input class="field-control" [(ngModel)]="model.notes" name="notes" /></label>
        <button type="submit" [disabled]="loading() || loadingCatalog() || !canSubmit()"> {{ loading() ? 'Guardando...' : 'Crear pago' }} </button>
      </form>
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; min-width:0; }
    input, select, button { font:inherit; }
    .field-control { width:100%; min-width:0; border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; box-sizing:border-box; }
    .payment-form-select { max-width:100%; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .helper { margin:0; color:#5f6b76; font-size:0.82rem; }
    .helper.error { color:#7a2020; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentCreateFormComponent {
  private readonly fiscalReceiverSatCatalogService = inject(FiscalReceiverSatCatalogService);

  readonly loading = input(false);
  readonly submit = output<CreateAccountsReceivablePaymentRequest>();

  protected readonly loadingCatalog = signal(true);
  protected readonly catalogError = signal<string | null>(null);

  protected readonly model: CreateAccountsReceivablePaymentRequest = {
    paymentDateUtc: formatDateTimeLocalValue(new Date()),
    paymentFormSat: '03',
    amount: 0,
    reference: '',
    notes: ''
  };

  protected readonly paymentFormOptions = computed(() =>
    this.paymentFormCatalog().filter((option) => option.code !== '99'));

  protected readonly selectedPaymentFormLabel = computed(() => {
    const selected = this.paymentFormOptions().find((option) => option.code === this.model.paymentFormSat);
    return selected ? `${selected.code} - ${selected.description}` : '';
  });

  private readonly paymentFormCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);

  constructor() {
    void this.loadSatCatalog();
  }

  protected handleSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }

    this.submit.emit({
      ...this.model,
      paymentFormSat: this.model.paymentFormSat.trim().toUpperCase()
    });
  }

  protected canSubmit(): boolean {
    return !!this.model.paymentDateUtc
      && this.model.amount > 0
      && this.paymentFormOptions().some((option) => option.code === this.model.paymentFormSat);
  }

  private async loadSatCatalog(): Promise<void> {
    this.loadingCatalog.set(true);
    this.catalogError.set(null);

    try {
      const catalog = await firstValueFrom(this.fiscalReceiverSatCatalogService.getCatalog());
      this.paymentFormCatalog.set(catalog.paymentForms ?? []);

      if (!this.paymentFormOptions().some((option) => option.code === this.model.paymentFormSat)) {
        this.model.paymentFormSat = this.paymentFormOptions()[0]?.code ?? '';
      }
    } catch {
      this.paymentFormCatalog.set([]);
      this.catalogError.set('No se pudo cargar el catálogo SAT de formas de pago.');
      this.model.paymentFormSat = '';
    } finally {
      this.loadingCatalog.set(false);
    }
  }
}
