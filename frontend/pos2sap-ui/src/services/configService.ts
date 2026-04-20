// src/services/configService.ts
import apiClient from './apiClient';
import type { InterfaceConfigDto } from '@/types/config';

const configService = {
  async getConfigs(): Promise<InterfaceConfigDto[]> {
    const res = await apiClient.get('/config');
    return res.data.data;
  },

  async getConfigByKey(key: string): Promise<InterfaceConfigDto> {
    const res = await apiClient.get(`/config/${key}`);
    return res.data.data;
  },

  async updateConfig(key: string, value: string): Promise<boolean> {
    const res = await apiClient.put(`/config/${key}`, { configValue: value });
    return res.data.success;
  },
};

export default configService;
