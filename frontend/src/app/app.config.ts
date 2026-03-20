import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { authTokenInterceptor } from './core/http/interceptors/auth-token.interceptor';
import { correlationIdInterceptor } from './core/http/interceptors/correlation-id.interceptor';
import { apiErrorInterceptor } from './core/http/interceptors/api-error.interceptor';
import { SessionService } from './core/auth/session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([
      correlationIdInterceptor,
      authTokenInterceptor,
      apiErrorInterceptor
    ])),
    provideAppInitializer(() => inject(SessionService).restoreSession())
  ]
};
