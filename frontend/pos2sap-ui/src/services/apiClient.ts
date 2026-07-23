// src/services/apiClient.ts
import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { getStoredLang, getTranslation } from '@/lib/i18n';

const REFRESH_KEY = 'pos2sapRefreshToken';
const TOKEN_KEY = 'pos2sapToken';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
});

type RetriableConfig = InternalAxiosRequestConfig & { _retry?: boolean };

let refreshInFlight: Promise<string | null> | null = null;

function clearSession() {
  localStorage.removeItem('pos2sapAuth');
  localStorage.removeItem('pos2sapUser');
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
}

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return null;

  // Use bare axios so this call never re-enters the 401 interceptor.
  const baseURL = apiClient.defaults.baseURL ?? '/api';
  const res = await axios.post(
    `${baseURL}/auth/refresh`,
    { refreshToken },
    { timeout: 15000, headers: { 'Content-Type': 'application/json' } }
  );
  const accessToken = res.data?.data?.accessToken as string | undefined;
  if (!accessToken) return null;
  localStorage.setItem(TOKEN_KEY, accessToken);
  return accessToken;
}

function forceLogoutToLogin() {
  clearSession();
  if (window.location.pathname !== '/login') {
    window.location.assign('/login');
  }
}

apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (err) => Promise.reject(err)
);

apiClient.interceptors.response.use(
  (res) => res,
  async (err: AxiosError<{ message?: string }>) => {
    const config = err.config as RetriableConfig | undefined;
    const status = err.response?.status;
    const url = String(config?.url ?? '');
    const isAuthEndpoint = url.includes('/auth/login') || url.includes('/auth/refresh');

    if (status === 401 && config && !config._retry && !isAuthEndpoint) {
      config._retry = true;
      try {
        refreshInFlight ??= refreshAccessToken().finally(() => {
          refreshInFlight = null;
        });
        const newToken = await refreshInFlight;
        if (newToken) {
          config.headers = config.headers ?? {};
          config.headers.Authorization = `Bearer ${newToken}`;
          return apiClient(config);
        }
      } catch {
        // fall through to logout
      }
      forceLogoutToLogin();
      return Promise.reject(new Error('Unauthorized'));
    }

    const msg = err.response?.data?.message ?? err.message ?? getTranslation('errorGeneric', getStoredLang());
    return Promise.reject(new Error(msg));
  }
);

export default apiClient;
