// src/services/apiClient.ts
import axios from 'axios';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
});

// Add Authorization header with JWT token
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('pos2sapToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (err) => Promise.reject(err)
);

apiClient.interceptors.response.use(
  (res) => res,
  (err) => {
    const msg = err.response?.data?.message ?? err.message ?? 'เกิดข้อผิดพลาด';
    return Promise.reject(new Error(msg));
  }
);

export default apiClient;
