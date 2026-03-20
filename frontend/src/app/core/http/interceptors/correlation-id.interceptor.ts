import { HttpInterceptorFn } from '@angular/common/http';

export const correlationIdInterceptor: HttpInterceptorFn = (req, next) => {
  const correlationId = crypto.randomUUID();
  return next(req.clone({
    setHeaders: {
      'X-Correlation-Id': correlationId
    }
  }));
};
