import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AuditEventItem } from '../models/audit.models';

@Component({
  selector: 'app-audit-event-detail',
  imports: [DatePipe],
  template: `
    @if (event(); as current) {
      <section class="card">
        <h3>Audit event detail</h3>
        <div class="grid">
          <p><strong>Occurred:</strong> {{ current.occurredAtUtc | date:'yyyy-MM-dd HH:mm:ss' }}</p>
          <p><strong>Actor:</strong> {{ current.actorUsername || 'Anonymous' }}</p>
          <p><strong>Action:</strong> {{ current.actionType }}</p>
          <p><strong>Entity:</strong> {{ current.entityType }} {{ current.entityId || '' }}</p>
          <p><strong>Outcome:</strong> {{ current.outcome }}</p>
          <p><strong>Correlation id:</strong> {{ current.correlationId }}</p>
          <p><strong>IP address:</strong> {{ current.ipAddress || 'N/A' }}</p>
          <p><strong>User agent:</strong> {{ current.userAgent || 'N/A' }}</p>
        </div>

        @if (current.errorMessage) {
          <div class="detail-block">
            <h4>Error message</h4>
            <pre>{{ current.errorMessage }}</pre>
          </div>
        }

        @if (current.requestSummaryJson) {
          <div class="detail-block">
            <h4>Request summary</h4>
            <pre>{{ current.requestSummaryJson }}</pre>
          </div>
        }

        @if (current.responseSummaryJson) {
          <div class="detail-block">
            <h4>Response summary</h4>
            <pre>{{ current.responseSummaryJson }}</pre>
          </div>
        }
      </section>
    }
  `,
  styles: [`
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.75rem 1rem; }
    .detail-block { margin-top:1rem; }
    h3, h4, p { margin:0; }
    pre { margin:0.35rem 0 0; padding:0.75rem; background:#f6f4ef; border-radius:0.8rem; white-space:pre-wrap; word-break:break-word; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuditEventDetailComponent {
  readonly event = input<AuditEventItem | null>(null);
}
