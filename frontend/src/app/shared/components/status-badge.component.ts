import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgClass } from '@angular/common';
import { getDisplayLabel } from '../ui/display-labels';

@Component({
  selector: 'app-status-badge',
  imports: [NgClass],
  template: `<span class="badge" [ngClass]="tone()">{{ displayLabel() }}</span>`,
  styles: [`
    .badge { display:inline-flex; padding:0.25rem 0.6rem; border-radius:999px; font-size:0.8rem; font-weight:600; border:1px solid transparent; }
    .neutral { background:#f2efe7; border-color:#ddd2bc; color:#5a4d35; }
    .success { background:#eefbf1; border-color:#a8dcb3; color:#215b31; }
    .warning { background:#fff7e8; border-color:#efd18c; color:#725000; }
    .danger { background:#fff0f0; border-color:#ebb1b1; color:#7a2020; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StatusBadgeComponent {
  readonly label = input.required<string>();
  readonly tone = input<'neutral' | 'success' | 'warning' | 'danger'>('neutral');
  protected readonly displayLabel = computed(() => getDisplayLabel(this.label()));
}
