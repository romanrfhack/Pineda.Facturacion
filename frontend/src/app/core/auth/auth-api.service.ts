import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { buildApiUrl } from '../config/api-url';
import { CurrentUser, LoginRequest, LoginResponse } from './models';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(buildApiUrl('/auth/login'), request);
  }

  getCurrentUser(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>(buildApiUrl('/auth/me'));
  }
}
