import apiClient from './apiClient';
import type { ShopSapMappingDto, UnmappedShopDto, UpsertShopMappingDto } from '@/types/shopMapping';

const shopMappingService = {
  async getAll(): Promise<ShopSapMappingDto[]> {
    const res = await apiClient.get('/shopmapping');
    return res.data.data;
  },

  async getUnmapped(): Promise<UnmappedShopDto[]> {
    const res = await apiClient.get('/shopmapping/unmapped');
    return res.data.data;
  },

  async upsert(dto: UpsertShopMappingDto): Promise<boolean> {
    const res = await apiClient.post('/shopmapping', dto);
    if (!res.data?.success) throw new Error(res.data?.message || 'Upsert shop mapping failed');
    return res.data.data;
  },

  async remove(shopId: number): Promise<boolean> {
    const res = await apiClient.delete(`/shopmapping/${shopId}`);
    return res.data.data;
  },
};

export default shopMappingService;
