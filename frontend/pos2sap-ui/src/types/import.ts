// src/types/import.ts

export interface ImportPreviewRequest {
  dateFrom: string;
  dateTo: string;
  branchCode?: string;
  interfaceType?: string;
  batchSize?: number;
}

export interface ImportPreviewItem {
  docNum: string;
  docDate: string;
  branchCode: string;
  branchName: string;
  channel: string;
  docTotal: number;
  alreadyImported: boolean;
}
