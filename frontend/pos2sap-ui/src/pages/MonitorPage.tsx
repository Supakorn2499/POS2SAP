// src/pages/MonitorPage.tsx
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Search, X, Play, RefreshCw, Zap } from 'lucide-react';
import { toast } from 'sonner';
import monitorService from '@/services/monitorService';
import interfaceService from '@/services/interfaceService';
import { StatusBadge } from '@/components/StatusBadge';
import { useLanguage } from '@/contexts/LanguageContext';
import { useAuth } from '@/contexts/AuthContext';
import { fmtDate, fmtDatetime, fmt, todayStr, cn } from '@/lib/utils';
import type { BranchOptionDto, InterfaceLogQueryParams } from '@/types/monitor';

const STATUS_OPTIONS = ['', 'PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'RETRY'];

export default function MonitorPage() {
  const navigate = useNavigate();
  const { t } = useLanguage();
  const { username } = useAuth();

  // Pending (filter bar state)
  const [pendingSearch, setPendingSearch] = useState('');
  const [pendingStatus, setPendingStatus] = useState('');
  const [pendingBranch, setPendingBranch] = useState('');
  const [pendingDateFrom, setPendingDateFrom] = useState('');
  const [pendingDateTo, setPendingDateTo] = useState('');

  const { data: branchOptions = [] } = useQuery<BranchOptionDto[]>({
    queryKey: ['monitor-branches'],
    queryFn: () => monitorService.getBranches(),
    staleTime: 5 * 60_000,
    retry: 1,
  });

  // Committed query params
  const [params, setParams] = useState<InterfaceLogQueryParams>({
    page: 1, pageSize: 20, sortBy: 'created_at', sortDirection: 'desc'
  });

  const [triggering, setTriggering] = useState(false);
  const [simulating, setSimulating] = useState(false);

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['monitor-logs', params],
    queryFn: () => monitorService.getLogs(params),
    staleTime: 30_000,
  });

  const rows = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;

  function handleSearch() {
    setParams({
      search: pendingSearch || undefined,
      status: pendingStatus || undefined,
      branchCode: pendingBranch || undefined,
      dateFrom: pendingDateFrom || undefined,
      dateTo: pendingDateTo || undefined,
      page: 1,
      pageSize: 20,
      sortBy: 'created_at',
      sortDirection: 'desc',
    });
  }

  function handleClear() {
    setPendingSearch(''); setPendingStatus(''); setPendingBranch('');
    setPendingDateFrom(''); setPendingDateTo('');
    setParams({ page: 1, pageSize: 20, sortBy: 'created_at', sortDirection: 'desc' });
  }

  async function handleTrigger() {
    setTriggering(true);
    try {
      const result = await interfaceService.triggerManual();
      toast.success(t('triggerSuccess'));
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('triggerError'));
    } finally {
      setTriggering(false);
    }
  }

  async function handleSimulate() {
    setSimulating(true);
    try {
      const result = await monitorService.simulateStatuses();
      toast.success(result);
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : 'เกิดข้อผิดพลาดในการจำลอง');
    } finally {
      setSimulating(false);
    }
  }

  async function handleRetry(id: string, e: React.MouseEvent) {
    e.stopPropagation();
    try {
      await interfaceService.retryRecord(id);
      toast.success(t('retrySuccess'));
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('retryError'));
    }
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold">{t('monitor')}</h1>
          <p className="text-sm text-muted-foreground">{t('monitorSubtitle')}</p>
        </div>
        <div className="flex gap-2">
          {username === 'vtec' && (
            <button
              onClick={handleSimulate}
              disabled={simulating}
              className="flex items-center gap-2 rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-50"
            >
              <Zap className="h-4 w-4" />
              {simulating ? 'กำลังจำลอง...' : 'Simulate Status'}
            </button>
          )}
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
          <input type="date" value={pendingDateFrom} onChange={(e) => setPendingDateFrom(e.target.value)}
            className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
          <span className="self-center text-muted-foreground text-sm">{t('to')}</span>
          <input type="date" value={pendingDateTo} onChange={(e) => setPendingDateTo(e.target.value)}
            className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring" />
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
                    {(r.status === 'FAILED' || r.status === 'RETRY') && (
                      <button
                        onClick={(e) => handleRetry(r.id, e)}
                        className="flex items-center gap-1 rounded px-2 py-1 text-xs bg-orange-50 text-orange-700 hover:bg-orange-100"
                      >
                        <RefreshCw className="h-3 w-3" /> {t('retry')}
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
