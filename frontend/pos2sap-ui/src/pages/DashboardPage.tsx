// src/pages/DashboardPage.tsx
import { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Clock, CheckCircle, XCircle, RefreshCw, Activity, Play, Download, MoreHorizontal } from 'lucide-react';
import { toast } from 'sonner';
import { useLanguage } from '@/contexts/LanguageContext';
import dashboardService from '@/services/dashboardService';
import interfaceService from '@/services/interfaceService';
import monitorService from '@/services/monitorService';
import { StatCard } from '@/components/StatCard';
import { StatusBadge } from '@/components/StatusBadge';
import type { BranchOptionDto, InterfaceLogDto, PagedResult } from '@/types/monitor';

const PAGE_SIZE = 10;

// =================================================================
// TopBranchesCard Component
// =================================================================
function TopBranchesCard({ title, branches, isLoading, t }: { title: string; branches: { branchCode: string; branchName?: string; total: number; success: number; failed: number; }[]; isLoading: boolean; t: (key: string, params?: Record<string, string | number>) => string }) {
  return (
    <div className="rounded-xl border bg-card text-card-foreground shadow">
      <div className="p-6">
        <h3 className="font-semibold">{title}</h3>
      </div>
      <div className="px-6 pb-6">
        {isLoading ? (
          <div className="h-48 animate-pulse rounded-md bg-muted" />
        ) : branches.length > 0 ? (
          <div className="space-y-4">
            {branches.map(branch => (
              <div key={branch.branchCode} className="flex items-center">
                <div>
                  <p className="text-sm font-medium leading-none">{branch.branchName || branch.branchCode}</p>
                  <p className="text-xs text-muted-foreground">{t('code')}: {branch.branchCode}</p>
                </div>
                <div className="ml-auto font-medium text-right">
                  <p className="text-sm">{branch.failed > 0 && <span className="text-destructive">{branch.failed} {t('failed')}</span>}</p>
                  <p className="text-xs text-muted-foreground">{t('total')}: {branch.total}</p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-center text-muted-foreground py-10">{t('noData')}</p>
        )}
      </div>
    </div>
  );
}

// =================================================================
// RecentLogsCard Component
// =================================================================
function RecentLogsCard({
  logs,
  isLoading,
  page,
  totalPages,
  branchFilter,
  branchOptions,
  statusFilter,
  onBranchChange,
  onStatusChange,
  onPageChange,
  onResendSuccess,
  t,
}: {
  logs: InterfaceLogDto[];
  isLoading: boolean;
  page: number;
  totalPages: number;
  branchFilter: string;
  branchOptions: BranchOptionDto[];
  onBranchChange: (value: string) => void;
  statusFilter: 'ALL' | 'FAILED' | 'RETRY';
  onStatusChange: (value: 'ALL' | 'FAILED' | 'RETRY') => void;
  onPageChange: (page: number) => void;
  onResendSuccess: () => void;
  t: (key: string, params?: Record<string, string | number>) => string;
}) {
  const [resendingId, setResendingId] = useState<string | null>(null);

  const { mutate: resendMutation } = useMutation({
    mutationFn: (id: string) => interfaceService.retryRecord(id),
    onSuccess: (_, id) => {
      toast.success(t('logResendSuccess', { id }));
      onResendSuccess();
    },
    onError: (err: unknown, id) => {
      toast.error(`${t('resendFailed', { id })}: ${err instanceof Error ? err.message : 'Unknown error'}`);
    },
    onSettled: () => {
      setResendingId(null);
    },
  });

  const handleResend = (id: string) => {
    setResendingId(id);
    resendMutation(id);
  };
  
  return (
    <div className="rounded-xl border bg-card text-card-foreground shadow col-span-1 lg:col-span-3">
      <div className="p-6">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="font-semibold">{t('recentLogsTitle')}</h3>
            <p className="text-xs text-muted-foreground">{t('recentLogsSubtitle')}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center gap-2 rounded-lg border bg-background px-3 py-2 text-sm">
              <label htmlFor="branchFilter" className="text-xs text-muted-foreground">{t('branch')}</label>
              <select
                id="branchFilter"
                value={branchFilter}
                onChange={(e) => onBranchChange(e.target.value)}
                className="w-40 bg-transparent text-sm outline-none"
              >
                <option value="">{t('allBranches')}</option>
                {branchOptions.map((branch) => (
                  <option key={branch.branchCode} value={branch.branchCode}>
                    {branch.branchName || branch.branchCode}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex items-center gap-2 rounded-lg border bg-background px-3 py-2 text-sm">
              <label htmlFor="statusFilter" className="text-xs text-muted-foreground">{t('status')}</label>
              <select
                id="statusFilter"
                value={statusFilter}
                onChange={(e) => onStatusChange(e.target.value as 'ALL' | 'FAILED' | 'RETRY')}
                className="bg-transparent text-sm outline-none"
              >
                <option value="ALL">{t('allStatuses')}</option>
                <option value="FAILED">{t('failed')}</option>
                <option value="RETRY">{t('retry')}</option>
              </select>
            </div>
          </div>
        </div>
      </div>
      <div className="overflow-x-auto">
        {isLoading ? (
          <div className="h-64 animate-pulse rounded-md bg-muted m-6" />
        ) : logs.length > 0 ? (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">{t('posDocNo')}</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">{t('branchLabel')}</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">{t('status')}</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">{t('actions')}</th>
              </tr>
            </thead>
            <tbody>
              {logs.map(log => (
                <tr key={log.id} className="border-b">
                  <td className="px-4 py-3 font-mono">{log.posDocNo}</td>
                  <td className="px-4 py-3">{log.branchName || log.branchCode}</td>
                  <td className="px-4 py-3"><StatusBadge status={log.status} /></td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <Link to={`/monitor/${log.id}`} className="text-primary hover:underline text-xs">{t('details')}</Link>
                      {(log.status === 'FAILED' || log.status === 'RETRY') && (
                        <>
                          <span className="text-muted-foreground">|</span>
                          <button
                            onClick={() => handleResend(log.id)}
                            disabled={resendingId === log.id}
                            className="text-orange-600 hover:underline text-xs disabled:opacity-50"
                          >
                            {resendingId === log.id ? t('triggerAllSending') : t('resend')}
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="py-20 text-center text-sm text-muted-foreground">{t('noRecentLogs')}</p>
        )}
      </div>
      <div className="px-6 py-4 flex flex-wrap items-center justify-between gap-2 text-sm">
        <span className="text-muted-foreground">{t('pageOf', { page, total: totalPages })}</span>
        <div className="flex items-center gap-2">
          <button
            onClick={() => onPageChange(Math.max(1, page - 1))}
            disabled={page <= 1}
            className="rounded border px-3 py-1 text-xs hover:bg-muted disabled:opacity-40"
          >
            {t('previous')}
          </button>
          <button
            onClick={() => onPageChange(Math.min(totalPages, page + 1))}
            disabled={page >= totalPages}
            className="rounded border px-3 py-1 text-xs hover:bg-muted disabled:opacity-40"
          >
            {t('next')}
          </button>
        </div>
      </div>
    </div>
  );
}


// =================================================================
// DashboardPage Component
// =================================================================
export default function DashboardPage() {
  const { t } = useLanguage();
  const [triggering, setTriggering] = useState(false);
  const [importing, setImporting] = useState(false);
  const [monthOffset, setMonthOffset] = useState(0);
  const [branchFilter, setBranchFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'FAILED' | 'RETRY'>('ALL');
  const [page, setPage] = useState(1);

  const { data: branchOptions = [] } = useQuery<BranchOptionDto[]>({
    queryKey: ['branches'],
    queryFn: () => monitorService.getBranches(),
    staleTime: 5 * 60_000,
    retry: 1,
  });

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['dashboard', monthOffset],
    queryFn: () => dashboardService.getDashboard(monthOffset),
    staleTime: 60_000,
    retry: 1,
  });

  const getDateRange = (offset: number) => {
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth() - offset, 1);
    const end = new Date(start.getFullYear(), start.getMonth() + 1, 1);
    return {
      dateFrom: start.toISOString().slice(0, 10),
      dateTo: end.toISOString().slice(0, 10),
    };
  };

  const monthRange = getDateRange(monthOffset);

  const {
    data: recentLogsPage,
    isLoading: recentLogsLoading,
    refetch: refetchRecentLogs,
  } = useQuery<PagedResult<InterfaceLogDto>>({
    queryKey: ['recentLogs', monthOffset, branchFilter, statusFilter, page],
    queryFn: () => monitorService.getLogs({
      status: statusFilter === 'ALL' ? 'FAILED,RETRY' : statusFilter,
      branchCode: branchFilter.trim() || undefined,
      dateFrom: monthRange.dateFrom,
      dateTo: monthRange.dateTo,
      page,
      pageSize: PAGE_SIZE,
      sortBy: 'created_at',
      sortDirection: 'desc',
    }),
    staleTime: 30_000,
    retry: 1,
  });

  const counts = data?.counts;
  const monthLabel = monthOffset === 0 ? t('currentMonth') : t('lastMonth');

  async function handleTrigger() {
    setTriggering(true);
    try {
      const result = await interfaceService.triggerManual();
      toast.success(t('triggerSuccess', { sent: result.sent }));
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('triggerError'));
    } finally {
      setTriggering(false);
    }
  }

  async function handleImport() {
    setImporting(true);
    try {
      const result = await interfaceService.importPreview();
      if (result.error && result.fetched === 0) {
        toast.error(`${t('importError')}: ${result.error}`);
      } else if (result.fetched === 0) {
        toast.info(t('importNoData'));
      } else if (result.imported === 0) {
        toast.info(t('importAllImported', { fetched: result.fetched }));
      } else {
        toast.success(`${t('importSuccess', { fetched: result.fetched, imported: result.imported })}`);
      }
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('importError'));
    } finally {
      setImporting(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold">{t('dashboard')}</h1>
          <p className="text-sm text-muted-foreground">{t('dashboardSubtitle', { month: monthLabel })}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <button
              onClick={() => { setMonthOffset(0); setPage(1); }}
              className={`rounded-full px-3 py-2 text-xs font-medium transition ${monthOffset === 0 ? 'bg-primary text-primary-foreground' : 'bg-muted/60 text-muted-foreground hover:bg-muted'}`}
            >
            {t('currentMonth')}
            </button>
            <button
              onClick={() => { setMonthOffset(1); setPage(1); }}
              className={`rounded-full px-3 py-2 text-xs font-medium transition ${monthOffset === 1 ? 'bg-primary text-primary-foreground' : 'bg-muted/60 text-muted-foreground hover:bg-muted'}`}
            >
            {t('lastMonth')}
            </button>
          </div>
          <button
            onClick={handleImport}
            disabled={importing}
            className="flex items-center gap-2 rounded-lg border border-green-500 px-4 py-2 text-sm font-medium text-green-700 hover:bg-green-50 disabled:opacity-50"
          >
            <Download className="h-4 w-4" />
            {importing ? t('importing') : t('importFromPOS')}
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
      
      {isError && (
        <div className="rounded-lg border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          {t('loadError')}
        </div>
      )}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">
        <StatCard title={t('pending')} value={counts?.pending ?? 0} icon={Clock} variant="warning" loading={isLoading} />
        <StatCard title={t('processing')} value={counts?.processing ?? 0} icon={Activity} variant="info" loading={isLoading} />
        <StatCard title={t('success')} value={counts?.success ?? 0} icon={CheckCircle} variant="success" loading={isLoading} />
        <StatCard title={t('failed')} value={counts?.failed ?? 0} icon={XCircle} variant="danger" loading={isLoading} />
        <StatCard title={t('retry')} value={counts?.retry ?? 0} icon={RefreshCw} variant="orange" loading={isLoading} />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
        <RecentLogsCard
          logs={recentLogsPage?.items ?? []}
          isLoading={recentLogsLoading}
          page={page}
          totalPages={recentLogsPage?.totalPages ?? 1}
          branchFilter={branchFilter}
          branchOptions={branchOptions}
          statusFilter={statusFilter}
          onBranchChange={(value) => { setBranchFilter(value); setPage(1); }}
          onStatusChange={(value) => { setStatusFilter(value); setPage(1); }}
          onPageChange={setPage}
          onResendSuccess={() => refetchRecentLogs()}
          t={t}
        />
        <div className="space-y-6">
          <TopBranchesCard title={t('topFailedBranches')} branches={data?.topFailedBranches ?? []} isLoading={isLoading} t={t} />
          <TopBranchesCard title={t('topBranches')} branches={data?.topBranches ?? []} isLoading={isLoading} t={t} />
        </div>
      </div>
    </div>
  );
}
