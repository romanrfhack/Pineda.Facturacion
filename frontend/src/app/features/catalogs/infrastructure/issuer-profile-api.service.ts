import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../../../core/config/api-url';
import { IssuerProfile, MutationResponse, UpsertIssuerProfileRequest } from '../models/catalogs.models';

@Injectable({ providedIn: 'root' })
export class IssuerProfileApiService {
  private readonly http = inject(HttpClient);

  getActive(): Observable<IssuerProfile> {
    return this.http.get<IssuerProfile>(buildApiUrl('/fiscal/issuer-profile/active'));
  }

  create(request: UpsertIssuerProfileRequest): Observable<MutationResponse> {
    return this.http.post<MutationResponse>(buildApiUrl('/fiscal/issuer-profile/'), request);
  }

  update(id: number, request: UpsertIssuerProfileRequest): Observable<MutationResponse> {
    return this.http.put<MutationResponse>(buildApiUrl(`/fiscal/issuer-profile/${id}`), request);
  }
}
