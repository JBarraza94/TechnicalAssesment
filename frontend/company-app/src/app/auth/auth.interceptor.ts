import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../companies/company.service';
import { AuthService } from './auth.service';

/**
 * Adds the bearer token to API requests (never to other hosts). When the API answers 401 —
 * e.g. the token expired — the session is cleared and the user is sent to the sign-in page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const apiBaseUrl = inject(API_BASE_URL);

  if (!req.url.startsWith(apiBaseUrl)) {
    return next(req);
  }

  const token = auth.token();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      const isLogin = req.url.endsWith('/auth/login');
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin) {
        auth.logout();
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url, reason: 'expired' } });
      }
      return throwError(() => error);
    }),
  );
};
