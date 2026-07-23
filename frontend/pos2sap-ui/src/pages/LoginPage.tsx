import { useState, type FormEvent } from 'react';
import { Activity, Eye, EyeOff, Moon, Sun } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import loginService from '@/services/loginService';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';
import { useTheme } from '@/contexts/ThemeContext';
import { AppIcon } from '@/components/ui/AppIcon';

/** Modern POS ↔ SAP connection backdrop */
function LoginConnectBg() {
  return (
    <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
      {/* Soft atmosphere */}
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_70%_55%_at_50%_45%,hsl(var(--primary)/0.09),transparent_65%)]" />
      <div className="absolute inset-0 opacity-[0.35] dark:opacity-[0.22] [background-image:linear-gradient(hsl(var(--border)/0.7)_1px,transparent_1px),linear-gradient(90deg,hsl(var(--border)/0.7)_1px,transparent_1px)] [background-size:48px_48px] [mask-image:radial-gradient(ellipse_75%_65%_at_50%_45%,#000_20%,transparent_75%)]" />

      <svg
        className="absolute inset-0 h-full w-full text-primary"
        viewBox="0 0 1440 900"
        preserveAspectRatio="xMidYMid slice"
        fill="none"
      >
        {/* Ambient constellation nodes */}
        {[
          [180, 160], [260, 720], [1180, 140], [1280, 680],
          [420, 120], [980, 780], [720, 90], [760, 820],
        ].map(([x, y], i) => (
          <g key={`${x}-${y}`}>
            <circle cx={x} cy={y} r={2.2} className="fill-current opacity-25" />
            <circle
              cx={x}
              cy={y}
              r={10}
              className="login-pulse-node stroke-current fill-none opacity-20"
              strokeWidth="1"
              style={{ animationDelay: `${i * 0.25}s` }}
            />
          </g>
        ))}

        {/* Side feeder links into hubs */}
        <path d="M180 160 L320 390" className="login-link-flow-slow stroke-current opacity-25" strokeWidth="1" />
        <path d="M260 720 L340 520" className="login-link-flow stroke-current opacity-20" strokeWidth="1" />
        <path d="M1180 140 L1080 380" className="login-link-flow stroke-current opacity-25" strokeWidth="1" />
        <path d="M1280 680 L1100 530" className="login-link-flow-slow stroke-current opacity-20" strokeWidth="1" />

        {/* Main bridge beams POS → SAP */}
        <path
          d="M380 450 C560 360, 880 360, 1060 450"
          className="stroke-current opacity-20"
          strokeWidth="10"
          strokeLinecap="round"
        />
        <path
          d="M380 450 C560 360, 880 360, 1060 450"
          className="login-link-flow stroke-current opacity-55"
          strokeWidth="2"
          strokeLinecap="round"
        />
        <path
          d="M390 470 C580 560, 860 560, 1050 470"
          className="login-link-flow-slow stroke-current opacity-35"
          strokeWidth="1.5"
          strokeLinecap="round"
        />
        <path
          d="M400 430 C600 400, 840 400, 1040 430"
          className="login-link-flow stroke-current opacity-30"
          strokeWidth="1"
          strokeLinecap="round"
        />

        {/* POS hub (left) — circle + orbiting world */}
        <g transform="translate(340 450)">
          <ellipse cx="0" cy="0" rx="82" ry="78" className="stroke-primary/30 fill-none" strokeWidth="1" strokeDasharray="3 7" />
          <g>
            <animateTransform attributeName="transform" type="rotate" from="0 0 0" to="360 0 0" dur="14s" repeatCount="indefinite" />
            <g transform="translate(82 0)">
              <circle r="9" className="fill-primary/90 stroke-primary" strokeWidth="1" />
              <ellipse cx="0" cy="0" rx="3.5" ry="9" className="stroke-primary-foreground/70 fill-none" strokeWidth="1" />
              <path d="M-7.5 0 H7.5 M-6 -4.5 H6 M-6 4.5 H6" className="stroke-primary-foreground/55" strokeWidth="0.9" />
            </g>
          </g>
          <circle r="58" className="fill-card stroke-primary/45" strokeWidth="1.5" />
          <circle r="50" className="fill-primary/[0.07] stroke-none" />
          {/* Monitor */}
          <rect x="-30" y="-28" width="60" height="42" rx="6" className="fill-primary/15 stroke-primary" strokeWidth="1.75" />
          <rect x="-24" y="-22" width="48" height="28" rx="3" className="fill-primary/25 stroke-primary/60" strokeWidth="1" />
          <path d="M-18 -14 H12 M-18 -8 H4" className="stroke-primary" strokeWidth="1.4" strokeLinecap="round" opacity="0.65" />
          <path d="M0 14 V22" className="stroke-primary" strokeWidth="2" strokeLinecap="round" />
          <path d="M-16 26 H16" className="stroke-primary" strokeWidth="2.5" strokeLinecap="round" />
          <text
            x="0"
            y="88"
            textAnchor="middle"
            fill="currentColor"
            fontSize="13"
            fontWeight="600"
            opacity="0.55"
            fontFamily="Prompt, system-ui, sans-serif"
          >
            POS
          </text>
        </g>

        {/* Center relay node */}
        <g transform="translate(700 420)">
          <circle cx="20" cy="30" r="18" className="fill-card stroke-primary/50" strokeWidth="1.5" />
          <circle cx="20" cy="30" r="6" className="fill-primary login-pulse-node" />
          <circle cx="20" cy="30" r="28" className="stroke-primary/20 fill-none login-pulse-node" strokeWidth="1" />
        </g>

        {/* SAP hub (right) — circle + orbiting world */}
        <g transform="translate(1100 450)">
          <ellipse cx="0" cy="0" rx="82" ry="78" className="stroke-sky-500/35 dark:stroke-sky-300/30 fill-none" strokeWidth="1" strokeDasharray="3 7" />
          <g>
            <animateTransform attributeName="transform" type="rotate" from="360 0 0" to="0 0 0" dur="16s" repeatCount="indefinite" />
            <g transform="translate(82 0)">
              <circle r="9" className="fill-sky-500 dark:fill-sky-300 stroke-sky-600 dark:stroke-sky-200" strokeWidth="1" />
              <ellipse cx="0" cy="0" rx="3.5" ry="9" className="stroke-sky-950/50 dark:stroke-slate-900/60 fill-none" strokeWidth="1" />
              <path d="M-7.5 0 H7.5 M-6 -4.5 H6 M-6 4.5 H6" className="stroke-sky-950/45 dark:stroke-slate-900/50" strokeWidth="0.9" />
            </g>
          </g>
          <circle r="58" className="fill-card stroke-sky-500/50 dark:stroke-sky-300/45" strokeWidth="1.5" />
          <circle r="50" className="fill-sky-500/[0.07] dark:fill-sky-300/[0.08] stroke-none" />
          {/* Monitor */}
          <rect x="-30" y="-28" width="60" height="42" rx="6" className="fill-sky-500/15 stroke-sky-600 dark:fill-sky-300/15 dark:stroke-sky-300" strokeWidth="1.75" />
          <rect x="-24" y="-22" width="48" height="28" rx="3" className="fill-sky-500/25 stroke-sky-600/60 dark:fill-sky-300/20 dark:stroke-sky-300/60" strokeWidth="1" />
          <path d="M-18 -14 H12 M-18 -8 H4" className="stroke-sky-600 dark:stroke-sky-300" strokeWidth="1.4" strokeLinecap="round" opacity="0.65" />
          <path d="M0 14 V22" className="stroke-sky-600 dark:stroke-sky-300" strokeWidth="2" strokeLinecap="round" />
          <path d="M-16 26 H16" className="stroke-sky-600 dark:stroke-sky-300" strokeWidth="2.5" strokeLinecap="round" />
          <text
            x="0"
            y="88"
            textAnchor="middle"
            fill="currentColor"
            fontSize="13"
            fontWeight="600"
            opacity="0.55"
            fontFamily="Prompt, system-ui, sans-serif"
          >
            SAP
          </text>
        </g>

        {/* Packet dots along bridge */}
        <circle r="3.5" className="fill-primary">
          <animateMotion dur="3.2s" repeatCount="indefinite" path="M380 450 C560 360, 880 360, 1060 450" />
        </circle>
        <circle r="2.5" className="fill-sky-500 dark:fill-sky-300">
          <animateMotion dur="4s" repeatCount="indefinite" begin="1.1s" path="M1060 450 C880 540, 560 540, 380 450" />
        </circle>
      </svg>
    </div>
  );
}

export default function LoginPage() {
  const navigate = useNavigate();
  const { t } = useLanguage();
  const { login } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const [staffLogin, setStaffLogin] = useState('');
  const [staffPassword, setStaffPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const user = await loginService.login(staffLogin.trim(), staffPassword);
      if (!user) {
        setError(t('loginFailed'));
        return;
      }

      localStorage.setItem('pos2sapUser', JSON.stringify(user));
      localStorage.setItem('pos2sapToken', user.accessToken);
      localStorage.setItem('pos2sapRefreshToken', user.refreshToken);
      login(user.staffLogin);
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : t('loginErrorGeneric'));
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-background px-4 py-10">
      <LoginConnectBg />

      <button
        type="button"
        onClick={toggleTheme}
        aria-label={t('themeToggle')}
        title={theme === 'dark' ? t('themeLight') : t('themeDark')}
        className="app-icon-well absolute right-4 top-4 z-10 h-9 w-9 transition hover:border-primary/30 hover:text-foreground"
      >
        <AppIcon icon={theme === 'dark' ? Sun : Moon} className="h-4 w-4" />
      </button>

      <div className="relative z-10 w-full max-w-md overflow-hidden rounded-2xl border border-border/80 bg-card/90 p-8 shadow-xl shadow-primary/10 backdrop-blur-md">
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-md shadow-primary/25">
            <AppIcon icon={Activity} className="h-5 w-5" />
          </div>
          <h1 className="text-2xl font-semibold tracking-tight text-foreground">{t('loginTitle')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('loginSubtitle')}</p>
        </div>

        {error && (
          <div className="mb-4 rounded-xl border border-destructive/25 bg-destructive/10 px-4 py-3 text-sm text-destructive">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label htmlFor="staffLogin" className="mb-2 block text-sm font-medium text-foreground">
              {t('username')}
            </label>
            <input
              id="staffLogin"
              type="text"
              value={staffLogin}
              onChange={(e) => setStaffLogin(e.target.value)}
              autoComplete="username"
              className="w-full rounded-xl border border-input bg-background px-4 py-3 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
              placeholder={t('username')}
            />
          </div>
          <div>
            <label htmlFor="staffPassword" className="mb-2 block text-sm font-medium text-foreground">
              {t('password')}
            </label>
            <div className="relative">
              <input
                id="staffPassword"
                type={showPassword ? 'text' : 'password'}
                value={staffPassword}
                onChange={(e) => setStaffPassword(e.target.value)}
                autoComplete="current-password"
                className="w-full rounded-xl border border-input bg-background px-4 py-3 pr-12 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15"
                placeholder={t('passwordPlaceholder')}
              />
              <button
                type="button"
                onClick={() => setShowPassword((prev) => !prev)}
                className="absolute right-3 top-1/2 inline-flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-lg text-muted-foreground transition hover:bg-muted hover:text-foreground"
                aria-label={showPassword ? t('hidePassword') : t('showPassword')}
              >
                <AppIcon icon={showPassword ? EyeOff : Eye} className="h-4 w-4" />
              </button>
            </div>
          </div>
          <button
            type="submit"
            disabled={isLoading}
            className="w-full rounded-xl bg-primary px-4 py-3 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20 transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isLoading ? t('loggingIn') : t('loginButton')}
          </button>
        </form>
      </div>

      <p className="absolute bottom-4 left-0 right-0 z-10 text-center text-xs text-muted-foreground/70">
        © 2026 P2S
      </p>
    </div>
  );
}
