import apiClient from './apiClient';
import type {
  ProductGroupSapMappingDto,
  UnmappedProductGroupDto,
  UpsertProductGroupMappingDto,
} from '@/types/productGroupMapping';

const productGroupMappingService = {
  async getAll(): Promise<ProductGroupSapMappingDto[]> {
    const res = await apiClient.get('/productgroupmapping');
    return res.data.data;
  },

  async getUnmapped(): Promise<UnmappedProductGroupDto[]> {
    const res = await apiClient.get('/productgroupmapping/unmapped');
    return res.data.data;
  },

  async upsert(dto: UpsertProductGroupMappingDto): Promise<boolean> {
    const res = await apiClient.post('/productgroupmapping', dto);
    return res.data.success;
  },

  async remove(productGroupId: number): Promise<boolean> {
    const res = await apiClient.delete(`/productgroupmapping/${productGroupId}`);
    return res.data.success;
  },
};

export default productGroupMappingService;
