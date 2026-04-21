import { useState, type FormEvent } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import loginService from '@/services/loginService';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';

export default function LoginPage() {
  const navigate = useNavigate();
  const { t } = useLanguage();
  const { login } = useAuth();
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
      login();
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการเข้าสู่ระบบ');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4 py-10">
      <div className="w-full max-w-md rounded-3xl border bg-card p-8 shadow-xl">
        <div className="mb-8 text-center">
          <h1 className="text-2xl font-semibold">{t('loginTitle')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('loginSubtitle')}</p>
        </div>

        {error && (
          <div className="mb-4 rounded-lg border border-destructive/20 bg-destructive/10 px-4 py-3 text-sm text-destructive">
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
              className="w-full rounded-xl border border-input bg-background px-4 py-3 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10"
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
                className="w-full rounded-xl border border-input bg-background px-4 py-3 pr-12 text-sm outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10"
                placeholder={t('passwordPlaceholder')}
              />
              <button
                type="button"
                onClick={() => setShowPassword((prev) => !prev)}
                className="absolute right-3 top-1/2 -translate-y-1/2 inline-flex h-8 w-8 items-center justify-center rounded-full text-muted-foreground transition hover:bg-muted"
                aria-label={showPassword ? t('hidePassword') : t('showPassword')}
              >
                {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>
          </div>
          <button
            type="submit"
            disabled={isLoading}
            className="w-full rounded-xl bg-primary px-4 py-3 text-sm font-semibold text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isLoading ? t('loggingIn') : t('loginButton')}
          </button>
        </form>
      </div>
    </div>
  );
}
