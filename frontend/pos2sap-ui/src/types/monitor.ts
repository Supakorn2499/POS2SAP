// src/types/monitor.ts

export type InterfaceStatus = 'PENDING' | 'PROCESSING' | 'SUCCESS' | 'FAILED' | 'RETRY';

export interface InterfaceLogDto {
  id: string;
  posDocNo: string;
  posDocDate?: string;
  branchCode?: string;
  branchName?: string;
  posId?: string;
  cardCode?: string;
  channel?: string;
  docTotal?: number;
  sapDocNum?: string;
  status: InterfaceStatus;
  errorMessage?: string;
  retryCount: number;
  sentAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface InterfaceLogDetailDto extends InterfaceLogDto {
  posData?: string;
  sapRequest?: string;
  sapResponse?: string;
}

export interface InterfaceLogQueryParams {
  search?: string;
  status?: string;
  interfaceType?: string;
  branchCode?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface BranchOptionDto {
  branchCode: string;
  branchName: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
