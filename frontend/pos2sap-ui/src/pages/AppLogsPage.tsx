// src/pages/AppLogsPage.tsx
import { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw, Search, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import appLogsService from '@/services/appLogsService';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { useLanguage } from '@/contexts/LanguageContext';
import { cn, fmtDatetime } from '@/lib/utils';

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / (1024 * 1024)).toFixed(1)} MB`;
}

export default function AppLogsPage() {
  const { t } = useLanguage();
  const queryClient = useQueryClient();
  const [selected, setSelected] = useState('');
  const [search, setSearch] = useState('');
  const [committedSearch, setCommittedSearch] = useState('');
  const [lines, setLines] = useState(500);
  const [clearMode, setClearMode] = useState<'one' | 'all' | null>(null);
  const [clearing, setClearing] = useState(false);

  const filesQuery = useQuery({
    queryKey: ['app-logs'],
    queryFn: () => appLogsService.list(),
    staleTime: 30_000,
  });

  useEffect(() => {
    if (!selected && filesQuery.data?.length) {
      setSelected(filesQuery.data[0].fileName);
    }
  }, [filesQuery.data, selected]);

  const contentQuery = useQuery({
    queryKey: ['app-logs', selected, lines, committedSearch],
    queryFn: () => appLogsService.getTail(selected, lines, committedSearch || undefined),
    enabled: !!selected,
    staleTime: 10_000,
  });

  const files = filesQuery.data ?? [];
  const content = contentQuery.data;

  const meta = useMemo(() => {
    if (!content) return '';
    return t('appLogsMeta', {
      shown: content.linesReturned,
      total: content.totalLines,
    });
  }, [content, t]);

  async function handleRefresh() {
    try {
      await Promise.all([filesQuery.refetch(), contentQuery.refetch()]);
      toast.success(t('appLogsRefreshed'));
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('appLogsLoadError'));
    }
  }

  async function handleConfirmClear() {
    if (!clearMode) return;
    setClearing(true);
    try {
      if (clearMode === 'one') {
        if (!selected) return;
        await appLogsService.clearOne(selected);
        toast.success(t('appLogsClearSuccess'));
      } else {
        const res = await appLogsService.clearAll();
        toast.success(t('appLogsClearAllSuccess', { count: res.cleared }));
        setSelected('');
      }
      await queryClient.invalidateQueries({ queryKey: ['app-logs'] });
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : t('appLogsClearError'));
    } finally {
      setClearing(false);
      setClearMode(null);
    }
  }

  return (
    <div className="space-y-4">
      <ConfirmDialog
        isOpen={clearMode !== null}
        title={clearMode === 'all' ? t('appLogsClearAllTitle') : t('appLogsClearTitle')}
        message={
          clearMode === 'all'
            ? t('appLogsClearAllMessage')
            : t('appLogsClearMessage', { file: selected })
        }
        confirmText={clearMode === 'all' ? t('appLogsClearAll') : t('appLogsClear')}
        cancelText={t('cancel')}
        isDangerous
        isLoading={clearing}
        onConfirm={() => void handleConfirmClear()}
        onCancel={() => setClearMode(null)}
      />

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t('appLogs')}</h2>
          <p className="text-sm text-muted-foreground">{t('appLogsSubtitle')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setClearMode('one')}
            disabled={!selected || clearing}
            className="inline-flex items-center gap-2 rounded-lg border border-destructive/30 px-3 py-2 text-sm font-medium text-destructive hover:bg-destructive/10 disabled:opacity-50"
          >
            <Trash2 className="h-4 w-4" />
            {t('appLogsClear')}
          </button>
          <button
            type="button"
            onClick={() => setClearMode('all')}
            disabled={files.length === 0 || clearing}
            className="inline-flex items-center gap-2 rounded-lg border border-destructive/30 px-3 py-2 text-sm font-medium text-destructive hover:bg-destructive/10 disabled:opacity-50"
          >
            <Trash2 className="h-4 w-4" />
            {t('appLogsClearAll')}
          </button>
          <button
            type="button"
            onClick={() => void handleRefresh()}
            className="inline-flex items-center gap-2 rounded-lg border px-3 py-2 text-sm font-medium hover:bg-muted"
          >
            <RefreshCw className={cn('h-4 w-4', (filesQuery.isFetching || contentQuery.isFetching) && 'animate-spin')} />
            {t('refresh')}
          </button>
        </div>
      </div>

      <div className="flex flex-wrap gap-2 rounded-xl border bg-card p-3">
        <select
          value={selected}
          onChange={(e) => setSelected(e.target.value)}
          className="min-w-[220px] rounded-md border bg-background px-3 py-2 text-sm"
        >
          {files.length === 0 && <option value="">{t('appLogsEmpty')}</option>}
          {files.map((f) => (
            <option key={f.fileName} value={f.fileName}>
              {f.fileName} ({formatBytes(f.sizeBytes)})
            </option>
          ))}
        </select>

        <select
          value={lines}
          onChange={(e) => setLines(Number(e.target.value))}
          className="rounded-md border bg-background px-3 py-2 text-sm"
        >
          {[200, 500, 1000, 2000, 5000].map((n) => (
            <option key={n} value={n}>{t('appLogsLastLines', { n })}</option>
          ))}
        </select>

        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') setCommittedSearch(search.trim());
            }}
            placeholder={t('appLogsSearchPlaceholder')}
            className="w-full rounded-md border bg-background py-2 pl-9 pr-3 text-sm"
          />
        </div>
        <button
          type="button"
          onClick={() => setCommittedSearch(search.trim())}
          className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          {t('searchButton')}
        </button>
      </div>

      {meta && <p className="text-xs text-muted-foreground">{meta}</p>}

      <div className="overflow-hidden rounded-xl border bg-card">
        {contentQuery.isLoading ? (
          <div className="p-6 text-sm text-muted-foreground">{t('loading')}</div>
        ) : contentQuery.isError ? (
          <div className="p-6 text-sm text-destructive">
            {contentQuery.error instanceof Error ? contentQuery.error.message : t('appLogsLoadError')}
          </div>
        ) : !content?.content ? (
          <div className="p-6 text-sm text-muted-foreground">{t('noData')}</div>
        ) : (
          <pre className="max-h-[70vh] overflow-auto bg-zinc-950 p-4 text-xs leading-5 text-zinc-100 whitespace-pre-wrap break-all">
            {content.content}
          </pre>
        )}
      </div>

      {selected && files.find((f) => f.fileName === selected) && (
        <p className="text-xs text-muted-foreground">
          {t('appLogsUpdated', {
            when: fmtDatetime(files.find((f) => f.fileName === selected)!.lastWriteUtc),
          })}
        </p>
      )}
    </div>
  );
}
