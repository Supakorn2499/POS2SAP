// src/services/interfaceService.ts
import apiClient from './apiClient';
import type { ImportPreviewRequest, ImportPreviewItem } from '@/types/import';

const interfaceService = {
  async triggerManual(docNos?: string[]): Promise<{ sent: number; failed: number; total: number }> {
    const res = await apiClient.post('/interface/trigger', { docNos });
    return res.data.data;
  },
  
  async retryRecord(id: string): Promise<boolean> {
    const res = await apiClient.post(`/interface/retry/${id}`);
    return res.data.data;
  },

  async importPreview(docNos?: string[], interfaceType?: string, branchCode?: string): Promise<{ fetched: number; imported: number; error?: string }> {
    const payload: any = { docNos };
    if (interfaceType) payload.interfaceType = interfaceType;
    if (branchCode) payload.branchCode = branchCode;
    const res = await apiClient.post('/interface/import', payload, { timeout: 180000 });
    return res.data.data;
  },

  async triggerManualFor(interfaceType?: string, docNos?: string[]): Promise<{ sent: number; failed: number; total: number }> {
    const payload: any = { docNos };
    if (interfaceType) payload.interfaceType = interfaceType;
    const res = await apiClient.post('/interface/trigger', payload, { timeout: 180000 });
    return res.data.data;
  },

  async previewImport(params: ImportPreviewRequest): Promise<ImportPreviewItem[]> {
    const res = await apiClient.post('/interface/preview', params, { timeout: 180000 });
    return res.data.data ?? [];
  },
};

export default interfaceService;
