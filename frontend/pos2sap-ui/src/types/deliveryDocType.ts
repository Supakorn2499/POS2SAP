export interface DeliveryDocTypeDto {
  documentTypeId: number;
  documentTypeCode: string;
  documentTypeName: string;
  isEnabled: boolean;
}

export interface SaveDeliveryDocTypeDto {
  enabledDocumentTypeIds: number[];
}
