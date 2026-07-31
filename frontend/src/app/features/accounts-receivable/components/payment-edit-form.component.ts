import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { FiscalReceiverSatCatalogService } from '../../catalogs/application/fiscal-receiver-sat-catalog.service';
import { FiscalReceiverSatCatalogOption } from '../../catalogs/models/catalogs.models';
import {
  AccountsReceivablePaymentResponse,
  UpdateAccountsReceivablePaymentRequest,
} from '../models/accounts-receivable.models';
import { formatDateTimeLocalValue } from './payment-create-form.component';

@Component({
  selector: 'app-payment-edit-form',
  imports: [FormsModule],
  template: `
    <section class="edit-panel" aria-labelledby="payment-edit-title">
      <div class="panel-heading">
        <div>
          <p class="eyebrow">Corrección controlada</p>
          <h4 id="payment-edit-title">Editar datos del pago</h4>
        </div>
        <p class="helper">Sólo se permiten cambios mientras el pago no tenga un REP asociado.</p>
      </div>

      <form #paymentEditForm="ngForm" (ngSubmit)="handleSubmit()">
        <div class="form-grid">
          <label>
            <span>Fecha de pago</span>
            <input
              class="field-control"
              data-testid="detail-payment-date-input"
              [(ngModel)]="model.paymentDateUtc"
              name="paymentDateUtc"
              type="datetime-local"
              required
            />
          </label>

          <label>
            <span>Forma de pago SAT</span>
            <select
              class="field-control"
              data-testid="detail-payment-form-select"
              [(ngModel)]="model.paymentFormSat"
              name="paymentFormSat"
              [disabled]="loading() || loadingCatalog() || !paymentFormOptions().length"
              required
            >
              <option value="">Selecciona forma de pago</option>
              @for (option of paymentFormOptions(); track option.code) {
                <option [value]="option.code">
                  {{ option.code }} - {{ option.description }}
                </option>
              }
            </select>
          </label>

          <label>
            <span>Importe</span>
            <input
              class="field-control"
              data-testid="detail-payment-amount-input"
              [(ngModel)]="model.amount"
              name="amount"
              type="number"
              min="0.01"
              step="0.01"
              [disabled]="!amountEditable()"
              required
            />
            @if (!amountEditable()) {
              <small class="field-helper">
                El importe está bloqueado porque el pago ya tiene aplicaciones.
              </small>
            }
          </label>

          <label>
            <span>Referencia</span>
            <input
              class="field-control"
              data-testid="detail-payment-reference-input"
              [(ngModel)]="model.reference"
              name="reference"
              maxlength="100"
            />
          </label>

          <label class="notes-field">
            <span>Notas</span>
            <textarea
              class="field-control"
              data-testid="detail-payment-notes-input"
              [(ngModel)]="model.notes"
              name="notes"
              maxlength="1000"
              rows="3"
            ></textarea>
          </label>
        </div>

        @if (catalogError()) {
          <p class="helper error" role="alert">{{ catalogError() }}</p>
        }

        <div class="actions">
          <button
            type="submit"
            data-testid="detail-payment-save-button"
            [disabled]="loading() || loadingCatalog() || !canSubmit()"
          >
            {{ loading() ? 'Guardando...' : 'Guardar cambios' }}
          </button>
          <button
            type="button"
            class="secondary"
            data-testid="detail-payment-cancel-button"
            (click)="cancelled.emit()"
            [disabled]="loading()"
          >
            Cancelar
          </button>
        </div>
      </form>
    </section>
  `,
  styles: [
    `
      .edit-panel {
        margin-top: 1rem;
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        padding: 1rem;
        background: #fffdf8;
        display: grid;
        gap: 1rem;
      }

      .panel-heading {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
      }

      .eyebrow {
        margin: 0 0 0.25rem;
        color: #8a6a32;
        font-size: 0.72rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }

      h4,
      .helper {
        margin: 0;
      }

      form {
        display: grid;
        gap: 1rem;
      }

      .form-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
        gap: 1rem;
        align-items: start;
      }

      label {
        display: grid;
        gap: 0.35rem;
        min-width: 0;
      }

      .notes-field {
        grid-column: 1 / -1;
      }

      input,
      select,
      textarea,
      button {
        font: inherit;
      }

      .field-control {
        width: 100%;
        min-width: 0;
        box-sizing: border-box;
        border: 1px solid #c9d1da;
        border-radius: 0.8rem;
        padding: 0.75rem 0.9rem;
        background: #fff;
      }

      .field-control:disabled {
        background: #eef1f4;
        color: #5f6b76;
      }

      textarea {
        resize: vertical;
      }

      .helper,
      .field-helper {
        color: #5f6b76;
        font-size: 0.82rem;
      }

      .helper.error {
        color: #7a2020;
      }

      .actions {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
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
        background: #eef1f4;
        color: #182533;
      }

      button:disabled {
        opacity: 0.58;
        cursor: not-allowed;
      }

      button:focus-visible,
      .field-control:focus-visible {
        outline: 2px solid #8a6a32;
        outline-offset: 2px;
      }

      @media (max-width: 720px) {
        .panel-heading,
        .actions {
          flex-direction: column;
          align-items: stretch;
        }

        .actions button {
          width: 100%;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentEditFormComponent implements OnInit {
  private readonly fiscalReceiverSatCatalogService = inject(FiscalReceiverSatCatalogService);

  readonly payment = input.required<AccountsReceivablePaymentResponse>();
  readonly amountEditable = input(true);
  readonly loading = input(false);
  readonly saved = output<UpdateAccountsReceivablePaymentRequest>();
  readonly cancelled = output<void>();

  protected readonly loadingCatalog = signal(true);
  protected readonly catalogError = signal<string | null>(null);
  protected readonly paymentFormCatalog = signal<FiscalReceiverSatCatalogOption[]>([]);
  protected readonly paymentFormOptions = computed(() =>
    this.paymentFormCatalog().filter((option) => option.code !== '99'),
  );
  protected readonly model: UpdateAccountsReceivablePaymentRequest = {
    paymentDateUtc: '',
    paymentFormSat: '',
    amount: 0,
    reference: '',
    notes: '',
  };

  ngOnInit(): void {
    const payment = this.payment();
    this.model.paymentDateUtc = formatDateTimeLocalValue(new Date(payment.paymentDateUtc));
    this.model.paymentFormSat = payment.paymentFormSat;
    this.model.amount = payment.amount;
    this.model.reference = payment.reference ?? '';
    this.model.notes = payment.notes ?? '';
    void this.loadSatCatalog();
  }

  protected handleSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }

    this.saved.emit({
      paymentDateUtc: this.model.paymentDateUtc,
      paymentFormSat: this.model.paymentFormSat.trim().toUpperCase(),
      amount: this.model.amount,
      reference: this.model.reference,
      notes: this.model.notes,
    });
  }

  protected canSubmit(): boolean {
    return (
      !!this.model.paymentDateUtc &&
      this.model.amount > 0 &&
      this.paymentFormOptions().some((option) => option.code === this.model.paymentFormSat)
    );
  }

  private async loadSatCatalog(): Promise<void> {
    this.loadingCatalog.set(true);
    this.catalogError.set(null);

    try {
      const catalog = await firstValueFrom(this.fiscalReceiverSatCatalogService.getCatalog());
      this.paymentFormCatalog.set(catalog.paymentForms ?? []);
      if (!this.paymentFormOptions().some((option) => option.code === this.model.paymentFormSat)) {
        this.catalogError.set(
          `La forma de pago actual '${this.model.paymentFormSat}' no está disponible en el catálogo SAT.`,
        );
      }
    } catch {
      this.paymentFormCatalog.set([]);
      this.catalogError.set('No se pudo cargar el catálogo SAT de formas de pago.');
    } finally {
      this.loadingCatalog.set(false);
    }
  }
}
