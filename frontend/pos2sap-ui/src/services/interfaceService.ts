// src/services/interfaceService.ts
import apiClient from './apiClient';

const interfaceService = {
  async triggerManual(docNos?: string[]): Promise<{ sent: number; failed: number; total: number }> {
    const res = await apiClient.post('/interface/trigger', { docNos });
    return res.data.data;
  },

  async retryRecord(id: string): Promise<boolean> {
    const res = await apiClient.post(`/interface/retry/${id}`);
    return res.data.data;
  },

  async importPreview(docNos?: string[]): Promise<{ fetched: number; imported: number; error?: string }> {
    const res = await apiClient.post('/interface/import', { docNos }, { timeout: 180000 });
    return res.data.data;
  },
};

export default interfaceService;
