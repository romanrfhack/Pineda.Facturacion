import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { FeedbackService } from '../../ui/feedback.service';
import { TokenStorageService } from '../../auth/token-storage.service';
import { buildApiUrl } from '../../config/api-url';

export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const feedbackService = inject(FeedbackService);
  const tokenStorage = inject(TokenStorageService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && req.url !== buildApiUrl('/auth/login')) {
        tokenStorage.clear();
        feedbackService.show('warning', 'Tu sesión ya no es válida. Inicia sesión nuevamente.');
        void router.navigate(['/login']);
      } else if (error.status === 403) {
        feedbackService.show('error', 'Tu usuario está autenticado, pero no tiene permisos para realizar esta acción.');
      } else if (error.status >= 500) {
        feedbackService.show('error', 'El servidor no pudo completar la solicitud. Intenta de nuevo o contacta a soporte.');
      }

      return throwError(() => error);
    })
  );
};
