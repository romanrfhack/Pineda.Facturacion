import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { AuditApiService } from '../infrastructure/audit-api.service';
import { AuditEventFilters, AuditEventItem, AuditEventListResponse } from '../models/audit.models';
import { AuditFiltersComponent } from '../components/audit-filters.component';
import { AuditEventsTableComponent } from '../components/audit-events-table.component';
import { AuditEventDetailComponent } from '../components/audit-event-detail.component';

@Component({
  selector: 'app-audit-events-page',
  imports: [AuditFiltersComponent, AuditEventsTableComponent, AuditEventDetailComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Audit</p>
        <h2>Read-only audit viewer</h2>
      </header>

      <section class="card">
        <app-audit-filters
          [filters]="filters()"
          (submitted)="applyFilters($event)"
          (cleared)="clearFilters()"
        />
      </section>

      <section class="card">
        @if (loading()) {
          <p class="helper">Loading audit events...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else {
          <p class="helper">Showing {{ events().length }} of {{ totalCount() }} events.</p>
          <app-audit-events-table [events]="events()" (selected)="selectEvent($event)" />
        }
      </section>

      <app-audit-event-detail [event]="selectedEvent()" />
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuditEventsPageComponent {
  private readonly api = inject(AuditApiService);
  private readonly feedbackService = inject(FeedbackService);

  protected readonly filters = signal<AuditEventFilters>({ page: 1, pageSize: 25 });
  protected readonly events = signal<AuditEventItem[]>([]);
  protected readonly selectedEvent = signal<AuditEventItem | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected async applyFilters(filters: AuditEventFilters): Promise<void> {
    this.filters.set(filters);
    await this.load();
  }

  protected async clearFilters(): Promise<void> {
    this.filters.set({ page: 1, pageSize: 25 });
    await this.load();
  }

  protected selectEvent(event: AuditEventItem): void {
    this.selectedEvent.set(event);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const response = await firstValueFrom(this.api.list(this.filters()));
      this.consumeResponse(response);
    } catch (error) {
      const message = extractApiErrorMessage(error, 'Audit events could not be loaded.');
      this.errorMessage.set(message);
      this.feedbackService.show('error', message);
      this.events.set([]);
      this.selectedEvent.set(null);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  private consumeResponse(response: AuditEventListResponse): void {
    this.events.set(response.items);
    this.totalCount.set(response.totalCount);
    this.selectedEvent.set(response.items[0] ?? null);
  }
}
