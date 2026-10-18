import type { LoginResponse } from '../types/api';

const SESSION_KEY = 'northwind-support.auth-session';
export const AUTH_EXPIRED_EVENT = 'northwind-support:auth-expired';

export type AuthSession = LoginResponse;

export function readAuthSession(): AuthSession | null {
  if (typeof window === 'undefined') return null;

  try {
    const raw = window.sessionStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as AuthSession;
    if (!session.accessToken || !session.expiresAtUtc || Date.parse(session.expiresAtUtc) <= Date.now()) {
      clearAuthSession();
      return null;
    }
    return session;
  } catch {
    clearAuthSession();
    return null;
  }
}

export function saveAuthSession(session: AuthSession): void {
  window.sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function clearAuthSession(): void {
  if (typeof window !== 'undefined') window.sessionStorage.removeItem(SESSION_KEY);
}

export function getAccessToken(): string | null {
  return readAuthSession()?.accessToken ?? null;
}
