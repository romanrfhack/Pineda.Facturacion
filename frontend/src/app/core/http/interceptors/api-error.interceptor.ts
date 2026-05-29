import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { FeedbackService } from '../../ui/feedback.service';
import { SessionService } from '../../auth/session.service';
import { buildApiUrl } from '../../config/api-url';
import { SUPPRESS_GLOBAL_ERROR_TOAST } from '../api-error-context.tokens';

export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const feedbackService = inject(FeedbackService);
  const sessionService = inject(SessionService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isLoginRequest(req.url)) {
        void sessionService.handleUnauthorized();
      } else if (error.status === 403) {
        feedbackService.show('error', 'Tu usuario está autenticado, pero no tiene permisos para realizar esta acción.');
      } else if (error.status >= 500 && !req.context.get(SUPPRESS_GLOBAL_ERROR_TOAST)) {
        feedbackService.show('error', 'El servidor no pudo completar la solicitud. Intenta de nuevo o contacta a soporte.');
      }

      return throwError(() => error);
    })
  );
};

function isLoginRequest(url: string): boolean {
  const loginUrl = buildApiUrl('/auth/login');
  return url === loginUrl || url.endsWith(loginUrl);
}
