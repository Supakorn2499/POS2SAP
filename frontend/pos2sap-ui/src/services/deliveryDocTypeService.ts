import apiClient from './apiClient';
import type { DeliveryDocTypeDto, SaveDeliveryDocTypeDto } from '@/types/deliveryDocType';

const deliveryDocTypeService = {
  async getAll(): Promise<DeliveryDocTypeDto[]> {
    const res = await apiClient.get('/delivery-doctype');
    return res.data.data;
  },

  async save(dto: SaveDeliveryDocTypeDto): Promise<boolean> {
    const res = await apiClient.put('/delivery-doctype', dto);
    return res.data.success;
  },
};

export default deliveryDocTypeService;
