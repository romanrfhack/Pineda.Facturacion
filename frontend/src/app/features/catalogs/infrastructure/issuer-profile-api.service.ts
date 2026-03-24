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

  getLogo(id: number): Observable<Blob> {
    return this.http.get(buildApiUrl(`/fiscal/issuer-profile/${id}/logo`), { responseType: 'blob' });
  }

  uploadLogo(id: number, file: File): Observable<MutationResponse> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.put<MutationResponse>(buildApiUrl(`/fiscal/issuer-profile/${id}/logo`), form);
  }

  removeLogo(id: number): Observable<MutationResponse> {
    return this.http.delete<MutationResponse>(buildApiUrl(`/fiscal/issuer-profile/${id}/logo`));
  }
}
