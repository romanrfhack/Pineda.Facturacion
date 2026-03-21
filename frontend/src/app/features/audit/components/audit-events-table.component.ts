import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AuditEventItem } from '../models/audit.models';
import { getDisplayLabel } from '../../../shared/ui/display-labels';

@Component({
  selector: 'app-audit-events-table',
  imports: [DatePipe],
  template: `
    @if (!events().length) {
      <p class="helper">No hay eventos de auditoría que coincidan con los filtros actuales.</p>
    } @else {
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Ocurrió</th>
              <th>Actor</th>
              <th>Acción</th>
              <th>Entidad</th>
              <th>Resultado</th>
              <th>Correlación</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (event of events(); track event.id) {
              <tr>
                <td>{{ event.occurredAtUtc | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ event.actorUsername || 'Anónimo' }}</td>
                <td>{{ event.actionType }}</td>
                <td>{{ event.entityType }} {{ event.entityId || '' }}</td>
                <td><span class="outcome">{{ getDisplayLabel(event.outcome) }}</span></td>
                <td>{{ event.correlationId }}</td>
                <td><button type="button" class="link" (click)="selected.emit(event)">Detalle</button></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: [`
    .helper { margin:0; color:#5f6b76; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; vertical-align:top; }
    .link { background:transparent; border:none; padding:0; color:#182533; cursor:pointer; font:inherit; }
    .outcome { display:inline-flex; padding:0.25rem 0.55rem; border-radius:999px; background:#eef6ff; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AuditEventsTableComponent {
  readonly events = input<AuditEventItem[]>([]);
  readonly selected = output<AuditEventItem>();
  protected readonly getDisplayLabel = getDisplayLabel;
}
