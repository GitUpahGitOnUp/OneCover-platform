import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api-base-url.token';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const apiBaseUrl = inject(API_BASE_URL);
  const auth = inject(AuthService);
  const router = inject(Router);
  const session = auth.sessionSnapshot;
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
  ).pipe(
    catchError((error: unknown) => {
      // A 401 on a signed-in request means the token expired or was rejected:
      // clear the session and send the user to log in again.
      if (error instanceof HttpErrorResponse && error.status === 401) {
        auth.logout();
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    }),
  );
};
