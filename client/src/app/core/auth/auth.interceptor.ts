import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Adds the Bearer token to calls to our own API (relative /api/ URLs only, so the token never leaks to
 * third-party hosts). A 401 from any API call except login means the session is no longer valid:
 * log out and send the user to the login page, remembering where they were.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isApiCall = req.url.startsWith('/api/');
  const token = isApiCall ? auth.token() : null;
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      const isLoginAttempt = req.url.startsWith('/api/auth/login') || req.url.startsWith('/api/auth/register');
      if (isApiCall && !isLoginAttempt && error instanceof HttpErrorResponse && error.status === 401) {
        auth.logout(router.url);
      }
      return throwError(() => error);
    }),
  );
};
