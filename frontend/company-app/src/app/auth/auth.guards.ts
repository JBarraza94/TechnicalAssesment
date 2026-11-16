import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Lets users through when auth is off or they are signed in; otherwise /login, then back here. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).canAccessApp()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

/** The sign-in page makes no sense when auth is off or the user is already signed in. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).canAccessApp() ? inject(Router).createUrlTree(['/add']) : true;

/** Only same-app paths are allowed as a post-login destination (prevents open redirects). */
export function safeReturnUrl(value: string | null | undefined, fallback = '/add'): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\') || value.startsWith('/login')) {
    return fallback;
  }
  return value;
}
