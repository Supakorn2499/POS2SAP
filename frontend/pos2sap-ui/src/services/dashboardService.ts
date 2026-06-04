// src/services/dashboardService.ts
import apiClient from './apiClient';
import type { DashboardSummaryDto } from '@/types/dashboard';

const dashboardService = {
  async getDashboard(monthOffset = 0, interfaceType?: string): Promise<DashboardSummaryDto> {
    const res = await apiClient.get('/monitor/dashboard', { params: { monthOffset, interfaceType } });
    return res.data.data;
  },
};

export default dashboardService;
