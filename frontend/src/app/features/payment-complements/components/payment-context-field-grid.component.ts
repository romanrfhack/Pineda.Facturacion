import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface PaymentContextFieldItem {
  label: string;
  value: string | number | null | undefined;
  title?: string | null;
  wide?: boolean;
  mono?: boolean;
  subdued?: boolean;
}

@Component({
  selector: 'app-payment-context-field-grid',
  template: `
    <dl class="field-grid" [style.--field-min-width]="minColumnWidth() + 'px'">
      @for (field of fields(); track field.label + '-' + $index) {
        <div class="field-item" [class.wide]="field.wide">
          <dt>{{ field.label }}</dt>
          <dd
            [class.mono]="field.mono"
            [class.subdued]="field.subdued"
            [attr.title]="field.title || displayValue(field.value)"
          >
            {{ displayValue(field.value) }}
          </dd>
        </div>
      }
    </dl>
  `,
  styles: [
    `
      .field-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(var(--field-min-width, 160px), 1fr));
        gap: 0.75rem 1rem;
        margin: 0;
      }

      .field-item {
        min-width: 0;
        display: grid;
        gap: 0.2rem;
      }

      .field-item.wide {
        grid-column: 1 / -1;
      }

      dt {
        font-size: 0.78rem;
        color: #6b7784;
      }

      dd {
        margin: 0;
        color: #182533;
        font-weight: 600;
        word-break: break-word;
      }

      dd.mono {
        font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
        font-size: 0.85rem;
      }

      dd.subdued {
        color: #425466;
        font-weight: 500;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentContextFieldGridComponent {
  readonly fields = input.required<PaymentContextFieldItem[]>();
  readonly minColumnWidth = input(160);

  protected displayValue(value: string | number | null | undefined): string {
    if (value === null || value === undefined) {
      return '—';
    }

    if (typeof value === 'number') {
      return Number.isNaN(value) ? '—' : `${value}`;
    }

    const trimmed = value.trim();
    return trimmed ? trimmed : '—';
  }
}
