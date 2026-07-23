export interface ShopSapMappingDto {
  mappingID: number;
  shopID: number;
  shopCode: string;
  shopName: string;
  posSloc: string;
  posBranchCode: string;
  posVatBranch: string;
  sapCardCode: string | null;
  sapBranchCode: string | null;
  sapBranchName: string | null;
  sapVatBranch: string | null;
  isActive: boolean;
  sortOrder: number;
  remarks: string | null;
  updatedAt: string;
}

export interface UnmappedShopDto {
  shopID: number;
  shopCode: string;
  shopName: string;
}

export interface UpsertShopMappingDto {
  shopID: number;
  shopCode: string;
  shopName: string;
  sapCardCode: string | null;
  sapBranchCode: string | null;
  sapBranchName: string | null;
  sapVatBranch: string | null;
  isActive: boolean;
  sortOrder: number;
  remarks: string | null;
}
