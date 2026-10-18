import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../api/auth';
import { AUTH_EXPIRED_EVENT, clearAuthSession, readAuthSession, saveAuthSession, type AuthSession } from './authSession';
import { AuthContext } from './authContextValue';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => readAuthSession());

  useEffect(() => {
    const expireSession = () => setSession(null);
    window.addEventListener(AUTH_EXPIRED_EVENT, expireSession);
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, expireSession);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const nextSession = await authApi.login(email, password);
    saveAuthSession(nextSession);
    setSession(nextSession);
    return nextSession;
  }, []);

  const logout = useCallback(() => {
    clearAuthSession();
    setSession(null);
  }, []);

  const value = useMemo(() => ({ session, login, logout }), [session, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
