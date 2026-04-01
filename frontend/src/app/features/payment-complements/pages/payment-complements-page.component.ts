import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PaymentComplementBaseDocumentsHubPageComponent } from './payment-complement-base-documents-hub-page.component';
import { PaymentComplementOperationsPageComponent } from './payment-complement-operations-page.component';

@Component({
  selector: 'app-payment-complements-page',
  imports: [PaymentComplementBaseDocumentsHubPageComponent, PaymentComplementOperationsPageComponent],
  template: `
    @if (paymentId()) {
      <app-payment-complement-operations-page />
    } @else {
      <app-payment-complement-base-documents-hub-page />
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementsPageComponent {
  private readonly route = inject(ActivatedRoute);

  protected readonly paymentId = signal<number | null>(parseNumber(this.route.snapshot.queryParamMap.get('paymentId')));
}

function parseNumber(value: string | null): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
