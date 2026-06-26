export interface ProductGroupSapMappingDto {
  mappingID: number;
  productGroupID: number;
  productGroupCode: string;
  productGroupName: string;
  sapItemGroupCode: string | null;
  sapItemGroupName: string | null;
  isActive: boolean;
  sortOrder: number;
  remarks: string | null;
  updatedAt: string;
}

export interface UnmappedProductGroupDto {
  productGroupID: number;
  productGroupCode: string;
  productGroupName: string;
}

export interface UpsertProductGroupMappingDto {
  productGroupID: number;
  productGroupCode: string;
  productGroupName: string;
  sapItemGroupCode: string | null;
  sapItemGroupName: string | null;
  isActive: boolean;
  sortOrder: number;
  remarks: string | null;
}
