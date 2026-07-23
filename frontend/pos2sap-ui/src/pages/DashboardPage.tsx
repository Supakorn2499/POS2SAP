// src/pages/DashboardPage.tsx
import { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Clock, CheckCircle, XCircle, RefreshCw, Activity, LayoutDashboard } from 'lucide-react';
import { toast } from 'sonner';
import { useLanguage } from '@/contexts/LanguageContext';
import dashboardService from '@/services/dashboardService';
import monitorService from '@/services/monitorService';
import { resendInterfaceLog } from '@/lib/interfaceResend';
import { cn } from '@/lib/utils';
import { StatCard } from '@/components/StatCard';
import { StatusBadge } from '@/components/StatusBadge';
import { AppSelect } from '@/components/ui/AppSelect';
import { PageHeader } from '@/components/PageHeader';
import type { BranchOptionDto, InterfaceLogDto, PagedResult } from '@/types/monitor';

const PAGE_SIZE = 10;

// =================================================================
// TopBranchesCard Component
// =================================================================
function TopBranchesCard({ title, branches, isLoading, t }: { title: string; branches: { branchCode: string; branchName?: string; total: number; success: number; failed: number; }[]; isLoading: boolean; t: (key: string, params?: Record<string, string | number>) => string }) {
  if (!isLoading && branches.length === 0) return null;

  return (
    <div className="rounded-2xl border bg-card text-card-foreground shadow-sm">
      <div className="p-5">
        <h3 className="text-sm font-semibold tracking-tight">{title}</h3>
      </div>
      <div className="px-5 pb-5">
        {isLoading ? (
          <div className="h-48 animate-pulse rounded-md bg-muted" />
        ) : (
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
    mutationFn: ({ id, posDocNo, interfaceType }: { id: string; posDocNo: string; interfaceType?: string }) =>
      resendInterfaceLog(id, posDocNo, interfaceType),
    onSuccess: (ok, { id }) => {
      if (ok) {
        toast.success(t('logResendSuccess', { id }));
        onResendSuccess();
      } else {
        toast.error(t('resendFailed', { id }));
      }
    },
    onError: (err: unknown, { id }) => {
      toast.error(`${t('resendFailed', { id })}: ${err instanceof Error ? err.message : 'Unknown error'}`);
    },
    onSettled: () => {
      setResendingId(null);
    },
  });

  const handleResend = (log: InterfaceLogDto) => {
    setResendingId(log.id);
    resendMutation({ id: log.id, posDocNo: log.posDocNo, interfaceType: log.interfaceType });
  };
  
  return (
    <div className="rounded-2xl border bg-card text-card-foreground shadow-sm col-span-1 xl:col-span-3">
      <div className="p-6">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 className="font-semibold">{t('recentLogsTitle')}</h3>
            <p className="text-xs text-muted-foreground">{t('recentLogsSubtitle')}</p>
          </div>
          <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center">
            <AppSelect
              id="branchFilter"
              value={branchFilter}
              onChange={(e) => onBranchChange(e.target.value)}
              wrapperClassName="sm:min-w-[11rem]"
              aria-label={t('branch')}
            >
              <option value="">{t('allBranches')}</option>
              {branchOptions.map((branch) => (
                <option key={branch.branchCode} value={branch.branchCode}>
                  {branch.branchName || branch.branchCode}
                </option>
              ))}
            </AppSelect>
            <AppSelect
              id="statusFilter"
              value={statusFilter}
              onChange={(e) => onStatusChange(e.target.value as 'ALL' | 'FAILED' | 'RETRY')}
              wrapperClassName="sm:min-w-[9rem]"
              aria-label={t('status')}
            >
              <option value="ALL">{t('allStatuses')}</option>
              <option value="FAILED">{t('failed')}</option>
              <option value="RETRY">{t('retry')}</option>
            </AppSelect>
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
                            onClick={() => handleResend(log)}
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
      {totalPages > 0 && (
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
      )}
    </div>
  );
}


// =================================================================
// DashboardPage Component
// =================================================================
export default function DashboardPage() {
  const { t } = useLanguage();
  const [monthOffset, setMonthOffset] = useState(0);
  const [interfaceType, setInterfaceType] = useState('ARInvoice');
  const [branchFilter, setBranchFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'FAILED' | 'RETRY'>('ALL');
  const [page, setPage] = useState(1);

  const { data: branchOptions = [] } = useQuery<BranchOptionDto[]>({
    queryKey: ['branches'],
    queryFn: () => monitorService.getBranches(),
    staleTime: 5 * 60_000,
    retry: 1,
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ['dashboard', monthOffset, interfaceType],
    queryFn: () => dashboardService.getDashboard(monthOffset, interfaceType),
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
    queryKey: ['recentLogs', interfaceType, monthOffset, branchFilter, statusFilter, page],
    queryFn: () => monitorService.getLogs({
      interfaceType,
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
  const failedBranches = data?.topFailedBranches ?? [];
  const topBranches = data?.topBranches ?? [];
  const showBranchSidebar = isLoading || failedBranches.length > 0 || topBranches.length > 0;
  const recentTotalPages = recentLogsPage?.totalPages ?? 0;

  return (
    <div className="space-y-6">
      <PageHeader
        icon={LayoutDashboard}
        title={t('dashboard')}
        subtitle={t('dashboardSubtitle', { month: monthLabel })}
        actions={(
          <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center">
            <AppSelect
              value={interfaceType}
              onChange={(e) => { setInterfaceType(e.target.value); setPage(1); }}
              wrapperClassName="sm:min-w-[12rem]"
            >
              <option value="ARInvoice">{t('interfaceTypeAR')}</option>
              <option value="IncomingPayment">{t('interfaceTypeAP')}</option>
              <option value="Delivery">{t('interfaceTypeDL')}</option>
            </AppSelect>
            <div className="inline-flex rounded-xl border border-border/80 bg-muted/40 p-0.5">
              <button
                type="button"
                onClick={() => { setMonthOffset(0); setPage(1); }}
                className={`min-h-9 flex-1 rounded-lg px-3 py-2 text-xs font-medium transition sm:flex-none ${monthOffset === 0 ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'}`}
              >
                {t('currentMonth')}
              </button>
              <button
                type="button"
                onClick={() => { setMonthOffset(1); setPage(1); }}
                className={`min-h-9 flex-1 rounded-lg px-3 py-2 text-xs font-medium transition sm:flex-none ${monthOffset === 1 ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground'}`}
              >
                {t('lastMonth')}
              </button>
            </div>
          </div>
        )}
      />
      
      {isError && (
        <div className="rounded-lg border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          {t('loadError')}
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5 md:gap-4">
        <StatCard title={t('pending')} value={counts?.pending ?? 0} icon={Clock} variant="warning" loading={isLoading} />
        <StatCard title={t('processing')} value={counts?.processing ?? 0} icon={Activity} variant="info" loading={isLoading} />
        <StatCard title={t('success')} value={counts?.success ?? 0} icon={CheckCircle} variant="success" loading={isLoading} />
        <StatCard title={t('failed')} value={counts?.failed ?? 0} icon={XCircle} variant="danger" loading={isLoading} />
        <StatCard title={t('retry')} value={counts?.retry ?? 0} icon={RefreshCw} variant="orange" loading={isLoading} />
      </div>

      <div className={cn('grid grid-cols-1 gap-6', showBranchSidebar && 'xl:grid-cols-4')}>
        <RecentLogsCard
          logs={recentLogsPage?.items ?? []}
          isLoading={recentLogsLoading}
          page={page}
          totalPages={recentTotalPages}
          branchFilter={branchFilter}
          branchOptions={branchOptions}
          statusFilter={statusFilter}
          onBranchChange={(value) => { setBranchFilter(value); setPage(1); }}
          onStatusChange={(value) => { setStatusFilter(value); setPage(1); }}
          onPageChange={setPage}
          onResendSuccess={() => refetchRecentLogs()}
          t={t}
        />
        {showBranchSidebar && (
          <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-1">
            <TopBranchesCard title={t('topFailedBranches')} branches={failedBranches} isLoading={isLoading} t={t} />
            <TopBranchesCard title={t('topBranches')} branches={topBranches} isLoading={isLoading} t={t} />
          </div>
        )}
      </div>
    </div>
  );
}
