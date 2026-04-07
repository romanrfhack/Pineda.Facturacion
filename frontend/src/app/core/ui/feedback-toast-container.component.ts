import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FeedbackService } from './feedback.service';
import { FeedbackToastComponent } from './feedback-toast.component';

@Component({
  selector: 'app-feedback-toast-container',
  imports: [FeedbackToastComponent],
  template: `
    @if (feedbackService.toasts().length) {
      <section class="toast-region" aria-label="Notificaciones globales">
        @for (toast of feedbackService.toasts(); track toast.id) {
          <div class="toast-slot" animate.enter="toast-enter" animate.leave="toast-leave">
            <app-feedback-toast [toast]="toast" (dismiss)="feedbackService.dismiss(toast.id)" />
          </div>
        }
      </section>
    }
  `,
  styles: [`
    .toast-region {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 60;
      display: grid;
      gap: 0.75rem;
      justify-items: end;
      pointer-events: none;
    }
    .toast-slot {
      pointer-events: auto;
    }
    .toast-enter {
      animation: toast-enter 180ms ease-out;
    }
    .toast-leave {
      animation: toast-leave 160ms ease-in forwards;
    }
    @keyframes toast-enter {
      from {
        opacity: 0;
        transform: translate3d(0, -0.5rem, 0);
      }
      to {
        opacity: 1;
        transform: translate3d(0, 0, 0);
      }
    }
    @keyframes toast-leave {
      from {
        opacity: 1;
        transform: translate3d(0, 0, 0);
      }
      to {
        opacity: 0;
        transform: translate3d(0, -0.35rem, 0);
      }
    }
    @media (max-width: 767px) {
      .toast-region {
        top: 0.75rem;
        right: 0.75rem;
        left: 0.75rem;
        justify-items: stretch;
      }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedbackToastContainerComponent {
  protected readonly feedbackService = inject(FeedbackService);
}
