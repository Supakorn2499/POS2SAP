// src/components/layout/AppLayout.tsx
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { LayoutDashboard, ListFilter, Settings, LogOut } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';

const navItems = [
  { to: '/dashboard', labelKey: 'dashboard', icon: LayoutDashboard, end: true },
  { to: '/monitor', labelKey: 'monitor', icon: ListFilter, end: false },
  { to: '/config', labelKey: 'config', icon: Settings, end: true },
];

export function AppLayout() {
  const navigate = useNavigate();
  const { lang, setLang, t } = useLanguage();
  const { logout } = useAuth();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const staffName = (() => {
    const stored = localStorage.getItem('pos2sapUser');
    if (!stored) return null;
    try {
      return JSON.parse(stored).staffFirstName as string | null;
    } catch {
      return null;
    }
  })();

  return (
    <div className="flex min-h-screen bg-background">
      {/* Sidebar */}
      <aside className="w-56 shrink-0 border-r bg-card flex flex-col min-h-screen">
        <div className="px-4 py-5 border-b">
          <div>
            <h1 className="text-base font-bold text-foreground">POS2SAP</h1>
            <p className="text-xs text-muted-foreground mt-0.5">POS → SAP B1 Interface</p>
          </div>
        </div>

        <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
          {navItems.map(({ to, labelKey, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                )
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              {t(labelKey)}
            </NavLink>
          ))}
        </nav>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-auto">
        <div className="border-b bg-background/60 px-6 py-4 shadow-sm">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              {staffName ? (
                <p className="text-sm text-muted-foreground">{staffName}</p>
              ) : (
                <p className="text-sm text-muted-foreground">{t('dashboard')}</p>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <div className="inline-flex items-center gap-2 rounded-full border border-input bg-background p-1">
                {(['th', 'en'] as const).map((code) => (
                  <button
                    key={code}
                    type="button"
                    onClick={() => setLang(code)}
                    className={cn(
                      'rounded-full px-3 py-2 text-xs font-medium transition',
                      lang === code
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-transparent bg-transparent text-foreground hover:border-primary'
                    )}
                  >
                    {code.toUpperCase()}
                  </button>
                ))}
              </div>
              <button
                type="button"
                onClick={handleLogout}
                aria-label={t('logout')}
                className="inline-flex items-center gap-2 rounded-xl border border-destructive bg-destructive/10 px-3 py-2 text-sm font-medium text-destructive transition hover:bg-destructive/20"
              >
                <LogOut className="h-4 w-4" />
                {t('logout')}
              </button>
            </div>
          </div>
        </div>
        <div className="p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
