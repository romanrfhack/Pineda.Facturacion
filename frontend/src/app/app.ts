import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FeedbackToastContainerComponent } from './core/ui/feedback-toast-container.component';
import { GlobalLoaderService } from './core/ui/global-loader.service';
import { PinedaLoaderOverlayComponent } from './core/ui/pineda-loader-overlay.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, FeedbackToastContainerComponent, PinedaLoaderOverlayComponent],
  template: `
    <div
      [attr.inert]="loader.active() ? '' : null"
      [attr.aria-hidden]="loader.active() ? 'true' : null">
      <router-outlet />
      <app-feedback-toast-container />
    </div>
    <app-pineda-loader-overlay />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {
  protected readonly loader = inject(GlobalLoaderService);
}
