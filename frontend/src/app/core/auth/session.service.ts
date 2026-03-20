import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import { AuthApiService } from './auth-api.service';
import { CurrentUser, LoginRequest, LoginResponse, AppRole } from './models';
import { TokenStorageService } from './token-storage.service';
import { FeedbackService } from '../ui/feedback.service';

const ANONYMOUS_USER: CurrentUser = {
  id: null,
  username: null,
  displayName: null,
  roles: [],
  isAuthenticated: false
};

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly authApi = inject(AuthApiService);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly router = inject(Router);
  private readonly feedbackService = inject(FeedbackService);

  readonly currentUser = signal<CurrentUser>(ANONYMOUS_USER);
  readonly token = signal<string | null>(this.tokenStorage.getToken());
  readonly initializing = signal(true);
  readonly loggingIn = signal(false);

  readonly isAuthenticated = computed(() => this.currentUser().isAuthenticated && !!this.token());
  readonly roles = computed(() => this.currentUser().roles);

  getDefaultAppRoute(): string {
    const roles = this.roles();
    if (roles.includes(AppRole.Auditor)) {
      return '/app/audit';
    }

    return '/app/orders';
  }

  async restoreSession(): Promise<void> {
    const token = this.tokenStorage.getToken();
    if (!token) {
      this.initializing.set(false);
      return;
    }

    this.token.set(token);

    try {
      const currentUser = await firstValueFrom(this.authApi.getCurrentUser());
      this.currentUser.set({
        ...currentUser,
        roles: (currentUser.roles ?? []) as AppRole[]
      });
    } catch {
      this.clearSession(false);
    } finally {
      this.initializing.set(false);
    }
  }

  async login(request: LoginRequest): Promise<LoginResponse> {
    this.loggingIn.set(true);
    try {
      const response = await firstValueFrom(this.authApi.login(request));
      if (response.isSuccess && response.token && response.user) {
        this.tokenStorage.setToken(response.token);
        this.token.set(response.token);
        this.currentUser.set({
          ...response.user,
          roles: (response.user.roles ?? []) as AppRole[]
        });
      }

      return response;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        return {
          outcome: 'InvalidCredentials',
          isSuccess: false,
          errorMessage: 'Invalid credentials.'
        };
      }

      throw error;
    } finally {
      this.loggingIn.set(false);
    }
  }

  async logout(redirect = true): Promise<void> {
    this.clearSession(redirect);
  }

  async handleUnauthorized(message = 'Your session is no longer valid. Please sign in again.'): Promise<void> {
    this.feedbackService.show('warning', message);
    this.clearSession(true);
  }

  private clearSession(redirect: boolean): void {
    this.tokenStorage.clear();
    this.token.set(null);
    this.currentUser.set(ANONYMOUS_USER);
    this.initializing.set(false);

    if (redirect) {
      void this.router.navigate(['/login']);
    }
  }
}
