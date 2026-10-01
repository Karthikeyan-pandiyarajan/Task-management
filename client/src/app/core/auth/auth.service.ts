import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest, Session } from '../models/auth.models';

export const SESSION_STORAGE_KEY = 'taskapp.session';

/**
 * Owns the logged-in session as a signal so the header, guards and interceptor all react to one source of truth.
 * The token is kept in localStorage so a page refresh keeps the user logged in. Trade-off: it is readable by
 * injected scripts (XSS); an httpOnly cookie would avoid that but needs cookie/CSRF handling across both APIs.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<Session | null>(readStoredSession());

  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly email = computed(() => this.session()?.email ?? null);

  /** Current token, or null when missing or expired (an expired session is cleared on the spot). */
  token(): string | null {
    const current = this.session();
    if (!current) {
      return null;
    }
    if (isExpired(current.token)) {
      this.clearSession();
      return null;
    }
    return current.token;
  }

  isAuthenticated(): boolean {
    return this.token() !== null;
  }

  login(request: LoginRequest): Observable<void> {
    return this.http.post<AuthResponse>('/api/auth/login', request).pipe(
      tap((response) => this.storeSession(response)),
      map(() => undefined),
    );
  }

  register(request: RegisterRequest): Observable<void> {
    return this.http.post<AuthResponse>('/api/auth/register', request).pipe(
      tap((response) => this.storeSession(response)),
      map(() => undefined),
    );
  }

  logout(returnUrl?: string): void {
    this.clearSession();
    void this.router.navigate(['/login'], { queryParams: returnUrl ? { returnUrl } : {} });
  }

  private storeSession({ token, email }: AuthResponse): void {
    const session: Session = { token, email };
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
    this.session.set(session);
  }

  private clearSession(): void {
    localStorage.removeItem(SESSION_STORAGE_KEY);
    this.session.set(null);
  }
}

function readStoredSession(): Session | null {
  try {
    const raw = localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    if (isSession(parsed) && !isExpired(parsed.token)) {
      return parsed;
    }
  } catch {
    // Corrupt storage is treated as logged out.
  }
  localStorage.removeItem(SESSION_STORAGE_KEY);
  return null;
}

function isSession(value: unknown): value is Session {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as Session).token === 'string' &&
    typeof (value as Session).email === 'string'
  );
}

/** Reads the JWT "exp" claim. The client never trusts the token for security; this only avoids sending stale ones. */
export function isExpired(token: string, nowMs: number = Date.now()): boolean {
  try {
    const payload = token.split('.')[1];
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    const exp: unknown = JSON.parse(json).exp;
    return typeof exp !== 'number' || exp * 1000 <= nowMs;
  } catch {
    return true;
  }
}
