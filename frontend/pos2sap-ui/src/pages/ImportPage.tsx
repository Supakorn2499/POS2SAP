// src/pages/ImportPage.tsx
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import {
  Search, Upload, ArrowRight, CheckCircle2,
  CheckSquare2, Square, MinusSquare, Loader2,
} from 'lucide-react';
import { toast } from 'sonner';
import interfaceService from '@/services/interfaceService';
import monitorService from '@/services/monitorService';
import { useLanguage } from '@/contexts/LanguageContext';
import { fmtDate, fmt, todayStr, cn } from '@/lib/utils';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import type { ImportPreviewItem } from '@/types/import';

const INTERFACE_OPTIONS = [
  { value: 'ARInvoice',       label: 'AR Invoice' },
  { value: 'IncomingPayment', label: 'Incoming Payment' },
  { value: 'Delivery',        label: 'Delivery' },
];

const PAGE_SIZE = 50;

export default function ImportPage() {
  const { t } = useLanguage();
  const navigate = useNavigate();

  // ─── Filter state ──────────────────────────────────────────────
  const [dateFrom,      setDateFrom]      = useState(todayStr());
  const [dateTo,        setDateTo]        = useState(todayStr());
  const [branchCode,    setBranchCode]    = useState('');
  const [interfaceType, setInterfaceType] = useState('ARInvoice');

  // ─── Preview state ─────────────────────────────────────────────
  const [sortedItems,  setSortedItems]  = useState<ImportPreviewItem[] | null>(null);
  const [selected,     setSelected]     = useState<Set<string>>(new Set());
  const [confirmOpen,  setConfirmOpen]  = useState(false);
  const [page,         setPage]         = useState(1);

  // ─── Result state ──────────────────────────────────────────────
  const [importResult, setImportResult] = useState<{ fetched: number; imported: number } | null>(null);

  // ─── Branch options (from interface_logs) ──────────────────────
  const { data: branchOptions = [] } = useQuery({
    queryKey: ['monitor-branches'],
    queryFn: () => monitorService.getBranches(),
    staleTime: 5 * 60_000,
  });

  // ─── Preview mutation ──────────────────────────────────────────
  const previewMutation = useMutation({
    mutationFn: () => interfaceService.previewImport({ dateFrom, dateTo, branchCode: branchCode || undefined, interfaceType }),
    onSuccess: (items) => {
      // Sort: new items first, already-imported last
      const sorted = [
        ...items.filter(i => !i.alreadyImported),
        ...items.filter(i =>  i.alreadyImported),
      ];
      setSortedItems(sorted);
      setImportResult(null);
      setPage(1);
      const newDocNos = items.filter(i => !i.alreadyImported).map(i => i.docNum);
      setSelected(new Set(newDocNos));
      if (items.length === 0) toast.info(t('importEmptyResult'));
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('importError'));
    },
  });

  // ─── Import mutation ───────────────────────────────────────────
  const importMutation = useMutation({
    mutationFn: () => interfaceService.importPreview([...selected], interfaceType, branchCode || undefined),
    onSuccess: (result) => {
      setImportResult(result);
      setSortedItems(null);
      toast.success(t('importDone'));
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('importError'));
    },
    onSettled: () => setConfirmOpen(false),
  });

  const isLoading = previewMutation.isPending || importMutation.isPending;

  // ─── Derived data ──────────────────────────────────────────────
  const newItems   = sortedItems?.filter(i => !i.alreadyImported) ?? [];
  const dupItems   = sortedItems?.filter(i =>  i.alreadyImported) ?? [];
  const totalPages = sortedItems ? Math.max(1, Math.ceil(sortedItems.length / PAGE_SIZE)) : 1;
  const pageItems  = sortedItems?.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE) ?? [];
  const pageNewItems = pageItems.filter(i => !i.alreadyImported);

  // Header checkbox state for current page
  const pageNewSelected    = pageNewItems.filter(i => selected.has(i.docNum));
  const allPageNewChecked  = pageNewItems.length > 0 && pageNewSelected.length === pageNewItems.length;
  const somePageChecked    = pageNewSelected.length > 0 && !allPageNewChecked;

  // ─── Helpers ───────────────────────────────────────────────────
  function handleToggle(docNum: string) {
    setSelected(prev => {
      const next = new Set(prev);
      next.has(docNum) ? next.delete(docNum) : next.add(docNum);
      return next;
    });
  }

  function handleSelectAllNew() {
    setSelected(new Set(newItems.map(i => i.docNum)));
  }

  function handleDeselectAll() {
    setSelected(new Set());
  }

  function handleTogglePageHeader() {
    if (allPageNewChecked) {
      setSelected(prev => {
        const next = new Set(prev);
        pageNewItems.forEach(i => next.delete(i.docNum));
        return next;
      });
    } else {
      setSelected(prev => {
        const next = new Set(prev);
        pageNewItems.forEach(i => next.add(i.docNum));
        return next;
      });
    }
  }

  function handleReset() {
    setSortedItems(null);
    setImportResult(null);
    setSelected(new Set());
    setPage(1);
  }

  // ─── Loading overlay ────────────────────────────────────────────
  const LoadingOverlay = ({ message }: { message: string }) => (
    <div className="fixed inset-0 z-50 flex flex-col items-center justify-center bg-black/40 backdrop-blur-sm">
      <div className="rounded-2xl bg-white shadow-2xl px-10 py-8 flex flex-col items-center gap-4 max-w-xs w-full mx-4">
        <Loader2 className="h-10 w-10 text-primary animate-spin" />
        <p className="text-sm font-medium text-center text-foreground">{message}</p>
      </div>
    </div>
  );

  // ─── Render ────────────────────────────────────────────────────
  return (
    <div className="space-y-6">
      {/* Loading overlay — blocks all navigation/interaction */}
      {previewMutation.isPending && <LoadingOverlay message={t('importLoadingOverlay')} />}
      {importMutation.isPending  && <LoadingOverlay message={t('importImportingOverlay')} />}

      {/* Confirm dialog */}
      <ConfirmDialog
        isOpen={confirmOpen}
        title={t('importConfirmTitle')}
        message={t('importConfirmMsg', { count: selected.size })}
        confirmText={t('importConfirmBtn', { count: selected.size })}
        cancelText={t('clearButton')}
        isLoading={importMutation.isPending}
        onConfirm={() => importMutation.mutate()}
        onCancel={() => setConfirmOpen(false)}
      />

      {/* Header */}
      <div>
        <h1 className="text-xl font-bold">{t('importPageTitle')}</h1>
        <p className="text-sm text-muted-foreground">{t('importPageSubtitle')}</p>
      </div>

      {/* ── STEP 1: Filter ── */}
      <div className="rounded-xl border bg-card p-5 shadow-sm space-y-4">
        <h2 className="font-semibold text-sm">{t('importStepFilter')}</h2>
        <div className="flex flex-wrap gap-3 items-end">
          {/* Interface Type */}
          <div className="flex flex-col gap-1">
            <label className="text-xs text-muted-foreground">{t('importDocType')}</label>
            <select
              value={interfaceType}
              onChange={e => setInterfaceType(e.target.value)}
              disabled={isLoading}
              className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
            >
              {INTERFACE_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </div>

          {/* Date From */}
          <div className="flex flex-col gap-1">
            <label className="text-xs text-muted-foreground">{t('importDateFrom')}</label>
            <input
              type="date"
              value={dateFrom}
              onChange={e => setDateFrom(e.target.value)}
              disabled={isLoading}
              className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
            />
          </div>

          {/* Date To */}
          <div className="flex flex-col gap-1">
            <label className="text-xs text-muted-foreground">{t('importDateTo')}</label>
            <input
              type="date"
              value={dateTo}
              min={dateFrom}
              onChange={e => setDateTo(e.target.value)}
              disabled={isLoading}
              className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
            />
          </div>

          {/* Branch */}
          <div className="flex flex-col gap-1">
            <label className="text-xs text-muted-foreground">{t('branchLabel')}</label>
            <select
              value={branchCode}
              onChange={e => setBranchCode(e.target.value)}
              disabled={isLoading}
              className="rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring min-w-40 disabled:opacity-50"
            >
              <option value="">{t('allBranches')}</option>
              {branchOptions.map(b => (
                <option key={b.branchCode} value={b.branchCode}>
                  {b.branchName || b.branchCode}
                </option>
              ))}
            </select>
          </div>

          {/* Preview Button */}
          <button
            onClick={() => previewMutation.mutate()}
            disabled={isLoading || !dateFrom || !dateTo}
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50 self-end"
          >
            {previewMutation.isPending
              ? <Loader2 className="h-4 w-4 animate-spin" />
              : <Search className="h-4 w-4" />}
            {previewMutation.isPending ? t('importPreviewing') : t('importPreviewBtn')}
          </button>
        </div>
      </div>

      {/* ── STEP 2: Preview Table ── */}
      {sortedItems !== null && sortedItems.length > 0 && (
        <div className="rounded-xl border bg-card shadow-sm">
          {/* Summary + action bar */}
          <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 border-b">
            <div className="flex items-center gap-3">
              <h2 className="font-semibold text-sm">{t('importStepPreview')}</h2>
              <span className="inline-flex items-center rounded-full bg-green-100 text-green-700 text-xs px-2.5 py-0.5 font-medium">
                {t('importNewCount', { count: newItems.length })}
              </span>
              {dupItems.length > 0 && (
                <span className="inline-flex items-center rounded-full bg-muted text-muted-foreground text-xs px-2.5 py-0.5 font-medium">
                  {t('importDupCount', { count: dupItems.length })}
                </span>
              )}
            </div>

            {/* Select / Deselect buttons */}
            <div className="flex items-center gap-2">
              <button
                onClick={handleSelectAllNew}
                disabled={isLoading || newItems.length === 0}
                className="inline-flex items-center gap-1.5 rounded-lg border border-primary bg-primary/5 px-3 py-1.5 text-xs font-medium text-primary hover:bg-primary/10 disabled:opacity-40 transition-colors"
              >
                <CheckSquare2 className="h-3.5 w-3.5" />
                {t('importSelectAll')}
              </button>
              <button
                onClick={handleDeselectAll}
                disabled={isLoading || selected.size === 0}
                className="inline-flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-xs font-medium text-muted-foreground hover:bg-muted hover:text-foreground disabled:opacity-40 transition-colors"
              >
                <MinusSquare className="h-3.5 w-3.5" />
                {t('importDeselectAll')}
              </button>
            </div>
          </div>

          {/* Table */}
          <div className="overflow-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50 text-left text-xs text-muted-foreground">
                  {/* Page-level header checkbox */}
                  <th className="px-4 py-2.5 w-10">
                    <button
                      onClick={handleTogglePageHeader}
                      disabled={isLoading || pageNewItems.length === 0}
                      className="flex items-center justify-center disabled:opacity-30"
                    >
                      {somePageChecked
                        ? <MinusSquare className="h-4 w-4 text-primary" />
                        : allPageNewChecked
                          ? <CheckSquare2 className="h-4 w-4 text-primary" />
                          : <Square className="h-4 w-4" />}
                    </button>
                  </th>
                  <th className="px-4 py-2.5">{t('posDocNo')}</th>
                  <th className="px-4 py-2.5">{t('billDate')}</th>
                  <th className="px-4 py-2.5">{t('branchLabel')}</th>
                  <th className="px-4 py-2.5">{t('channel')}</th>
                  <th className="px-4 py-2.5 text-right">{t('totalAmount')}</th>
                  <th className="px-4 py-2.5">{t('importStatusCol')}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {pageItems.map(item => {
                  const isChecked = selected.has(item.docNum);
                  return (
                    <tr
                      key={item.docNum}
                      onClick={() => !item.alreadyImported && !isLoading && handleToggle(item.docNum)}
                      className={cn(
                        'transition-colors',
                        item.alreadyImported
                          ? 'opacity-40 cursor-default'
                          : isLoading
                            ? 'cursor-not-allowed'
                            : 'hover:bg-muted/30 cursor-pointer',
                        isChecked && !item.alreadyImported && 'bg-primary/5',
                      )}
                    >
                      <td className="px-4 py-2">
                        {item.alreadyImported ? (
                          <Square className="h-4 w-4 text-muted-foreground/30" />
                        ) : isChecked ? (
                          <CheckSquare2 className="h-4 w-4 text-primary" />
                        ) : (
                          <Square className="h-4 w-4 text-muted-foreground" />
                        )}
                      </td>
                      <td className="px-4 py-2 font-mono text-xs">{item.docNum}</td>
                      <td className="px-4 py-2 text-xs">{fmtDate(item.docDate)}</td>
                      <td className="px-4 py-2 text-xs">{item.branchName || item.branchCode || '-'}</td>
                      <td className="px-4 py-2 text-xs">{item.channel || '-'}</td>
                      <td className="px-4 py-2 text-right font-mono text-xs">{fmt(item.docTotal)}</td>
                      <td className="px-4 py-2">
                        {item.alreadyImported ? (
                          <span className="inline-flex items-center rounded-full bg-muted text-muted-foreground text-xs px-2 py-0.5 font-medium">
                            {t('importDupBadge')}
                          </span>
                        ) : (
                          <span className="inline-flex items-center rounded-full bg-green-100 text-green-700 text-xs px-2 py-0.5 font-medium">
                            {t('importNewBadge')}
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Pagination + footer action */}
          <div className="flex flex-wrap items-center justify-between gap-3 border-t px-5 py-3">
            {/* Left: selected count + pagination */}
            <div className="flex items-center gap-4">
              <span className="text-sm text-muted-foreground">
                {t('importSelectedCount', { count: selected.size })}
              </span>
              {totalPages > 1 && (
                <div className="flex items-center gap-1.5">
                  <button
                    onClick={() => setPage(p => Math.max(1, p - 1))}
                    disabled={page <= 1 || isLoading}
                    className="rounded border px-2.5 py-1 text-xs hover:bg-muted disabled:opacity-40"
                  >
                    {t('previous')}
                  </button>
                  <span className="text-xs text-muted-foreground px-1">
                    {page} / {totalPages}
                  </span>
                  <button
                    onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages || isLoading}
                    className="rounded border px-2.5 py-1 text-xs hover:bg-muted disabled:opacity-40"
                  >
                    {t('next')}
                  </button>
                </div>
              )}
            </div>

            {/* Right: Import button */}
            <button
              onClick={() => setConfirmOpen(true)}
              disabled={selected.size === 0 || isLoading}
              className="flex items-center gap-2 rounded-lg bg-green-600 px-5 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50 transition-colors"
            >
              <Upload className="h-4 w-4" />
              {t('importConfirmBtn', { count: selected.size })}
            </button>
          </div>
        </div>
      )}

      {/* Preview returned empty */}
      {sortedItems !== null && sortedItems.length === 0 && (
        <div className="rounded-xl border bg-card p-10 text-center text-sm text-muted-foreground shadow-sm">
          {t('importEmptyResult')}
        </div>
      )}

      {/* ── STEP 3: Result ── */}
      {importResult !== null && (
        <div className="rounded-xl border bg-card p-8 shadow-sm text-center space-y-4">
          <CheckCircle2 className="h-12 w-12 text-green-500 mx-auto" />
          <h2 className="text-lg font-bold">{t('importDone')}</h2>
          <p className="text-sm text-muted-foreground">
            {t('importDoneDetail', { imported: importResult.imported })}
          </p>
          <div className="flex justify-center gap-3 pt-2">
            <button
              onClick={handleReset}
              className="rounded-lg border px-4 py-2 text-sm hover:bg-muted"
            >
              {t('importAgain')}
            </button>
            <button
              onClick={() => navigate('/monitor')}
              className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
            >
              {t('goToMonitor')} <ArrowRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
