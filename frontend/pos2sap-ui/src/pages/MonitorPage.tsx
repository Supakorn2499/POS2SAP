// src/pages/MonitorPage.tsx
import { useState, useEffect } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Search, X, Play, RefreshCw, Download } from 'lucide-react';
import { toast } from 'sonner';
import monitorService from '@/services/monitorService';
import interfaceService from '@/services/interfaceService';
import { StatusBadge } from '@/components/StatusBadge';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { DateInputDdMmYyyy } from '@/components/DateInputDdMmYyyy';
import { useLanguage } from '@/contexts/LanguageContext';
import { fmtDate, fmtDatetime, fmt, todayStr, cn } from '@/lib/utils';
import { downloadCsv } from '@/lib/downloadCsv';
import type { BranchOptionDto, InterfaceLogDto, InterfaceLogQueryParams } from '@/types/monitor';

const STATUS_OPTIONS = ['', 'PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'RETRY'];

function interfaceTypeToTrigger(interfaceType: string): string {
  switch (interfaceType) {
    case 'AP': return 'IncomingPayment';
    case 'DL': return 'Delivery';
    default: return 'ARInvoice';
  }
}

const FILTER_KEY = 'monitorFilters';

function loadFilters() {
  try {
    const raw = sessionStorage.getItem(FILTER_KEY);
    if (raw) return JSON.parse(raw) as Record<string, string>;
  } catch { /* ignore */ }
  return null;
}

export default function MonitorPage() {
  const navigate = useNavigate();
  const { t } = useLanguage();

  // Restore last filter from sessionStorage (survives back-navigation)
  const _saved = loadFilters();

  // Pending (filter bar state)
  const [pendingSearch, setPendingSearch] = useState(_saved?.search ?? '');
  const [pendingStatus, setPendingStatus] = useState(_saved?.status ?? '');
  const [pendingInterface, setPendingInterface] = useState(_saved?.interfaceType ?? 'ARInvoice');
  const [pendingBranch, setPendingBranch] = useState(_saved?.branchCode ?? '');
  const [pendingDateFrom, setPendingDateFrom] = useState(_saved?.dateFrom ?? '');
  const [pendingDateTo, setPendingDateTo] = useState(_saved?.dateTo ?? '');

  const { data: branchOptions = [] } = useQuery<BranchOptionDto[]>({
    queryKey: ['monitor-branches'],
    queryFn: () => monitorService.getBranches(),
    staleTime: 5 * 60_000,
    retry: 1,
  });

  // Committed query params — restore from sessionStorage if available
  const [params, setParams] = useState<InterfaceLogQueryParams>(() => {
    const s = loadFilters();
    if (s) return {
      page: 1, pageSize: 20, sortBy: 'created_at', sortDirection: 'desc',
      interfaceType: s.interfaceType || 'ARInvoice',
      search:        s.search        || undefined,
      status:        s.status        || undefined,
      branchCode:    s.branchCode    || undefined,
      dateFrom:      s.dateFrom      || undefined,
      dateTo:        s.dateTo        || undefined,
    } as InterfaceLogQueryParams;
    return { page: 1, pageSize: 20, sortBy: 'created_at', sortDirection: 'desc', interfaceType: 'ARInvoice' };
  });

  // Persist committed params to sessionStorage whenever they change
  useEffect(() => {
    sessionStorage.setItem(FILTER_KEY, JSON.stringify({
      interfaceType: params.interfaceType ?? '',
      search:        params.search        ?? '',
      status:        params.status        ?? '',
      branchCode:    params.branchCode    ?? '',
      dateFrom:      params.dateFrom      ?? '',
      dateTo:        params.dateTo        ?? '',
    }));
  }, [params]);

  const [triggering, setTriggering] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [sendingId, setSendingId] = useState<string | null>(null);
  const [confirmDialog, setConfirmDialog] = useState<{
    isOpen: boolean;
    docNo: string;
    interfaceType: string;
  }>({ isOpen: false, docNo: '', interfaceType: '' });
  const [triggerConfirmOpen, setTriggerConfirmOpen] = useState(false);

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['monitor-logs', params],
    queryFn: () => monitorService.getLogs(params),
    staleTime: 30_000,
  });

  const queryClient = useQueryClient();

  const rows = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;

  function handleSearch() {
    console.debug('MonitorPage.handleSearch', { pendingSearch, pendingInterface, pendingStatus, pendingBranch, pendingDateFrom, pendingDateTo });
    const newParams = {
      search: pendingSearch || undefined,
      interfaceType: pendingInterface || undefined,
      status: pendingStatus || undefined,
      branchCode: pendingBranch || undefined,
      dateFrom: pendingDateFrom || undefined,
      dateTo: pendingDateTo || undefined,
      page: 1,
      pageSize: 20,
      sortBy: 'created_at',
      sortDirection: 'desc',
    } as InterfaceLogQueryParams;

    setParams(newParams);

    // Ensure we fetch using the exact params selected (avoid any stale query key timing)
    void queryClient.fetchQuery({
      queryKey: ['monitor-logs', newParams],
      queryFn: () => monitorService.getLogs(newParams),
    }).catch((err) => {
      // swallow - UI will show errors elsewhere
      console.error('fetchQuery failed', err);
    });
  }

  function handleClear() {
    setPendingSearch(''); setPendingStatus(''); setPendingBranch(''); setPendingInterface('ARInvoice');
    setPendingDateFrom(''); setPendingDateTo('');
    sessionStorage.removeItem(FILTER_KEY);
    setParams({ page: 1, pageSize: 20, sortBy: 'created_at', sortDirection: 'desc', interfaceType: 'ARInvoice' });
  }

  async function handleExportExcel() {
    setExporting(true);
    try {
      const exportParams: InterfaceLogQueryParams = {
        ...params,
        page: 1,
        pageSize: 100,
        includeJson: true,
      };
      const all: InterfaceLogDto[] = [];
      let page = 1;
      let totalPages = 1;
      do {
        const res = await monitorService.getLogs({ ...exportParams, page });
        all.push(...(res.items ?? []));
        totalPages = res.totalPages || 1;
        page++;
      } while (page <= totalPages);

      if (all.length === 0) {
        toast.error(t('exportEmpty'));
        return;
      }

      downloadCsv(
        `monitor-${params.interfaceType || 'all'}-${todayStr()}.csv`,
        [
          'PosDocNo', 'PosDocDate', 'BranchCode', 'BranchName', 'Channel',
          'InterfaceType', 'DocTotal', 'SapDocNum', 'Status', 'RetryCount',
          'ErrorMessage', 'SentAt', 'CreatedAt', 'PosData',
        ],
        all.map((r) => [
          r.posDocNo,
          r.posDocDate ? fmtDate(r.posDocDate) : '',
          r.branchCode ?? '',
          r.branchName ?? '',
          r.channel ?? '',
          r.interfaceType ?? '',
          r.docTotal ?? '',
          r.sapDocNum ?? '',
          r.status,
          r.retryCount,
          r.errorMessage ?? '',
          r.sentAt ? fmtDatetime(r.sentAt) : '',
          r.createdAt ? fmtDatetime(r.createdAt) : '',
          r.posData ?? '',
        ])
      );
      toast.success(t('exportSuccess', { count: all.length }));
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('exportEmpty'));
    } finally {
      setExporting(false);
    }
  }

  async function handleTrigger() {
    setTriggerConfirmOpen(true);
  }

  async function handleConfirmTrigger() {
    setTriggerConfirmOpen(false);
    setTriggering(true);
    try {
      const result = await interfaceService.triggerManualFor(pendingInterface);
      if (result.failed > 0) {
        toast.error(t('triggerPartialFail', { sent: result.sent, failed: result.failed }));
      } else {
        toast.success(t('triggerSuccess', { sent: result.sent }));
      }
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('triggerError'));
    } finally {
      setTriggering(false);
    }
  }

  async function handleRetry(id: string, e: React.MouseEvent) {
    e.stopPropagation();
    setRetryingId(id);
    try {
      await interfaceService.retryRecord(id);
      toast.success(t('retrySuccess'));
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('retryError'));
    } finally {
      setRetryingId(null);
    }
  }

  async function handleSend(posDocNo: string, interfaceType: string, e: React.MouseEvent) {
    e.stopPropagation();
    setConfirmDialog({ isOpen: true, docNo: posDocNo, interfaceType });
  }

  async function handleConfirmSend() {
    setSendingId(confirmDialog.docNo);
    try {
      const result = await interfaceService.triggerManualFor(
        confirmDialog.interfaceType,
        [confirmDialog.docNo]
      );
      if (result.failed > 0) {
        toast.error(t('triggerPartialFail', { sent: result.sent, failed: result.failed }));
      } else {
        toast.success(t('triggerSuccess', { sent: result.sent }));
      }
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('triggerError'));
    } finally {
      setSendingId(null);
      setConfirmDialog({ isOpen: false, docNo: '', interfaceType: '' });
    }
  }

  function handleCancelSend() {
    setConfirmDialog({ isOpen: false, docNo: '', interfaceType: '' });
  }

  return (
    <div className="space-y-4">
      {/* Confirm Dialog — Send single record */}
      <ConfirmDialog
        isOpen={confirmDialog.isOpen}
        title="ยืนยันการส่ง"
        message={`คุณต้องการส่ง ${confirmDialog.docNo} ไปที่ SAP ใช่หรือไม่?`}
        confirmText="ส่ง"
        cancelText="ยกเลิก"
        isLoading={sendingId === confirmDialog.docNo}
        onConfirm={handleConfirmSend}
        onCancel={handleCancelSend}
      />
      {/* Confirm Dialog — Trigger All */}
      <ConfirmDialog
        isOpen={triggerConfirmOpen}
        title="ยืนยัน Trigger All"
        message={`ต้องการส่งรายการ PENDING/RETRY ทั้งหมด (${pendingInterface}) ไปยัง SAP ใช่หรือไม่?`}
        confirmText="ยืนยัน"
        cancelText="ยกเลิก"
        isLoading={triggering}
        onConfirm={handleConfirmTrigger}
        onCancel={() => setTriggerConfirmOpen(false)}
      />

      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold">{t('monitor')}</h1>
          <p className="text-sm text-muted-foreground">{t('monitorSubtitle')}</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => void handleExportExcel()}
            disabled={exporting || isLoading}
            className="flex items-center gap-2 rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50"
          >
            <Download className="h-4 w-4" />
            {exporting ? t('exporting') : t('exportToExcel')}
          </button>
          <button
            onClick={handleTrigger}
            disabled={triggering}
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
          >
            <Play className="h-4 w-4" />
            {triggering ? t('triggerAllSending') : t('triggerAll')}
          </button>
        </div>
      </div>

      {/* Filter bar */}
      <div className="rounded-xl border bg-card p-4 shadow-sm">
        <div className="flex flex-wrap gap-3">
            <div className="shrink-0">
              <select
                value={pendingInterface}
                onChange={(e) => setPendingInterface(e.target.value)}
                className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
              >
                <option value="ARInvoice">ARInvoice</option>
                <option value="IncomingPayment">IncomingPayment</option>
                <option value="Delivery">Delivery</option>
              </select>
            </div>
          <div className="relative flex-1 min-w-48">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <input
              value={pendingSearch}
              onChange={(e) => setPendingSearch(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              placeholder={t('searchPlaceholder')}
              className="w-full rounded-md border bg-background pl-9 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <select
            value={pendingStatus}
            onChange={(e) => setPendingStatus(e.target.value)}
            className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          >
            {STATUS_OPTIONS.map((s) => (
              <option key={s} value={s}>{s ? t(`statusLabel.${s}`) : t('allStatuses')}</option>
            ))}
          </select>
          <select
            value={pendingBranch}
            onChange={(e) => setPendingBranch(e.target.value)}
            className="w-40 rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
          >
            <option value="">{t('allBranches')}</option>
            {branchOptions.map((branch) => (
              <option key={branch.branchCode} value={branch.branchCode}>
                {branch.branchName || branch.branchCode}
              </option>
            ))}
          </select>
          <DateInputDdMmYyyy
            value={pendingDateFrom}
            onChange={setPendingDateFrom}
            className="w-36"
          />
          <span className="self-center text-muted-foreground text-sm">{t('to')}</span>
          <DateInputDdMmYyyy
            value={pendingDateTo}
            min={pendingDateFrom || undefined}
            onChange={setPendingDateTo}
            className="w-36"
          />
          <button onClick={handleSearch}
            className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90">
            {t('searchButton')}
          </button>
          <button onClick={handleClear}
            className="flex items-center gap-1 rounded-md border px-3 py-2 text-sm hover:bg-muted">
            <X className="h-4 w-4" /> {t('clearButton')}
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="rounded-xl border bg-card shadow-sm">
        <div className="overflow-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50 text-left text-xs text-muted-foreground">
                <th className="px-4 py-2.5">{t('posDocNo')}</th>
                <th className="px-4 py-2.5">{t('billDate')}</th>
                <th className="px-4 py-2.5">{t('branch')}</th>
                <th className="px-4 py-2.5">{t('channel')}</th>
                <th className="px-4 py-2.5 text-right">{t('totalAmount')}</th>
                <th className="px-4 py-2.5">{t('sapDocNum')}</th>
                <th className="px-4 py-2.5">{t('status')}</th>
                <th className="px-4 py-2.5 text-center">{t('retry')}</th>
                <th className="px-4 py-2.5">{t('sentTime')}</th>
                <th className="px-4 py-2.5">{t('actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {isLoading || isFetching ? (
                <tr><td colSpan={10} className="px-4 py-8 text-center text-sm text-muted-foreground">กำลังโหลด...</td></tr>
              ) : rows.length === 0 ? (
                <tr><td colSpan={10} className="px-4 py-8 text-center text-sm text-muted-foreground">{t('noData')}</td></tr>
              ) : rows.map((r) => (
                <tr
                  key={r.id}
                  onClick={() => navigate(`/monitor/${r.id}`)}
                  className="hover:bg-muted/30 cursor-pointer"
                >
                  <td className="px-4 py-2 font-mono text-xs">{r.posDocNo}</td>
                  <td className="px-4 py-2">{fmtDate(r.posDocDate)}</td>
                  <td className="px-4 py-2 text-xs">{r.branchName || r.branchCode || '-'}</td>
                  <td className="px-4 py-2 text-xs">{r.channel ?? '-'}</td>
                  <td className="px-4 py-2 text-right font-mono text-xs">{fmt(r.docTotal)}</td>
                  <td className="px-4 py-2 font-mono text-xs text-muted-foreground">{r.sapDocNum ?? '-'}</td>
                  <td className="px-4 py-2"><StatusBadge status={r.status} size="sm" /></td>
                  <td className="px-4 py-2 text-center text-xs">{r.retryCount}</td>
                  <td className="px-4 py-2 text-xs text-muted-foreground">{fmtDatetime(r.sentAt)}</td>
                  <td className="px-4 py-2" onClick={(e) => e.stopPropagation()}>
                    {r.status === 'PENDING' && (
                      <button
                        onClick={(e) => handleSend(r.posDocNo, interfaceTypeToTrigger(r.interfaceType ?? 'AR'), e)}
                        disabled={sendingId === r.posDocNo}
                        className="flex items-center gap-1 rounded px-2 py-1 text-xs bg-blue-50 text-blue-700 hover:bg-blue-100 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {sendingId === r.posDocNo ? (
                          <>
                            <div className="h-3 w-3 border-2 border-blue-700 border-t-transparent rounded-full animate-spin" />
                            {t('sending')}
                          </>
                        ) : (
                          <>
                            <Play className="h-3 w-3" /> {t('send')}
                          </>
                        )}
                      </button>
                    )}
                    {(r.status === 'FAILED' || r.status === 'RETRY') && r.interfaceType === 'AR' && (
                      <button
                        onClick={(e) => handleRetry(r.id, e)}
                        disabled={retryingId === r.id}
                        className="flex items-center gap-1 rounded px-2 py-1 text-xs bg-amber-50 text-amber-700 hover:bg-amber-100 disabled:opacity-50 disabled:cursor-not-allowed dark:bg-amber-950/50 dark:text-amber-200 dark:hover:bg-amber-900/50"
                      >
                        {retryingId === r.id ? (
                          <>
                            <div className="h-3 w-3 border-2 border-amber-700 border-t-transparent rounded-full animate-spin" />
                            {t('retrying')}
                          </>
                        ) : (
                          <>
                            <RefreshCw className="h-3 w-3" /> {t('resend')}
                          </>
                        )}
                      </button>
                    )}
                    {(r.status === 'FAILED' || r.status === 'RETRY') && r.interfaceType === 'AP' && (
                      <button
                        onClick={(e) => handleSend(r.posDocNo, 'IncomingPayment', e)}
                        disabled={sendingId === r.posDocNo}
                        className="flex items-center gap-1 rounded px-2 py-1 text-xs bg-amber-50 text-amber-700 hover:bg-amber-100 disabled:opacity-50 disabled:cursor-not-allowed dark:bg-amber-950/50 dark:text-amber-200 dark:hover:bg-amber-900/50"
                      >
                        {sendingId === r.posDocNo ? (
                          <>
                            <div className="h-3 w-3 border-2 border-amber-700 border-t-transparent rounded-full animate-spin" />
                            {t('sending')}
                          </>
                        ) : (
                          <>
                            <RefreshCw className="h-3 w-3" /> {t('resend')}
                          </>
                        )}
                      </button>
                    )}
                    {(r.status === 'FAILED' || r.status === 'RETRY') && r.interfaceType === 'DL' && (
                      <button
                        onClick={(e) => handleRetry(r.id, e)}
                        disabled={retryingId === r.id}
                        className="flex items-center gap-1 rounded px-2 py-1 text-xs bg-amber-50 text-amber-700 hover:bg-amber-100 disabled:opacity-50 disabled:cursor-not-allowed dark:bg-amber-950/50 dark:text-amber-200 dark:hover:bg-amber-900/50"
                      >
                        {retryingId === r.id ? (
                          <>
                            <div className="h-3 w-3 border-2 border-amber-700 border-t-transparent rounded-full animate-spin" />
                            {t('retrying')}
                          </>
                        ) : (
                          <>
                            <RefreshCw className="h-3 w-3" /> {t('resend')}
                          </>
                        )}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-between border-t px-4 py-3 text-sm">
          <span className="text-muted-foreground">
            {t('showingRecords', { count: rows.length, total: total })}
          </span>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setParams((p) => ({ ...p, page: Math.max(1, (p.page ?? 1) - 1) }))}
              disabled={(params.page ?? 1) <= 1}
              className="rounded border px-3 py-1 text-xs hover:bg-muted disabled:opacity-40"
            >
              {t('previous')}
            </button>
            <span className="text-muted-foreground">
              หน้า {params.page ?? 1} / {totalPages}
            </span>
            <button
              onClick={() => setParams((p) => ({ ...p, page: Math.min(totalPages, (p.page ?? 1) + 1) }))}
              disabled={(params.page ?? 1) >= totalPages}
              className="rounded border px-3 py-1 text-xs hover:bg-muted disabled:opacity-40"
            >
              {t('next')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
