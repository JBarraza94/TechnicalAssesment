import { TestBed } from '@angular/core/testing';
import { AuthService, AuthSession } from './auth.service';

/** Test helper: a session that expires <paramref name="minutes"/> from now (negative = already expired). */
export function fakeSession(minutes = 60, username = 'admin'): AuthSession {
  return {
    accessToken: `token-for-${username}`,
    tokenType: 'Bearer',
    expiresAt: new Date(Date.now() + minutes * 60_000).toISOString(),
    username,
  };
}

/** Test helper: signs in through the real AuthService without HTTP. */
export function signIn(username = 'admin'): void {
  TestBed.inject(AuthService).setSession(fakeSession(60, username));
}
