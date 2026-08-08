import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { SessionService } from './core/auth/session.service';
import { apiErrorInterceptor } from './core/http/interceptors/api-error.interceptor';
import { authTokenInterceptor } from './core/http/interceptors/auth-token.interceptor';
import { correlationIdInterceptor } from './core/http/interceptors/correlation-id.interceptor';
import { globalLoaderInterceptor } from './core/http/interceptors/global-loader.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([
      correlationIdInterceptor,
      authTokenInterceptor,
      globalLoaderInterceptor,
      apiErrorInterceptor
    ])),
    provideAppInitializer(() => inject(SessionService).restoreSession())
  ]
};
