import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../api/api-base-url.token';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const apiBaseUrl = inject(API_BASE_URL);
  const session = inject(AuthService).sessionSnapshot;
  const targetsGateway =
    request.url === apiBaseUrl ||
    request.url.startsWith(`${apiBaseUrl}/`) ||
    request.url.startsWith(`${apiBaseUrl}?`);

  if (!session || !targetsGateway) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${session.token}` },
    }),
  );
};
