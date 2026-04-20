// src/services/dashboardService.ts
import apiClient from './apiClient';
import type { DashboardSummaryDto } from '@/types/dashboard';

const dashboardService = {
  async getDashboard(monthOffset = 0): Promise<DashboardSummaryDto> {
    const res = await apiClient.get('/monitor/dashboard', { params: { monthOffset } });
    return res.data.data;
  },
};

export default dashboardService;
