import { createContext, useContext, useMemo, useState, type PropsWithChildren } from 'react';

interface AuthContextValue {
  authenticated: boolean;
  username: string | null;
  login: (username: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: PropsWithChildren) {
  const [authenticated, setAuthenticated] = useState<boolean>(
    () => localStorage.getItem('pos2sapAuth') === 'true' && !!localStorage.getItem('pos2sapToken')
  );
  const [username, setUsername] = useState<string | null>(
    () => localStorage.getItem('pos2sapUser')
  );

  const login = (user: string) => {
    localStorage.setItem('pos2sapAuth', 'true');
    localStorage.setItem('pos2sapUser', user);
    setAuthenticated(true);
    setUsername(user);
  };

  const logout = () => {
    localStorage.removeItem('pos2sapAuth');
    localStorage.removeItem('pos2sapUser');
    localStorage.removeItem('pos2sapToken');
    setAuthenticated(false);
    setUsername(null);
  };

  const value = useMemo(
    () => ({ authenticated, username, login, logout }),
    [authenticated, username]
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
