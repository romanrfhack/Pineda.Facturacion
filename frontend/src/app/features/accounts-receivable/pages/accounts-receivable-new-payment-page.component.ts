import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivableReceiverWorkspaceResponse,
  CreateAccountsReceivablePaymentRequest,
} from '../models/accounts-receivable.models';
import { PaymentCreateFormComponent } from '../components/payment-create-form.component';

@Component({
  selector: 'app-accounts-receivable-new-payment-page',
  imports: [RouterLink, CurrencyPipe, PaymentCreateFormComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Cuentas por cobrar</p>
        <h2>Nuevo pago</h2>
        <p class="helper">
          Registra un pago recibido y, cuando quede guardado, se abrirá automáticamente su vista de
          aplicación.
        </p>
      </header>

      <section class="card">
        <div class="section-head compact">
          <div>
            <h3>Captura del pago</h3>
            @if (invoice(); as currentInvoice) {
              <p class="helper">
                Cuenta #{{ currentInvoice.id }} · CFDI
                {{ formatInvoiceFiscalLabel(currentInvoice) }}
              </p>
            } @else if (receiverWorkspace(); as workspace) {
              <p class="helper">Receptor {{ workspace.legalName }} · RFC {{ workspace.rfc }}</p>
            } @else {
              <p class="helper">Registra el pago con los datos operativos actuales.</p>
            }
          </div>
          <a
            class="secondary"
            [routerLink]="['/app/accounts-receivable']"
            [queryParams]="backQueryParams()"
            >Volver</a
          >
        </div>

        @if (invoice(); as currentInvoice) {
          <div class="summary-grid">
            <article class="summary-card">
              <strong>Receptor</strong>
              <div class="subtle">{{ currentInvoice.receiverLegalName || 'Sin receptor' }}</div>
              <div class="subtle">{{ currentInvoice.receiverRfc || 'Sin RFC' }}</div>
            </article>
            <article class="summary-card">
              <strong>Saldo pendiente</strong>
              <div class="subtle">
                {{ currentInvoice.outstandingBalance | currency: 'MXN' : 'symbol' : '1.2-2' }}
              </div>
            </article>
          </div>
        } @else if (receiverWorkspace(); as workspace) {
          <div class="summary-grid">
            <article class="summary-card">
              <strong>Receptor</strong>
              <div class="subtle">{{ workspace.legalName }}</div>
              <div class="subtle">{{ workspace.rfc }}</div>
            </article>
            <article class="summary-card">
              <strong>Saldo pendiente operativo</strong>
              <div class="subtle">
                {{ workspace.summary.pendingBalanceTotal | currency: 'MXN' : 'symbol' : '1.2-2' }}
              </div>
            </article>
          </div>
        }

        <app-payment-create-form
          [loading]="loading()"
          [maxOperationalAmount]="maxOperationalAmount()"
          (submit)="createPayment($event)"
        />
      </section>
    </section>
  `,
  styles: [
    `
      .page {
        display: grid;
        gap: 1rem;
      }
      .card {
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        padding: 1rem;
        background: #fff;
      }
      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 0.72rem;
        color: #8a6a32;
      }
      h2,
      h3 {
        margin: 0;
      }
      .helper {
        color: #5f6b76;
        margin: 0.35rem 0 0;
      }
      .section-head {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: flex-end;
        margin-bottom: 1rem;
      }
      .section-head.compact {
        margin-bottom: 0.75rem;
      }
      a {
        border: none;
        border-radius: 0.8rem;
        padding: 0.75rem 1rem;
        background: #182533;
        color: #fff;
        cursor: pointer;
        text-decoration: none;
        display: inline-flex;
        justify-content: center;
      }
      a.secondary {
        background: #eef1f4;
        color: #182533;
      }
      .summary-grid {
        display: grid;
        gap: 0.75rem;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        margin-bottom: 1rem;
      }
      .summary-card {
        border: 1px solid #ece3d3;
        border-radius: 0.85rem;
        padding: 0.85rem;
        background: #fffdf8;
        display: grid;
        gap: 0.35rem;
      }
      .subtle {
        color: #71808f;
        font-size: 0.84rem;
      }
      @media (max-width: 720px) {
        .section-head {
          flex-direction: column;
          align-items: stretch;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountsReceivableNewPaymentPageComponent {
  private readonly api = inject(AccountsReceivableApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedbackService = inject(FeedbackService);

  protected readonly loading = signal(false);
  protected readonly invoice = signal<AccountsReceivableInvoiceResponse | null>(null);
  protected readonly receiverWorkspace = signal<AccountsReceivableReceiverWorkspaceResponse | null>(
    null,
  );
  protected readonly invoiceId = signal<number | null>(
    parseNumber(this.route.snapshot.queryParamMap.get('invoiceId')),
  );
  protected readonly fiscalDocumentId = signal<number | null>(
    parseNumber(this.route.snapshot.queryParamMap.get('fiscalDocumentId')),
  );
  protected readonly fiscalReceiverId = signal<number | null>(
    parseNumber(this.route.snapshot.queryParamMap.get('fiscalReceiverId')),
  );
  protected readonly maxOperationalAmount = computed(
    () =>
      this.invoice()?.outstandingBalance ??
      this.receiverWorkspace()?.summary.pendingBalanceTotal ??
      null,
  );

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.invoiceId.set(parseNumber(params.get('invoiceId')));
      this.fiscalDocumentId.set(parseNumber(params.get('fiscalDocumentId')));
      this.fiscalReceiverId.set(parseNumber(params.get('fiscalReceiverId')));
      void this.loadContext();
    });
  }

  protected formatInvoiceFiscalLabel(item: AccountsReceivableInvoiceResponse): string {
    const series = item.fiscalSeries?.trim();
    const folio = item.fiscalFolio?.trim();
    if (series || folio) {
      return [series, folio].filter(Boolean).join('-');
    }

    if (item.fiscalDocumentId) {
      return `#${item.fiscalDocumentId}`;
    }

    return `#${item.id}`;
  }

  protected backQueryParams(): Record<string, number> {
    const currentInvoice = this.invoice();
    if (currentInvoice) {
      return { invoiceId: currentInvoice.id };
    }

    const fiscalReceiverId = this.receiverWorkspace()?.fiscalReceiverId ?? this.fiscalReceiverId();
    if (fiscalReceiverId) {
      return { fiscalReceiverId };
    }

    return {};
  }

  protected async createPayment(request: CreateAccountsReceivablePaymentRequest): Promise<void> {
    const currentInvoice = this.invoice();
    const currentReceiverWorkspace = this.receiverWorkspace();

    await this.run(async () => {
      const response = await firstValueFrom(
        this.api.createPayment({
          ...request,
          accountsReceivableInvoiceId: currentInvoice?.id ?? null,
          paymentDateUtc: new Date(request.paymentDateUtc).toISOString(),
          receivedFromFiscalReceiverId:
            currentInvoice?.fiscalReceiverId ??
            currentReceiverWorkspace?.fiscalReceiverId ??
            this.fiscalReceiverId(),
        }),
      );

      if (!response.payment) {
        return;
      }

      this.feedbackService.show('success', 'Pago registrado.');
      const detailQueryParams = currentInvoice
        ? { paymentId: response.payment.id, invoiceId: currentInvoice.id }
        : { paymentId: response.payment.id };
      await this.router.navigate(['/app/accounts-receivable'], {
        queryParams: detailQueryParams,
      });
    });
  }

  private async loadContext(): Promise<void> {
    await this.run(
      async () => {
        this.invoice.set(null);
        this.receiverWorkspace.set(null);

        const invoiceId = this.invoiceId();
        const fiscalDocumentId = this.fiscalDocumentId();
        const fiscalReceiverId = this.fiscalReceiverId();

        if (invoiceId !== null) {
          this.invoice.set(await firstValueFrom(this.api.getInvoiceById(invoiceId)));
          return;
        }

        if (fiscalDocumentId !== null) {
          this.invoice.set(
            await firstValueFrom(this.api.getInvoiceByFiscalDocumentId(fiscalDocumentId)),
          );
          return;
        }

        if (fiscalReceiverId !== null) {
          this.receiverWorkspace.set(
            await firstValueFrom(this.api.getReceiverWorkspace(fiscalReceiverId)),
          );
        }
      },
      { silenceErrors: true },
    );
  }

  private async run(
    operation: () => Promise<void>,
    options: { silenceErrors?: boolean } = {},
  ): Promise<void> {
    this.loading.set(true);
    try {
      await operation();
    } catch (error) {
      if (!options.silenceErrors) {
        this.feedbackService.show('error', extractApiErrorMessage(error));
      }
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
