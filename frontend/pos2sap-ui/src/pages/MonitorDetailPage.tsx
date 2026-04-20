// src/pages/MonitorDetailPage.tsx
import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, RefreshCw, CheckCircle, Clock, Activity, XCircle } from 'lucide-react';
import { toast } from 'sonner';
import monitorService from '@/services/monitorService';
import interfaceService from '@/services/interfaceService';
import { StatusBadge } from '@/components/StatusBadge';
import { JsonViewer } from '@/components/JsonViewer';
import { useLanguage } from '@/contexts/LanguageContext';
import { fmtDate, fmtDatetime, fmt, cn } from '@/lib/utils';

type Tab = 'request' | 'response' | 'source';

const statusOrder = ['PENDING', 'PROCESSING', 'SUCCESS', 'FAILED', 'RETRY'] as const;
const statusIcon: Record<string, React.ReactNode> = {
  PENDING:    <Clock className="h-4 w-4" />,
  PROCESSING: <Activity className="h-4 w-4" />,
  SUCCESS:    <CheckCircle className="h-4 w-4" />,
  FAILED:     <XCircle className="h-4 w-4" />,
  RETRY:      <RefreshCw className="h-4 w-4" />,
};

export default function MonitorDetailPage() {
  const { t } = useLanguage();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('request');
  const [retrying, setRetrying] = useState(false);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['monitor-detail', id],
    queryFn: () => monitorService.getDetail(id!),
    enabled: !!id,
    staleTime: 10_000,
  });

  async function handleRetry() {
    if (!id) return;
    setRetrying(true);
    try {
      await interfaceService.retryRecord(id);
      toast.success(t('retrySuccess'));
      refetch();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('retryError'));
    } finally {
      setRetrying(false);
    }
  }

  if (isLoading) return <div className="p-8 text-sm text-muted-foreground">{t('loading')}</div>;
  if (isError || !data) return <div className="p-8 text-sm text-destructive">{t('noData')}</div>;

  const canRetry = data.status === 'FAILED' || data.status === 'RETRY';
  const reachedIndex = statusOrder.indexOf(data.status as typeof statusOrder[number]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <button onClick={() => navigate('/monitor')}
          className="flex items-center gap-1.5 rounded-lg border px-3 py-2 text-sm hover:bg-muted">
          <ArrowLeft className="h-4 w-4" /> {t('detailBack')}
        </button>
        <div className="flex-1">
          <h1 className="text-xl font-bold">{t('detailTitle')}: {data.posDocNo}</h1>
          <p className="text-sm text-muted-foreground">{t('detailId')}: {data.id}</p>
        </div>
        {canRetry && (
          <button
            onClick={handleRetry}
            disabled={retrying}
            className="flex items-center gap-2 rounded-lg bg-orange-500 px-4 py-2 text-sm font-medium text-white hover:bg-orange-600 disabled:opacity-50"
          >
            <RefreshCw className="h-4 w-4" />
            {retrying ? t('retrying') : t('retry')}
          </button>
        )}
      </div>

      {/* Info cards */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {[
          { label: t('posDocNo'), value: data.posDocNo },
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
            { key: 'request', label: 'SAP Request' },
            { key: 'response', label: 'SAP Response' },
            { key: 'source', label: 'POS Data' },
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
