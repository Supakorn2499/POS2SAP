// src/types/dashboard.ts
import type { InterfaceLogDto } from './monitor';

export type { InterfaceLogDto };

export interface StatusCountDto {
  pending: number;
  processing: number;
  success: number;
  failed: number;
  retry: number;
  total: number;
}


export interface DailyTrendDto {
  date: string;   // yyyy-MM-dd
  success: number;
  failed: number;
  total: number;
}

export interface BranchStatDto {
  branchCode: string;
  branchName?: string;
  total: number;
  success: number;
  failed: number;
}

export interface DashboardSummaryDto {
  counts: StatusCountDto;
  dailyTrend: DailyTrendDto[];
  topBranches: BranchStatDto[];
  topFailedBranches: BranchStatDto[];
  recentLogs: InterfaceLogDto[];
}
