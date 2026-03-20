import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditEventFilters } from '../models/audit.models';

@Component({
  selector: 'app-audit-filters',
  imports: [FormsModule],
  template: `
    <form class="form-grid" (ngSubmit)="submitFilters()">
      <label><span>Actor</span><input [(ngModel)]="draft.actorUsername" name="actorUsername" /></label>
      <label><span>Action type</span><input [(ngModel)]="draft.actionType" name="actionType" /></label>
      <label><span>Entity type</span><input [(ngModel)]="draft.entityType" name="entityType" /></label>
      <label><span>Entity id</span><input [(ngModel)]="draft.entityId" name="entityId" /></label>
      <label><span>Outcome</span><input [(ngModel)]="draft.outcome" name="outcome" /></label>
      <label><span>Correlation id</span><input [(ngModel)]="draft.correlationId" name="correlationId" /></label>
      <label><span>From UTC</span><input [(ngModel)]="draft.fromUtc" name="fromUtc" type="datetime-local" /></label>
      <label><span>To UTC</span><input [(ngModel)]="draft.toUtc" name="toUtc" type="datetime-local" /></label>
      <label><span>Page size</span><input [(ngModel)]="draft.pageSize" name="pageSize" type="number" min="1" max="100" /></label>
      <div class="actions">
        <button type="submit">Apply filters</button>
        <button type="button" class="secondary" (click)="clearFilters()">Clear</button>
      </div>
    </form>
  `,
  styles: [`
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .actions { display:flex; gap:0.75rem; flex-wrap:wrap; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuditFiltersComponent {
  readonly filters = input<AuditEventFilters>({
    page: 1,
    pageSize: 25
  });
  readonly submitted = output<AuditEventFilters>();
  readonly cleared = output<void>();

  protected draft: AuditEventFilters = { page: 1, pageSize: 25 };

  ngOnChanges(): void {
    this.draft = { ...this.filters() };
  }

  protected submitFilters(): void {
    this.submitted.emit({
      ...this.draft,
      page: 1
    });
  }

  protected clearFilters(): void {
    this.draft = { page: 1, pageSize: 25 };
    this.cleared.emit();
  }
}
