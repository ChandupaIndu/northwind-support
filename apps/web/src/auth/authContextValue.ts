import { createContext } from 'react';
import type { AuthSession } from './authSession';

export interface AuthContextValue {
  session: AuthSession | null;
  login: (email: string, password: string) => Promise<AuthSession>;
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
