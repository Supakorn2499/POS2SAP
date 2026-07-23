// src/pages/MonitorDetailPage.tsx
import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, RefreshCw, CheckCircle2, Clock, Activity, XCircle, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import monitorService from '@/services/monitorService';
import { StatusBadge } from '@/components/StatusBadge';
import { JsonViewer } from '@/components/JsonViewer';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { AppIcon } from '@/components/ui/AppIcon';
import { useLanguage } from '@/contexts/LanguageContext';
import { fmtDate, fmtDatetime, fmt, cn } from '@/lib/utils';
import { interfaceTypeLabel, resendInterfaceLog } from '@/lib/interfaceResend';

type Tab = 'request' | 'response' | 'source';

const statusOrder = ['PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'RETRY'] as const;
const statusIcon: Record<string, React.ReactNode> = {
  PENDING:    <AppIcon icon={Clock} className="h-4 w-4" />,
  PROCESSING: <AppIcon icon={Activity} className="h-4 w-4" />,
  SUCCESS:    <AppIcon icon={CheckCircle2} className="h-4 w-4" />,
  FAILED:     <AppIcon icon={XCircle} className="h-4 w-4" />,
  RETRY:      <AppIcon icon={RefreshCw} className="h-4 w-4" />,
};

export default function MonitorDetailPage() {
  const { t } = useLanguage();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>('request');
  const [retrying, setRetrying] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['monitor-detail', id],
    queryFn: () => monitorService.getDetail(id!),
    enabled: !!id,
    staleTime: 10_000,
  });

  async function handleRetry() {
    if (!id || !data) return;
    setRetrying(true);
    try {
      const ok = await resendInterfaceLog(id, data.posDocNo, data.interfaceType);
      if (ok) {
        toast.success(t('retrySuccess'));
      } else {
        toast.error(t('retryError'));
      }
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('retryError'));
    } finally {
      setRetrying(false);
    }
  }

  async function handleDelete() {
    if (!id) return;
    setDeleting(true);
    try {
      await monitorService.deleteLog(id);
      // List page caches for 30s — drop it so back-nav doesn't show the deleted row
      await queryClient.invalidateQueries({ queryKey: ['monitor-logs'] });
      await queryClient.removeQueries({ queryKey: ['monitor-detail', id] });
      toast.success(t('deleteLogSuccess'));
      navigate('/monitor');
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('deleteLogError'));
    } finally {
      setDeleting(false);
      setDeleteOpen(false);
    }
  }

  if (isLoading) return <div className="p-8 text-sm text-muted-foreground">{t('loading')}</div>;
  if (isError || !data) return <div className="p-8 text-sm text-destructive">{t('noData')}</div>;

  const canRetry = data.status === 'FAILED' || data.status === 'RETRY';
  const canDelete = ['PENDING', 'FAILED', 'RETRY'].includes(data.status);
  const reachedIndex = statusOrder.indexOf(data.status as typeof statusOrder[number]);

  return (
    <div className="space-y-6">
      <ConfirmDialog
        isOpen={deleteOpen}
        title={t('deleteLogConfirmTitle')}
        message={t('deleteLogConfirmMsg')}
        confirmText={deleting ? t('deleting') : t('deleteLog')}
        cancelText={t('clearButton')}
        isLoading={deleting}
        onConfirm={handleDelete}
        onCancel={() => setDeleteOpen(false)}
      />
      {/* Header */}
      <div className="flex flex-wrap items-center gap-3">
        <button onClick={() => navigate('/monitor')}
          className="inline-flex items-center gap-1.5 rounded-xl border border-input bg-background px-3 py-2 text-sm font-medium shadow-sm transition hover:bg-muted">
          <AppIcon icon={ArrowLeft} className="h-4 w-4" /> {t('detailBack')}
        </button>
        <div className="min-w-0 flex-1">
          <h1 className="text-xl font-semibold tracking-tight">{t('detailTitle')}: {data.posDocNo}</h1>
          <p className="text-sm text-muted-foreground">{t('detailId')}: {data.id}</p>
        </div>
        {canDelete && (
          <button
            onClick={() => setDeleteOpen(true)}
            disabled={deleting}
            className="inline-flex items-center gap-2 rounded-xl border border-destructive/30 px-4 py-2 text-sm font-medium text-destructive transition hover:bg-destructive hover:text-destructive-foreground disabled:opacity-50"
          >
            <AppIcon icon={Trash2} className="h-4 w-4" />
            {t('deleteLog')}
          </button>
        )}
        {canRetry && (
          <button
            onClick={handleRetry}
            disabled={retrying}
            className="inline-flex items-center gap-2 rounded-xl bg-orange-500 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-orange-600 disabled:opacity-50"
          >
            <AppIcon icon={RefreshCw} className="h-4 w-4" />
            {retrying ? t('retrying') : t('retry')}
          </button>
        )}
      </div>

      {/* Info cards */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {[
          { label: t('posDocNo'), value: data.posDocNo },
          { label: t('interfaceType'), value: interfaceTypeLabel(data.interfaceType, t) },
          { label: t('billDate'), value: fmtDate(data.posDocDate) },
          { label: t('branchLabel'), value: `${data.branchCode ?? '-'} ${data.branchName ?? ''}` },
          { label: t('channel'), value: data.channel ?? '-' },
          { label: t('sapDocNum'), value: data.sapDocNum ?? '-' },
          { label: t('totalAmount'), value: fmt(data.docTotal) },
          { label: t('retryCount'), value: String(data.retryCount) },
          { label: t('sentTime'), value: fmtDatetime(data.sentAt) },
        ].map(({ label, value }) => (
          <div key={label} className="rounded-lg border bg-card p-3">
            <p className="text-xs text-muted-foreground">{label}</p>
            <p className="mt-0.5 text-sm font-medium truncate">{value}</p>
          </div>
        ))}
      </div>

      {/* Status + badge */}
      <div className="rounded-xl border bg-card p-4 shadow-sm">
        <div className="flex items-center gap-3 mb-4">
          <StatusBadge status={data.status} />
          {data.errorMessage && (
            <p className="text-sm text-destructive">{data.errorMessage}</p>
          )}
        </div>
        {/* Status timeline */}
        <div className="flex items-center gap-0">
          {['PENDING', 'PROCESSING', 'SUCCESS / FAILED'].map((s, i) => {
            const reached = i <= Math.min(reachedIndex, 2);
            return (
              <div key={s} className="flex items-center flex-1 last:flex-none">
                <div className={cn(
                  'flex h-7 w-7 items-center justify-center rounded-full text-xs font-medium shrink-0',
                  reached ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'
                )}>
                  {statusIcon[s.split(' ')[0]] ?? i + 1}
                </div>
                <p className={cn('ml-2 text-xs', reached ? 'font-medium' : 'text-muted-foreground')}>
                  {s.includes('SUCCESS') ? t('statusLabel.SUCCESS') : t(`statusLabel.${s.split(' ')[0]}`)}
                </p>
                {i < 2 && <div className={cn('mx-2 h-px flex-1', reached ? 'bg-primary' : 'bg-muted')} />}
              </div>
            );
          })}
        </div>
      </div>

      {/* JSON Tabs */}
      <div className="rounded-xl border bg-card shadow-sm">
        <div className="flex border-b">
          {([
            { key: 'request' as const, label: t('sapRequest') },
            { key: 'response' as const, label: t('sapResponse') },
            { key: 'source' as const, label: t('posData') },
          ] as { key: Tab; label: string }[]).map(({ key, label }) => (
            <button
              key={key}
              onClick={() => setTab(key)}
              className={cn(
                'px-5 py-3 text-sm font-medium border-b-2 transition-colors',
                tab === key
                  ? 'border-primary text-primary'
                  : 'border-transparent text-muted-foreground hover:text-foreground'
              )}
            >
              {label}
            </button>
          ))}
        </div>
        <div className="p-4">
          {tab === 'request' && <JsonViewer value={data.sapRequest} title={t('sapRequest')} />}
          {tab === 'response' && <JsonViewer value={data.sapResponse} title={t('sapResponse')} />}
          {tab === 'source' && <JsonViewer value={data.posData} title={t('posData')} />}
        </div>
      </div>
    </div>
  );
}
