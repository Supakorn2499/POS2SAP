import { createContext, useContext, useMemo, useState, type PropsWithChildren } from 'react';

interface AuthContextValue {
  authenticated: boolean;
  login: () => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: PropsWithChildren) {
  const [authenticated, setAuthenticated] = useState<boolean>(
    () => localStorage.getItem('pos2sapAuth') === 'true'
  );

  const login = () => {
    localStorage.setItem('pos2sapAuth', 'true');
    setAuthenticated(true);
  };

  const logout = () => {
    localStorage.removeItem('pos2sapAuth');
    localStorage.removeItem('pos2sapUser');
    setAuthenticated(false);
  };

  const value = useMemo(
    () => ({ authenticated, login, logout }),
    [authenticated]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return ctx;
}
