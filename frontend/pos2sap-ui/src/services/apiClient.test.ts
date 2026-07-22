import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import axios from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';

const TOKEN_KEY = 'pos2sapToken';
const REFRESH_KEY = 'pos2sapRefreshToken';

describe('apiClient refresh interceptor', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
    // jsdom location.assign is not implemented by default
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { pathname: '/dashboard', assign: vi.fn() },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('retries once after successful token refresh on 401', async () => {
    vi.resetModules();
    localStorage.setItem(REFRESH_KEY, 'refresh-1');
    localStorage.setItem(TOKEN_KEY, 'old-token');

    const postSpy = vi.spyOn(axios, 'post').mockResolvedValue({
      data: { data: { accessToken: 'new-token' } },
    } as never);

    const { default: apiClient } = await import('./apiClient');

    let calls = 0;
    apiClient.defaults.adapter = async (config) => {
      calls += 1;
      if (calls === 1) {
        const err = new axios.AxiosError('Unauthorized');
        err.config = config as InternalAxiosRequestConfig;
        err.response = {
          status: 401,
          data: { message: 'Unauthorized' },
          statusText: 'Unauthorized',
          headers: {},
          config: config as InternalAxiosRequestConfig,
        };
        throw err;
      }
      return {
        data: { ok: true },
        status: 200,
        statusText: 'OK',
        headers: {},
        config: config as InternalAxiosRequestConfig,
      };
    };

    const res = await apiClient.get('/monitor/logs');
    expect(res.data).toEqual({ ok: true });
    expect(calls).toBe(2);
    expect(localStorage.getItem(TOKEN_KEY)).toBe('new-token');
    expect(postSpy).toHaveBeenCalled();
    expect((window.location.assign as ReturnType<typeof vi.fn>)).not.toHaveBeenCalled();
  });

  it('logs out when refresh fails', async () => {
    vi.resetModules();
    localStorage.setItem(REFRESH_KEY, 'refresh-1');
    localStorage.setItem(TOKEN_KEY, 'old-token');
    localStorage.setItem('pos2sapAuth', '1');

    vi.spyOn(axios, 'post').mockRejectedValue(new Error('refresh failed'));

    const { default: apiClient } = await import('./apiClient');

    apiClient.defaults.adapter = async (config) => {
      const err = new axios.AxiosError('Unauthorized');
      err.config = config as InternalAxiosRequestConfig;
      err.response = {
        status: 401,
        data: { message: 'Unauthorized' },
        statusText: 'Unauthorized',
        headers: {},
        config: config as InternalAxiosRequestConfig,
      };
      throw err;
    };

    await expect(apiClient.get('/monitor/logs')).rejects.toThrow('Unauthorized');
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
    expect(localStorage.getItem(REFRESH_KEY)).toBeNull();
    expect(window.location.assign).toHaveBeenCalledWith('/login');
  });

  it('does not refresh auth endpoints on 401', async () => {
    vi.resetModules();
    localStorage.setItem(REFRESH_KEY, 'refresh-1');

    const postSpy = vi.spyOn(axios, 'post');

    const { default: apiClient } = await import('./apiClient');

    apiClient.defaults.adapter = async (config) => {
      const err = new axios.AxiosError('bad login');
      err.config = config as InternalAxiosRequestConfig;
      err.response = {
        status: 401,
        data: { message: 'bad login' },
        statusText: 'Unauthorized',
        headers: {},
        config: config as InternalAxiosRequestConfig,
      };
      throw err;
    };

    await expect(apiClient.post('/auth/login', {})).rejects.toThrow('bad login');
    expect(postSpy).not.toHaveBeenCalled();
    expect(window.location.assign).not.toHaveBeenCalled();
  });
});
