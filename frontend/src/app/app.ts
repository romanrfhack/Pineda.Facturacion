import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FeedbackToastContainerComponent } from './core/ui/feedback-toast-container.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, FeedbackToastContainerComponent],
  template: `
    <router-outlet />
    <app-feedback-toast-container />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {}
