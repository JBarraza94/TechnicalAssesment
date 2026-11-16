import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, firstValueFrom, tap } from 'rxjs';
import { API_BASE_URL, toApiError } from '../companies/company.service';

/** Mirrors the API's LoginResponse. */
export interface AuthSession {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  username: string;
}

export const SESSION_STORAGE_KEY = 'company-app.session';

/**
 * Holds the signed-in user's token. The session lives in sessionStorage, so it survives reloads
 * but ends when the tab closes. The API remains the authority: it rejects missing or expired tokens.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/auth`;
  private readonly session = signal<AuthSession | null>(readStoredSession());

  readonly username = computed(() => this.session()?.username ?? null);

  /**
   * Whether the API requires sign-in (GET /api/auth/config). Starts as true, so a server that cannot
   * be reached never silently opens the app; loadConfig() relaxes it when the server says so.
   */
  readonly authRequired = signal(true);

  /** Called once at startup (provideAppInitializer). Never rejects: on failure auth stays required. */
  async loadConfig(): Promise<void> {
    try {
      const config = await firstValueFrom(this.http.get<{ enabled: boolean }>(`${this.baseUrl}/config`));
      this.authRequired.set(config.enabled !== false);
    } catch {
      this.authRequired.set(true);
    }
  }

  /** True when the protected pages can be used: auth is off, or the user is signed in. */
  canAccessApp(): boolean {
    return !this.authRequired() || this.isAuthenticated();
  }

  login(username: string, password: string): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(`${this.baseUrl}/login`, { username, password })
      .pipe(
        tap((session) => this.setSession(session)),
        catchError(toApiError),
      );
  }

  logout(): void {
    this.setSession(null);
  }

  /** True while there is a session whose token has not expired. Reactive in templates (reads a signal). */
  isAuthenticated(): boolean {
    return isUsable(this.session());
  }

  /** The bearer token, or null when signed out or expired. */
  token(): string | null {
    const session = this.session();
    return isUsable(session) ? session!.accessToken : null;
  }

  setSession(session: AuthSession | null): void {
    this.session.set(session);
    try {
      if (session) sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
      else sessionStorage.removeItem(SESSION_STORAGE_KEY);
    } catch {
      // Storage can be unavailable (private mode, blocked site data); the in-memory session still works.
    }
  }
}

function isUsable(session: AuthSession | null): boolean {
  return !!session && Date.parse(session.expiresAt) > Date.now();
}

function readStoredSession(): AuthSession | null {
  try {
    const raw = sessionStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<AuthSession>;
    return parsed.accessToken && parsed.expiresAt && parsed.username ? (parsed as AuthSession) : null;
  } catch {
    return null;
  }
}
