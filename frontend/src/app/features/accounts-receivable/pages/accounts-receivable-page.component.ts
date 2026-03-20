import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivablePaymentResponse,
  ApplyAccountsReceivablePaymentRequest,
  CreateAccountsReceivablePaymentRequest
} from '../models/accounts-receivable.models';
import { AccountsReceivableCardComponent } from '../components/accounts-receivable-card.component';
import { PaymentCreateFormComponent } from '../components/payment-create-form.component';
import { PaymentApplicationFormComponent } from '../components/payment-application-form.component';

@Component({
  selector: 'app-accounts-receivable-page',
  imports: [RouterLink, AccountsReceivableCardComponent, PaymentCreateFormComponent, PaymentApplicationFormComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Accounts receivable and payments</p>
        <h2>Create AR invoices, record payments, and apply balances</h2>
      </header>

      @if (fiscalDocumentId()) {
        <section class="card">
          <h3>AR invoice from fiscal document #{{ fiscalDocumentId() }}</h3>
          @if (permissionService.canManagePayments()) {
            <button type="button" (click)="createInvoice()" [disabled]="loading()">Create AR invoice</button>
          }
        </section>
      }

      @if (invoice(); as currentInvoice) {
        <app-accounts-receivable-card [invoice]="currentInvoice" />
      }

      @if (permissionService.canManagePayments()) {
        <app-payment-create-form [loading]="loading()" (submit)="createPayment($event)" />
      }

      @if (payment(); as currentPayment) {
        <section class="card">
          <h3>Payment #{{ currentPayment.id }}</h3>
          <p class="helper">Amount {{ currentPayment.amount }} MXN · Remaining {{ currentPayment.remainingAmount }} MXN</p>
          @if (currentPayment.applications.length) {
            <table class="applications">
              <thead>
                <tr>
                  <th>AR invoice</th>
                  <th>Applied</th>
                  <th>Previous balance</th>
                  <th>New balance</th>
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
              <a [routerLink]="['/app/payment-complements']" [queryParams]="{ paymentId: currentPayment.id }">Open payment complement flow</a>
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
    h2 { margin:0.3rem 0 0; }
    .helper { color:#5f6b76; }
    button, a { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; }
    .links { margin-top:1rem; }
    .applications { width:100%; border-collapse:collapse; margin:1rem 0; }
    .applications th, .applications td { text-align:left; padding:0.6rem; border-top:1px solid #ece3d3; }
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
  protected readonly invoice = signal<AccountsReceivableInvoiceResponse | null>(null);
  protected readonly payment = signal<AccountsReceivablePaymentResponse | null>(null);
  protected readonly loading = signal(false);

  constructor() {
    if (this.fiscalDocumentId()) {
      void this.loadInvoice(this.fiscalDocumentId()!);
    }
    if (this.paymentId()) {
      void this.loadPayment(this.paymentId()!);
    }
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
        this.feedbackService.show('success', 'Accounts receivable invoice created.');
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
        this.feedbackService.show('success', 'Payment recorded.');
      }
    });
  }

  protected async applyPayment(request: ApplyAccountsReceivablePaymentRequest): Promise<void> {
    const currentPayment = this.payment();
    if (!currentPayment) {
      this.feedbackService.show('error', 'Create or load a payment before applying it.');
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

      this.feedbackService.show('success', 'Payment application recorded.');
    });
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

function extractErrorMessage(error: unknown): string {
  if (typeof error === 'object' && error && 'error' in error) {
    const payload = (error as { error?: { errorMessage?: string } }).error;
    if (payload?.errorMessage) {
      return payload.errorMessage;
    }
  }

  return 'The operation could not be completed.';
}
