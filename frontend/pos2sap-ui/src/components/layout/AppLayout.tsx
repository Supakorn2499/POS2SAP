// src/components/layout/AppLayout.tsx
import { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Activity,
  Settings2,
  LogOut,
  FileInput,
  Boxes,
  Building2,
  PanelLeftClose,
  PanelLeftOpen,
  TerminalSquare,
  Moon,
  Sun,
  BookOpen,
  Menu,
  X,
  Wallet,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { AppIcon } from '@/components/ui/AppIcon';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';
import { useTheme } from '@/contexts/ThemeContext';

const SIDEBAR_COLLAPSED_KEY = 'pos2sapSidebarCollapsed';
/** iPad landscape ≈ 1024–1180 — keep drawer until true desktop */
const DESKTOP_MQ = '(min-width: 1280px)';

const navItems = [
  { to: '/dashboard', labelKey: 'dashboard', icon: LayoutDashboard, end: true },
  { to: '/monitor', labelKey: 'monitor', icon: Activity, end: false },
  { to: '/import', labelKey: 'importSidebarLabel', icon: FileInput, end: true },
  { to: '/glmapping', labelKey: 'glMapping', icon: Wallet, end: true },
  { to: '/productgroupmapping', labelKey: 'pgMapping', icon: Boxes, end: true },
  { to: '/shopmapping', labelKey: 'shopMapping', icon: Building2, end: true },
  { to: '/config', labelKey: 'config', icon: Settings2, end: true },
  { to: '/app-logs', labelKey: 'appLogs', icon: TerminalSquare, end: true },
];

/** Includes pages that live outside the sidebar (e.g. User Guide in header) */
const pageMeta = [
  ...navItems,
  { to: '/guide', labelKey: 'userGuide', icon: BookOpen, end: true },
];

function resolvePageLabelKey(pathname: string): string {
  const sorted = [...pageMeta].sort((a, b) => b.to.length - a.to.length);
  for (const item of sorted) {
    if (item.end) {
      if (pathname === item.to) return item.labelKey;
    } else if (pathname === item.to || pathname.startsWith(`${item.to}/`)) {
      return item.labelKey;
    }
  }
  return 'dashboard';
}

function useIsDesktop() {
  const [isDesktop, setIsDesktop] = useState(() =>
    typeof window !== 'undefined' ? window.matchMedia(DESKTOP_MQ).matches : true
  );

  useEffect(() => {
    const mq = window.matchMedia(DESKTOP_MQ);
    const onChange = () => setIsDesktop(mq.matches);
    onChange();
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, []);

  return isDesktop;
}

export function AppLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { lang, setLang, t } = useLanguage();
  const { logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const isDesktop = useIsDesktop();
  const [sidebarCollapsed, setSidebarCollapsed] = useState(
    () => localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true'
  );
  const [mobileOpen, setMobileOpen] = useState(false);

  // Drawer only below lg — close when crossing to desktop or changing route
  useEffect(() => {
    if (isDesktop) setMobileOpen(false);
  }, [isDesktop]);

  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!mobileOpen || isDesktop) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMobileOpen(false);
    };
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = prev;
      window.removeEventListener('keydown', onKey);
    };
  }, [mobileOpen, isDesktop]);

  // Keep sticky bars (e.g. mapping unsaved) aligned with sidebar width
  useEffect(() => {
    const w = !isDesktop ? '0px' : sidebarCollapsed ? '4.25rem' : '15rem';
    document.documentElement.style.setProperty('--app-sidebar-w', w);
    return () => {
      document.documentElement.style.removeProperty('--app-sidebar-w');
    };
  }, [isDesktop, sidebarCollapsed]);

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
  const ActiveIcon = pageMeta.find((n) => n.labelKey === pageLabelKey)?.icon ?? LayoutDashboard;
  const guideActive = location.pathname === '/guide' || location.pathname.startsWith('/guide/');

  // On tablet/phone drawer always shows labels; desktop respects collapse
  const showLabels = !isDesktop || !sidebarCollapsed;
  const sidebarWide = !isDesktop || !sidebarCollapsed;

  return (
    <div className="flex min-h-screen bg-background">
      {/* Backdrop — tablet / phone drawer */}
      {mobileOpen && !isDesktop && (
        <button
          type="button"
          aria-label={t('menuClose')}
          className="fixed inset-0 z-40 bg-black/40 backdrop-blur-[2px] xl:hidden"
          onClick={() => setMobileOpen(false)}
        />
      )}

      <aside
        className={cn(
          'z-50 flex flex-col border-r border-[hsl(var(--sidebar-border))] bg-[hsl(var(--sidebar))] transition-[width,transform] duration-200 ease-in-out',
          // Desktop: static rail (xl+)
          'xl:relative xl:translate-x-0 xl:shrink-0',
          isDesktop
            ? cn('min-h-screen', sidebarCollapsed ? 'w-[4.25rem]' : 'w-60')
            : cn(
                'fixed inset-y-0 left-0 w-[min(18rem,85vw)] shadow-xl',
                mobileOpen ? 'translate-x-0' : '-translate-x-full'
              )
        )}
      >
        <div
          className={cn(
            'flex items-center gap-2 border-b border-[hsl(var(--sidebar-border))]',
            sidebarWide ? 'justify-between px-4 py-4' : 'justify-center px-2 py-4'
          )}
        >
          {showLabels ? (
            <div className="flex min-w-0 items-center gap-2.5">
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-sm">
                <AppIcon icon={Activity} className="h-4 w-4" />
              </div>
              <div className="min-w-0">
                <h1 className="text-[15px] font-semibold tracking-tight text-foreground">POS2SAP</h1>
                <p className="truncate text-[11px] font-medium text-muted-foreground">POS → SAP B1</p>
              </div>
            </div>
          ) : (
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-sm">
              <AppIcon icon={Activity} className="h-4 w-4" />
            </div>
          )}

          {isDesktop && showLabels && (
            <button
              type="button"
              onClick={toggleSidebar}
              aria-label={t('sidebarCollapse')}
              title={t('sidebarCollapse')}
              className="app-icon-well transition hover:border-primary/30 hover:text-foreground"
            >
              <AppIcon icon={PanelLeftClose} className="h-4 w-4" />
            </button>
          )}

          {!isDesktop && (
            <button
              type="button"
              onClick={() => setMobileOpen(false)}
              aria-label={t('menuClose')}
              title={t('menuClose')}
              className="app-icon-well transition hover:border-primary/30 hover:text-foreground"
            >
              <AppIcon icon={X} className="h-4 w-4" />
            </button>
          )}
        </div>

        {isDesktop && sidebarCollapsed && (
          <div className="flex justify-center border-b border-[hsl(var(--sidebar-border))] py-2">
            <button
              type="button"
              onClick={toggleSidebar}
              aria-label={t('sidebarExpand')}
              title={t('sidebarExpand')}
              className="app-icon-well transition hover:border-primary/30 hover:text-foreground"
            >
              <AppIcon icon={PanelLeftOpen} className="h-4 w-4" />
            </button>
          </div>
        )}

        <nav className="flex-1 space-y-0.5 overflow-y-auto overscroll-contain p-2.5">
          {navItems.map(({ to, labelKey, icon: Icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              title={!showLabels ? t(labelKey) : undefined}
              className={({ isActive }) =>
                cn(
                  'group relative flex min-h-10 items-center rounded-xl py-2 text-[13px] font-medium transition-all duration-150',
                  showLabels ? 'gap-2.5 px-2.5' : 'justify-center px-2',
                  isActive
                    ? 'bg-primary/10 text-primary shadow-sm ring-1 ring-primary/15'
                    : 'text-muted-foreground hover:bg-muted/80 hover:text-foreground'
                )
              }
            >
              {({ isActive }) => (
                <>
                  {isActive && (
                    <span className="absolute left-0 top-1/2 h-5 w-0.5 -translate-y-1/2 rounded-full bg-primary" />
                  )}
                  <span
                    className={cn(
                      'app-nav-icon transition',
                      isActive
                        ? 'bg-primary text-primary-foreground shadow-sm'
                        : 'bg-transparent text-muted-foreground group-hover:bg-background group-hover:text-foreground'
                    )}
                  >
                    <AppIcon icon={Icon} className="h-4 w-4" />
                  </span>
                  {showLabels && <span className="truncate tracking-tight">{t(labelKey)}</span>}
                </>
              )}
            </NavLink>
          ))}
        </nav>
      </aside>

      <main className="flex min-w-0 flex-1 flex-col overflow-auto">
        <header className="sticky top-0 z-20 border-b border-border/80 bg-[hsl(var(--header))] px-4 py-3 backdrop-blur-md md:px-5 xl:px-6 xl:py-3.5">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div className="flex min-w-0 items-center gap-2.5 md:gap-3">
              {!isDesktop && (
                <button
                  type="button"
                  onClick={() => setMobileOpen(true)}
                  aria-label={t('menuOpen')}
                  title={t('menuOpen')}
                  className="app-icon-well h-10 w-10 shrink-0 transition hover:border-primary/30 hover:text-foreground"
                >
                  <AppIcon icon={Menu} className="h-4 w-4" />
                </button>
              )}
              <div className="app-icon-well hidden text-primary sm:inline-flex">
                <AppIcon icon={ActiveIcon} className="h-4 w-4" />
              </div>
              <div className="min-w-0">
                <p className="truncate text-sm font-semibold tracking-tight text-foreground">
                  {t(pageLabelKey)}
                </p>
                {staffName && (
                  <p className="truncate text-xs text-muted-foreground">{staffName}</p>
                )}
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={toggleTheme}
                aria-label={t('themeToggle')}
                title={theme === 'dark' ? t('themeLight') : t('themeDark')}
                className="app-icon-well h-10 w-10 transition hover:border-primary/30 hover:text-foreground"
              >
                <AppIcon icon={theme === 'dark' ? Sun : Moon} className="h-4 w-4" />
              </button>
              <NavLink
                to="/guide"
                title={t('userGuide')}
                aria-label={t('userGuide')}
                className={cn(
                  'inline-flex h-10 items-center gap-2 rounded-xl border px-3 text-sm font-medium shadow-sm transition',
                  guideActive
                    ? 'border-primary/30 bg-primary/10 text-primary'
                    : 'border-border/80 bg-card text-muted-foreground hover:border-primary/30 hover:text-foreground'
                )}
              >
                <AppIcon icon={BookOpen} className="h-4 w-4" />
                <span className="hidden lg:inline">{t('userGuideShort')}</span>
              </NavLink>
              <div className="inline-flex items-center rounded-xl border border-border/80 bg-card p-0.5 shadow-sm">
                {(['th', 'en'] as const).map((code) => (
                  <button
                    key={code}
                    type="button"
                    onClick={() => setLang(code)}
                    className={cn(
                      'min-h-9 rounded-lg px-3 py-1.5 text-xs font-semibold tracking-wide transition',
                      lang === code
                        ? 'bg-primary text-primary-foreground shadow-sm'
                        : 'text-muted-foreground hover:text-foreground'
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
                className="inline-flex h-10 items-center gap-2 rounded-xl border border-destructive/25 bg-destructive/5 px-3 text-sm font-medium text-destructive transition hover:bg-destructive/10"
              >
                <AppIcon icon={LogOut} className="h-4 w-4" />
                <span className="hidden md:inline">{t('logout')}</span>
              </button>
            </div>
          </div>
        </header>
        <div className="flex-1 p-4 md:p-5 xl:p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
