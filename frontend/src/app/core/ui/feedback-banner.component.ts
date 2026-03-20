import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { FeedbackService } from './feedback.service';

@Component({
  selector: 'app-feedback-banner',
  imports: [NgClass],
  template: `
    @if (message(); as current) {
      <div class="banner" [ngClass]="current.level">
        <span>{{ current.text }}</span>
        <button type="button" (click)="dismiss()">Dismiss</button>
      </div>
    }
  `,
  styles: [`
    .banner { display:flex; justify-content:space-between; gap:1rem; padding:0.75rem 1rem; border-radius:0.75rem; margin-bottom:1rem; border:1px solid transparent; }
    .banner.info { background:#eef6ff; border-color:#bfdcff; color:#10345a; }
    .banner.success { background:#eefbf1; border-color:#b8e3c0; color:#153f22; }
    .banner.warning { background:#fff8e8; border-color:#f3d58c; color:#5d4300; }
    .banner.error { background:#fff0f0; border-color:#efb1b1; color:#6a1717; }
    button { background:transparent; border:none; font:inherit; cursor:pointer; color:inherit; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FeedbackBannerComponent {
  private readonly feedbackService = inject(FeedbackService);
  protected readonly message = computed(() => this.feedbackService.message());

  protected dismiss(): void {
    this.feedbackService.clear();
  }
}
