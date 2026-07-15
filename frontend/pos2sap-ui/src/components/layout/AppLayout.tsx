// src/components/layout/AppLayout.tsx
import { useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  ListFilter,
  Settings,
  LogOut,
  FolderInput,
  Map,
  Layers,
  PanelLeftClose,
  PanelLeftOpen,
  ScrollText,
  Moon,
  Sun,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';
import { useTheme } from '@/contexts/ThemeContext';

const SIDEBAR_COLLAPSED_KEY = 'pos2sapSidebarCollapsed';

const navItems = [
  { to: '/dashboard', labelKey: 'dashboard',        icon: LayoutDashboard, end: true  },
  { to: '/monitor',   labelKey: 'monitor',           icon: ListFilter,      end: false },
  { to: '/import',    labelKey: 'importSidebarLabel', icon: FolderInput,     end: true  },
  { to: '/glmapping', labelKey: 'glMapping',          icon: Map,             end: true  },
  { to: '/productgroupmapping', labelKey: 'pgMapping', icon: Layers,        end: true  },
  { to: '/app-logs',  labelKey: 'appLogs',            icon: ScrollText,      end: true  },
  { to: '/config',    labelKey: 'config',             icon: Settings,        end: true  },
];

function resolvePageLabelKey(pathname: string): string {
  const sorted = [...navItems].sort((a, b) => b.to.length - a.to.length);
  for (const item of sorted) {
    if (item.end) {
      if (pathname === item.to) return item.labelKey;
    } else if (pathname === item.to || pathname.startsWith(`${item.to}/`)) {
      return item.labelKey;
    }
  }
  return 'dashboard';
}

export function AppLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { lang, setLang, t } = useLanguage();
  const { logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const [sidebarCollapsed, setSidebarCollapsed] = useState(
    () => localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true'
  );

  const toggleSidebar = () => {
    setSidebarCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(next));
      return next;
    });
  };

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

  const pageLabelKey = resolvePageLabelKey(location.pathname);

  return (
    <div className="flex min-h-screen bg-background">
      {/* Sidebar */}
      <aside
        className={cn(
          'shrink-0 border-r bg-card flex flex-col min-h-screen transition-[width] duration-200 ease-in-out',
          sidebarCollapsed ? 'w-16' : 'w-56'
        )}
      >
        <div
          className={cn(
            'border-b flex items-center gap-2',
            sidebarCollapsed ? 'justify-center px-2 py-4' : 'justify-between px-4 py-5'
          )}
        >
          {!sidebarCollapsed && (
            <div className="min-w-0">
              <h1 className="text-base font-bold text-foreground">POS2SAP</h1>
              <p className="text-xs text-muted-foreground mt-0.5 truncate">POS → SAP B1 Interface</p>
            </div>
          )}
          <button
            type="button"
            onClick={toggleSidebar}
            aria-label={sidebarCollapsed ? t('sidebarExpand') : t('sidebarCollapse')}
            title={sidebarCollapsed ? t('sidebarExpand') : t('sidebarCollapse')}
            className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-input bg-background text-muted-foreground transition hover:bg-muted hover:text-foreground"
          >
            {sidebarCollapsed ? (
              <PanelLeftOpen className="h-4 w-4" />
            ) : (
              <PanelLeftClose className="h-4 w-4" />
            )}
          </button>
        </div>

        <nav className="flex-1 p-2 space-y-1 overflow-y-auto">
          {navItems.map(({ to, labelKey, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              title={sidebarCollapsed ? t(labelKey) : undefined}
              className={({ isActive }) =>
                cn(
                  'flex items-center rounded-lg py-2 text-sm font-medium transition-colors',
                  sidebarCollapsed ? 'justify-center px-2' : 'gap-2.5 px-3',
                  isActive
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                )
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              {!sidebarCollapsed && <span className="truncate">{t(labelKey)}</span>}
            </NavLink>
          ))}
        </nav>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-auto min-w-0">
        <div className="border-b bg-background/60 px-6 py-4 shadow-sm">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-sm font-semibold text-foreground">{t(pageLabelKey)}</p>
              {staffName && (
                <p className="text-xs text-muted-foreground">{staffName}</p>
              )}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={toggleTheme}
                aria-label={t('themeToggle')}
                title={theme === 'dark' ? t('themeLight') : t('themeDark')}
                className="inline-flex h-9 w-9 items-center justify-center rounded-full border border-input bg-background text-foreground transition hover:bg-muted"
              >
                {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
              </button>
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
