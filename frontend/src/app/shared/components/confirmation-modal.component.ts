import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-confirmation-modal',
  template: `
    @if (open()) {
      <section class="modal-backdrop" (click)="handleBackdropClick()">
        <section
          class="modal-card"
          (click)="$event.stopPropagation()"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="title()"
        >
          <header class="modal-header">
            <div>
              @if (eyebrow()) {
                <p class="eyebrow">{{ eyebrow() }}</p>
              }
              <h3>{{ title() }}</h3>
            </div>
          </header>

          <p class="message">{{ message() }}</p>

          <div class="actions">
            <button
              type="button"
              class="secondary"
              (click)="handleCancelClick()"
              [disabled]="busy()"
            >
              {{ cancelLabel() }}
            </button>
            <button
              type="button"
              [class.danger]="tone() === 'danger'"
              (click)="handleConfirmClick()"
              [disabled]="busy()"
            >
              {{ busy() ? busyConfirmLabel() || confirmLabel() : confirmLabel() }}
            </button>
          </div>
        </section>
      </section>
    }
  `,
  styles: [
    `
      .modal-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(24, 37, 51, 0.52);
        display: grid;
        place-items: center;
        padding: 1rem;
        z-index: 60;
      }
      .modal-card {
        width: min(520px, 100%);
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        background: #fff;
        padding: 1rem;
        display: grid;
        gap: 1rem;
        box-shadow: 0 24px 60px rgba(24, 37, 51, 0.24);
      }
      .modal-header {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: flex-start;
      }
      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 0.72rem;
        color: #8a6a32;
      }
      h3 {
        margin: 0.3rem 0 0;
      }
      .message {
        margin: 0;
        color: #243444;
        line-height: 1.5;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
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
        font: inherit;
      }
      button.secondary {
        background: #d8c49b;
        color: #182533;
      }
      button.danger {
        background: #7a2020;
        color: #fff;
      }
      button:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      @media (max-width: 720px) {
        .actions {
          flex-direction: column-reverse;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmationModalComponent {
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input('Confirmar');
  readonly cancelLabel = input('Cancelar');
  readonly busyConfirmLabel = input<string | null>(null);
  readonly eyebrow = input<string | null>(null);
  readonly tone = input<'default' | 'danger'>('default');
  readonly busy = input(false);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected handleBackdropClick(): void {
    if (this.busy()) {
      return;
    }

    this.cancelled.emit();
  }

  protected handleCancelClick(): void {
    if (this.busy()) {
      return;
    }

    this.cancelled.emit();
  }

  protected handleConfirmClick(): void {
    if (this.busy()) {
      return;
    }

    this.confirmed.emit();
  }
}
