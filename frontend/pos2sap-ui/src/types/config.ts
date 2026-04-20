// src/types/config.ts

export interface InterfaceConfigDto {
  id: string;
  configKey: string;
  configValue?: string;
  description?: string;
  isActive: boolean;
  updatedAt: string;
}

export interface UpdateConfigDto {
  configValue: string;
}
