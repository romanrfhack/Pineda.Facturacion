import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import {
  PendingFiscalDocumentListResponse,
  PendingFiscalDocumentSearchRequest,
} from '../models/pending-fiscal-documents.models';

@Injectable({ providedIn: 'root' })
export class PendingFiscalDocumentsApiService {
  private readonly http = inject(HttpClient);

  search(request: PendingFiscalDocumentSearchRequest): Observable<PendingFiscalDocumentListResponse> {
    const query = new URLSearchParams();
    query.set('page', `${request.page}`);
    query.set('pageSize', `${request.pageSize}`);
    query.set('workStatus', request.workStatus);
    query.set('sort', request.sort);

    if (request.query?.trim()) {
      query.set('query', request.query.trim());
    }

    return this.http.get<PendingFiscalDocumentListResponse>(
      buildApiUrl(`/fiscal-documents/pending-stamping?${query.toString()}`),
    );
  }
}
