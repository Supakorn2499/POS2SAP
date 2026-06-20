export type SapPayCategory = 'CASH' | 'TRANSFER' | 'CREDIT_CARD' | 'SKIP';

export interface PaytypeGlMappingDto {
  mappingID:      number;
  payTypeID:      number;
  payTypeName:    string;
  sapPayCategory: SapPayCategory;
  sapGlAccount:   string | null;
  sapPayTypeName: string | null;
  isActive:       boolean;
  sortOrder:      number;
  remarks:        string | null;
  updatedAt:      string;
}

export interface UnmappedPaytypeDto {
  payTypeID:   number;
  payTypeName: string;
}

export interface UpsertGlMappingDto {
  payTypeID:      number;
  payTypeName:    string;
  sapPayCategory: SapPayCategory;
  sapGlAccount:   string | null;
  sapPayTypeName: string | null;
  isActive:       boolean;
  sortOrder:      number;
  remarks:        string | null;
}
