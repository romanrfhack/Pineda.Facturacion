import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { FiscalReceiver, FiscalReceiverSatCatalog, FiscalReceiverSearchItem, MutationResponse, UpsertFiscalReceiverRequest } from '../models/catalogs.models';

@Injectable({ providedIn: 'root' })
export class FiscalReceiversApiService {
  private readonly http = inject(HttpClient);

  search(query: string): Observable<FiscalReceiverSearchItem[]> {
    return this.http.get<FiscalReceiverSearchItem[]>(buildApiUrl(`/fiscal/receivers/search?q=${encodeURIComponent(query)}`));
  }

  getByRfc(rfc: string): Observable<FiscalReceiver> {
    return this.http.get<FiscalReceiver>(buildApiUrl(`/fiscal/receivers/by-rfc/${encodeURIComponent(rfc)}`));
  }

  getSatCatalog(): Observable<FiscalReceiverSatCatalog> {
    return this.http.get<FiscalReceiverSatCatalog>(buildApiUrl('/fiscal/receivers/sat-catalogs'));
  }

  create(request: UpsertFiscalReceiverRequest): Observable<MutationResponse> {
    return this.http.post<MutationResponse>(buildApiUrl('/fiscal/receivers/'), request);
  }

  update(id: number, request: UpsertFiscalReceiverRequest): Observable<MutationResponse> {
    return this.http.put<MutationResponse>(buildApiUrl(`/fiscal/receivers/${id}`), request);
  }
}
