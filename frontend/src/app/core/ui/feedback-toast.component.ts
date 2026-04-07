import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { FeedbackMessage } from './feedback.service';

@Component({
  selector: 'app-feedback-toast',
  imports: [NgClass],
  template: `
    <article
      class="toast-card"
      [ngClass]="toast().level"
      [attr.role]="role()"
      [attr.aria-live]="ariaLive()"
      aria-atomic="true">
      <button
        type="button"
        class="toast-close"
        [attr.aria-label]="closeLabel()"
        (click)="dismiss.emit()">
        <span aria-hidden="true">X</span>
      </button>

      <p class="toast-level">{{ levelLabel() }}</p>
      <p class="toast-message">{{ toast().text }}</p>
    </article>
  `,
  styles: [`
    :host { display:block; }
    .toast-card {
      position: relative;
      display: grid;
      gap: 0.45rem;
      min-width: min(22rem, calc(100vw - 2rem));
      max-width: min(24rem, calc(100vw - 2rem));
      padding: 0.9rem 2.7rem 0.95rem 1rem;
      border: 1px solid transparent;
      border-inline-start-width: 0.35rem;
      border-radius: 1rem;
      background: rgba(255, 255, 255, 0.98);
      box-shadow: 0 14px 36px rgba(24, 37, 51, 0.16);
      color: #1d2a39;
      overflow-wrap: anywhere;
    }
    .toast-level {
      margin: 0;
      text-transform: uppercase;
      letter-spacing: 0.1em;
      font-size: 0.72rem;
      font-weight: 700;
    }
    .toast-message {
      margin: 0;
      line-height: 1.45;
      color: #324254;
    }
    .toast-close {
      position: absolute;
      top: 0.55rem;
      right: 0.55rem;
      width: 2rem;
      height: 2rem;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: none;
      border-radius: 999px;
      background: transparent;
      color: inherit;
      cursor: pointer;
    }
    .toast-close:hover,
    .toast-close:focus-visible {
      background: rgba(29, 42, 57, 0.08);
      outline: none;
    }
    .toast-card.success {
      border-color: #b8e3c0;
      border-inline-start-color: #2f8f4f;
      background: #f4fbf5;
    }
    .toast-card.info {
      border-color: #bfdcff;
      border-inline-start-color: #2f6fb8;
      background: #f2f8ff;
    }
    .toast-card.warning {
      border-color: #f0d49a;
      border-inline-start-color: #b17708;
      background: #fff9ec;
    }
    .toast-card.error {
      border-color: #efb1b1;
      border-inline-start-color: #c24a4a;
      background: #fff3f3;
    }
    @media (max-width: 767px) {
      .toast-card {
        min-width: 0;
        max-width: 100%;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedbackToastComponent {
  readonly toast = input.required<FeedbackMessage>();
  readonly dismiss = output<void>();

  protected readonly role = computed(() => this.toast().level === 'error' ? 'alert' : 'status');
  protected readonly ariaLive = computed(() => this.toast().level === 'error' ? 'assertive' : 'polite');
  protected readonly levelLabel = computed(() => getLevelLabel(this.toast().level));
  protected readonly closeLabel = computed(() => `Cerrar notificación de ${this.levelLabel().toLowerCase()}`);
}

function getLevelLabel(level: FeedbackMessage['level']): string {
  switch (level) {
    case 'success':
      return 'Éxito';
    case 'warning':
      return 'Advertencia';
    case 'error':
      return 'Error';
    case 'info':
    default:
      return 'Información';
  }
}
