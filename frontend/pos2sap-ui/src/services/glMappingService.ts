import apiClient from './apiClient';
import type { PaytypeGlMappingDto, UnmappedPaytypeDto, UpsertGlMappingDto } from '@/types/glMapping';

const glMappingService = {
  async getAll(): Promise<PaytypeGlMappingDto[]> {
    const res = await apiClient.get('/glmapping');
    return res.data.data;
  },

  async getUnmapped(): Promise<UnmappedPaytypeDto[]> {
    const res = await apiClient.get('/glmapping/unmapped');
    return res.data.data;
  },

  async upsert(dto: UpsertGlMappingDto): Promise<boolean> {
    const res = await apiClient.post('/glmapping', dto);
    if (!res.data?.success) {
      throw new Error(res.data?.message || 'Upsert GL mapping failed');
    }
    return true;
  },

  async remove(payTypeId: number): Promise<boolean> {
    const res = await apiClient.delete(`/glmapping/${payTypeId}`);
    return res.data.success;
  },
};

export default glMappingService;
