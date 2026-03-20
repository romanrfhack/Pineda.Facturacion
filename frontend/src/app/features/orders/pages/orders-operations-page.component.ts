import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { CreateBillingDocumentResponse, ImportLegacyOrderResponse } from '../models/orders.models';
import { BillingDocumentCardComponent } from '../components/billing-document-card.component';
import { FeedbackService } from '../../../core/ui/feedback.service';

@Component({
  selector: 'app-orders-operations-page',
  imports: [FormsModule, BillingDocumentCardComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Orders and billing preparation</p>
        <h2>Import a legacy order and start the fiscal path</h2>
      </header>

      <section class="card">
        <form class="form-grid" (ngSubmit)="importOrder()">
          <label>
            <span>Legacy order id</span>
            <input [(ngModel)]="legacyOrderId" name="legacyOrderId" placeholder="LEG-1001" required />
          </label>

          <label>
            <span>Billing document type</span>
            <select [(ngModel)]="documentType" name="documentType">
              <option value="I">Invoice</option>
            </select>
          </label>

          <div class="actions">
            <button type="submit" [disabled]="loadingImport()"> {{ loadingImport() ? 'Importing...' : 'Import order' }} </button>
            <button type="button" class="secondary" (click)="createBillingDocument()" [disabled]="!importedOrder() || loadingBilling()">
              {{ loadingBilling() ? 'Creating...' : 'Create billing document' }}
            </button>
          </div>
        </form>

        @if (localError()) {
          <p class="error">{{ localError() }}</p>
        }
      </section>

      @if (importedOrder(); as importedOrder) {
        <app-billing-document-card [imported]="importedOrder" [billing]="billingDocument()" />
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.3rem 0 0; }
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .actions { display:flex; gap:0.75rem; flex-wrap:wrap; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button:disabled { opacity:0.6; cursor:wait; }
    .error { color:#7a2020; margin:0.75rem 0 0; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrdersOperationsPageComponent {
  private readonly ordersApi = inject(OrdersApiService);
  private readonly feedbackService = inject(FeedbackService);

  protected legacyOrderId = '';
  protected documentType = 'I';
  protected readonly loadingImport = signal(false);
  protected readonly loadingBilling = signal(false);
  protected readonly localError = signal<string | null>(null);
  protected readonly importedOrder = signal<ImportLegacyOrderResponse | null>(null);
  protected readonly billingDocument = signal<CreateBillingDocumentResponse | null>(null);

  protected async importOrder(): Promise<void> {
    this.localError.set(null);
    this.billingDocument.set(null);
    this.loadingImport.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.importLegacyOrder(this.legacyOrderId.trim()));
      this.importedOrder.set(response);
      this.feedbackService.show('success', response.isIdempotent ? 'Order was already imported. Snapshot reused.' : 'Legacy order imported successfully.');
    } catch (error) {
      this.localError.set(extractErrorMessage(error));
    } finally {
      this.loadingImport.set(false);
    }
  }

  protected async createBillingDocument(): Promise<void> {
    const importedOrder = this.importedOrder();
    if (!importedOrder?.salesOrderId) {
      this.localError.set('Import a sales order before creating a billing document.');
      return;
    }

    this.localError.set(null);
    this.loadingBilling.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.createBillingDocument(importedOrder.salesOrderId, { documentType: this.documentType }));
      this.billingDocument.set(response);
      this.feedbackService.show('success', 'Billing document created.');
    } catch (error) {
      this.localError.set(extractErrorMessage(error));
    } finally {
      this.loadingBilling.set(false);
    }
  }
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
