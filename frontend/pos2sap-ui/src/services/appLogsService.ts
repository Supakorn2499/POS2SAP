// src/services/appLogsService.ts
import apiClient from './apiClient';

export interface AppLogFileDto {
  fileName: string;
  sizeBytes: number;
  lastWriteUtc: string;
}

export interface AppLogContentDto {
  fileName: string;
  content: string;
  linesReturned: number;
  totalLines: number;
}

const appLogsService = {
  async list(): Promise<AppLogFileDto[]> {
    const res = await apiClient.get('/app-logs');
    return res.data.data ?? [];
  },

  async getTail(fileName: string, lines = 500, search?: string): Promise<AppLogContentDto> {
    const res = await apiClient.get(`/app-logs/${encodeURIComponent(fileName)}`, {
      params: { lines, search: search || undefined },
      timeout: 60000,
    });
    return res.data.data;
  },

  async clearOne(fileName: string): Promise<{ cleared: number; fileName: string }> {
    const res = await apiClient.delete(`/app-logs/${encodeURIComponent(fileName)}`);
    return res.data.data;
  },

  async clearAll(): Promise<{ cleared: number; failed?: string[] }> {
    const res = await apiClient.delete('/app-logs');
    return res.data.data;
  },
};

export default appLogsService;
