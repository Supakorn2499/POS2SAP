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
    () => {
      const raw = localStorage.getItem('pos2sapUser');
      if (!raw) return null;
      try {
        const parsed = JSON.parse(raw);
        return typeof parsed === 'object' && parsed?.staffLogin ? String(parsed.staffLogin) : raw;
      } catch {
        return raw;
      }
    }
  );

  const login = (user: string) => {
    localStorage.setItem('pos2sapAuth', 'true');
    setAuthenticated(true);
    setUsername(user);
  };

  const logout = () => {
    localStorage.removeItem('pos2sapAuth');
    localStorage.removeItem('pos2sapUser');
    localStorage.removeItem('pos2sapToken');
    localStorage.removeItem('pos2sapRefreshToken');
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
