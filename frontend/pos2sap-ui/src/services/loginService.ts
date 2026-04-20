import apiClient from './apiClient';
import type { LoginResultDto } from '@/types/auth';

interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string;
  statusCode: number;
  errors: string[];
}

const login = async (staffLogin: string, staffPassword: string) => {
  const response = await apiClient.post<ApiResponse<LoginResultDto>>('/auth/login', {
    staffLogin,
    staffPassword,
  });

  return response.data.data;
};

export default { login };
