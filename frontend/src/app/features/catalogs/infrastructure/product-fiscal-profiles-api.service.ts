import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { MutationResponse, ProductFiscalProfile, ProductFiscalProfileSearchItem, UpsertProductFiscalProfileRequest } from '../models/catalogs.models';

@Injectable({ providedIn: 'root' })
export class ProductFiscalProfilesApiService {
  private readonly http = inject(HttpClient);

  search(query: string): Observable<ProductFiscalProfileSearchItem[]> {
    return this.http.get<ProductFiscalProfileSearchItem[]>(buildApiUrl(`/fiscal/product-fiscal-profiles/search?q=${encodeURIComponent(query)}`));
  }

  getByCode(internalCode: string): Observable<ProductFiscalProfile> {
    return this.http.get<ProductFiscalProfile>(buildApiUrl(`/fiscal/product-fiscal-profiles/by-code/${encodeURIComponent(internalCode)}`));
  }

  create(request: UpsertProductFiscalProfileRequest): Observable<MutationResponse> {
    return this.http.post<MutationResponse>(buildApiUrl('/fiscal/product-fiscal-profiles/'), request);
  }

  update(id: number, request: UpsertProductFiscalProfileRequest): Observable<MutationResponse> {
    return this.http.put<MutationResponse>(buildApiUrl(`/fiscal/product-fiscal-profiles/${id}`), request);
  }
}
