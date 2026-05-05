import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { StampedLegacyNotesReportFilters, StampedLegacyNotesReportResponse } from '../models/stamped-legacy-notes-report.models';

@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);

  searchStampedLegacyNotes(filters: StampedLegacyNotesReportFilters): Observable<StampedLegacyNotesReportResponse> {
    return this.http.get<StampedLegacyNotesReportResponse>(buildApiUrl('/reports/stamped-legacy-notes'), {
      params: buildParams(filters, true)
    });
  }

  exportStampedLegacyNotes(filters: StampedLegacyNotesReportFilters): Observable<HttpResponse<Blob>> {
    return this.http.get(buildApiUrl('/reports/stamped-legacy-notes/export'), {
      params: buildParams(filters, false),
      observe: 'response',
      responseType: 'blob'
    });
  }
}

function buildParams(filters: StampedLegacyNotesReportFilters, includePaging: boolean): HttpParams {
  let params = new HttpParams()
    .set('fromDate', filters.fromDate)
    .set('toDate', filters.toDate);

  if (includePaging) {
    params = params
      .set('page', String(filters.page))
      .set('pageSize', String(filters.pageSize));
  }

  params = append(params, 'receiverSearch', filters.receiverSearch);
  params = append(params, 'uuid', filters.uuid);
  params = append(params, 'series', filters.series);
  params = append(params, 'folio', filters.folio);
  params = append(params, 'legacyOrderId', filters.legacyOrderId);
  params = append(params, 'legacyOrderNumber', filters.legacyOrderNumber);

  return params;
}

function append(params: HttpParams, key: string, value?: string | null): HttpParams {
  return value && value.trim() ? params.set(key, value.trim()) : params;
}
