import { Injectable } from '@angular/core';

const AUTH_TOKEN_KEY = 'pf.auth.token';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  getToken(): string | null {
    return localStorage.getItem(AUTH_TOKEN_KEY);
  }

  setToken(token: string): void {
    localStorage.setItem(AUTH_TOKEN_KEY, token);
  }

  clear(): void {
    localStorage.removeItem(AUTH_TOKEN_KEY);
  }
}
