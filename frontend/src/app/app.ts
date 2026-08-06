import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FeedbackToastContainerComponent } from './core/ui/feedback-toast-container.component';
import { PinedaLoaderOverlayComponent } from './core/ui/pineda-loader-overlay.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, FeedbackToastContainerComponent, PinedaLoaderOverlayComponent],
  template: `
    <router-outlet />
    <app-feedback-toast-container />
    <app-pineda-loader-overlay />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {}
