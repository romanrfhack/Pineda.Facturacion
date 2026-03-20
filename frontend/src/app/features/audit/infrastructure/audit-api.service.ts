import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { AuditEventFilters, AuditEventListResponse } from '../models/audit.models';

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private readonly http = inject(HttpClient);

  list(filters: AuditEventFilters): Observable<AuditEventListResponse> {
    let params = new HttpParams()
      .set('page', String(filters.page))
      .set('pageSize', String(filters.pageSize));

    params = append(params, 'actorUsername', filters.actorUsername);
    params = append(params, 'actionType', filters.actionType);
    params = append(params, 'entityType', filters.entityType);
    params = append(params, 'entityId', filters.entityId);
    params = append(params, 'outcome', filters.outcome);
    params = append(params, 'fromUtc', filters.fromUtc);
    params = append(params, 'toUtc', filters.toUtc);
    params = append(params, 'correlationId', filters.correlationId);

    return this.http.get<AuditEventListResponse>(buildApiUrl('/audit-events'), { params });
  }
}

function append(params: HttpParams, key: string, value?: string): HttpParams {
  return value && value.trim() ? params.set(key, value.trim()) : params;
}
