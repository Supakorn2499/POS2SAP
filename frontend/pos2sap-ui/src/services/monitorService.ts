// src/services/monitorService.ts
import apiClient from './apiClient';
import type { BranchOptionDto, InterfaceLogDto, InterfaceLogDetailDto, InterfaceLogQueryParams, PagedResult } from '@/types/monitor';

const monitorService = {
  async getLogs(params: InterfaceLogQueryParams): Promise<PagedResult<InterfaceLogDto>> {
    const res = await apiClient.get('/monitor/logs', { params });
    return res.data.data;
  },

  async getBranches(): Promise<BranchOptionDto[]> {
    const res = await apiClient.get('/monitor/branches');
    return res.data.data;
  },

  async getDetail(id: string): Promise<InterfaceLogDetailDto> {
    const res = await apiClient.get(`/monitor/logs/${id}`);
    return res.data.data;
  },
};

export default monitorService;
