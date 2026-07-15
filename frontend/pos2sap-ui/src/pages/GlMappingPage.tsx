// src/pages/GlMappingPage.tsx
import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Map, Trash2, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import glMappingService from '@/services/glMappingService';
import {
  MappingActionButton,
  MappingPageHeader,
  MappingSection,
  MappingStatGrid,
  MappingToolbar,
  MappingUnsavedBar,
  MappingPagination,
  mappingPaginationLabels,
  mappingInputClass,
  mappingTableClass,
  mappingTableHeadClass,
} from '@/components/mapping/MappingPageLayout';
import { MappingExcelActions } from '@/components/mapping/MappingExcelActions';
import {
  MappingConfirmDialog,
  type MappingConfirmState,
} from '@/components/mapping/MappingConfirmDialog';
import { useLanguage } from '@/contexts/LanguageContext';
import type { PaytypeGlMappingDto, SapPayCategory, UpsertGlMappingDto } from '@/types/glMapping';
import { cn, fmtDatetime, todayStr } from '@/lib/utils';
import { downloadCsv } from '@/lib/downloadCsv';
import { csvBool, csvCell, csvInt, csvNullable, parseCsv } from '@/lib/parseCsv';
import { useMappingPagination } from '@/hooks/useMappingPagination';

const CATEGORIES: SapPayCategory[] = ['CASH', 'TRANSFER', 'CREDIT_CARD', 'SKIP'];

const CATEGORY_BADGE: Record<SapPayCategory, string> = {
  CASH:        'bg-green-700 text-white',
  TRANSFER:    'bg-blue-700 text-white',
  CREDIT_CARD: 'bg-amber-700 text-white',
  SKIP:        'bg-slate-600 text-white',
};

const CATEGORY_DOT: Record<SapPayCategory, string> = {
  CASH:        'bg-green-600',
  TRANSFER:    'bg-blue-600',
  CREDIT_CARD: 'bg-amber-600',
  SKIP:        'bg-slate-500',
};

const CATEGORY_BORDER: Record<SapPayCategory, string> = {
  CASH:        'border-l-green-600',
  TRANSFER:    'border-l-blue-600',
  CREDIT_CARD: 'border-l-amber-600',
  SKIP:        'border-l-slate-500',
};

type CategoryFilter = '' | SapPayCategory | 'PENDING_GL';
type RowEdit = Omit<UpsertGlMappingDto, never>;

type PendingAdd = { payTypeId: number; payTypeName: string };

function isPendingGl(edit: UpsertGlMappingDto): boolean {
  if (edit.sapPayCategory === 'SKIP') return false;
  const gl = edit.sapGlAccount?.trim() ?? '';
  return !gl || gl === '[GL-PENDING]';
}

function snapshotRow(row: PaytypeGlMappingDto): UpsertGlMappingDto {
  return {
    payTypeID: row.payTypeID,
    payTypeName: row.payTypeName,
    sapPayCategory: row.sapPayCategory,
    sapGlAccount: row.sapGlAccount,
    sapPayTypeName: row.sapPayTypeName,
    isActive: row.isActive,
    sortOrder: row.sortOrder,
    remarks: row.remarks,
  };
}

export default function GlMappingPage() {
  const { t } = useLanguage();
  const qc = useQueryClient();

  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<CategoryFilter>('');
  const [edits, setEdits] = useState<Record<number, Partial<RowEdit>>>({});
  const [confirm, setConfirm] = useState<MappingConfirmState>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [pendingAdd, setPendingAdd] = useState<PendingAdd | null>(null);

  const { data: rows = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['glmapping'],
    queryFn: () => glMappingService.getAll(),
    staleTime: 30_000,
  });

  const { data: unmapped = [] } = useQuery({
    queryKey: ['glmapping-unmapped'],
    queryFn: () => glMappingService.getUnmapped(),
    staleTime: 30_000,
  });

  const saveAllMutation = useMutation({
    mutationFn: async (dirtyRows: PaytypeGlMappingDto[]) => {
      for (const row of dirtyRows) {
        const edit = getEdit(row);
        const err = validateEdit(edit);
        if (err) throw new Error(err);
        await glMappingService.upsert(edit);
      }
    },
    onSuccess: () => {
      toast.success(t('glMappingSaved'));
      setEdits({});
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['glmapping'] });
      qc.invalidateQueries({ queryKey: ['glmapping-unmapped'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (payTypeId: number) => glMappingService.remove(payTypeId),
    onSuccess: () => {
      toast.success(t('glMappingDeleted'));
      qc.invalidateQueries({ queryKey: ['glmapping'] });
      qc.invalidateQueries({ queryKey: ['glmapping-unmapped'] });
      clearConfirm();
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
      clearConfirm();
    },
  });

  const addMutation = useMutation({
    mutationFn: (payload: PendingAdd) =>
      glMappingService.upsert({
        payTypeID: payload.payTypeId,
        payTypeName: payload.payTypeName,
        sapPayCategory: 'SKIP',
        sapGlAccount: null,
        sapPayTypeName: null,
        isActive: true,
        sortOrder: 99,
        remarks: null,
      }),
    onSuccess: () => {
      toast.success(t('glMappingSaved'));
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['glmapping'] });
      qc.invalidateQueries({ queryKey: ['glmapping-unmapped'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
    },
  });

  function getEdit(row: PaytypeGlMappingDto): UpsertGlMappingDto {
    const e = edits[row.payTypeID] ?? {};
    return {
      payTypeID:      row.payTypeID,
      payTypeName:    e.payTypeName      ?? row.payTypeName,
      sapPayCategory: e.sapPayCategory   ?? row.sapPayCategory,
      sapGlAccount:   e.sapGlAccount     !== undefined ? e.sapGlAccount   : row.sapGlAccount,
      sapPayTypeName: e.sapPayTypeName   !== undefined ? e.sapPayTypeName : row.sapPayTypeName,
      isActive:       e.isActive         !== undefined ? e.isActive       : row.isActive,
      sortOrder:      e.sortOrder        !== undefined ? e.sortOrder      : row.sortOrder,
      remarks:        e.remarks          !== undefined ? e.remarks        : row.remarks,
    };
  }

  function isRowDirty(row: PaytypeGlMappingDto): boolean {
    return JSON.stringify(getEdit(row)) !== JSON.stringify(snapshotRow(row));
  }

  function setField<K extends keyof RowEdit>(payTypeId: number, field: K, value: RowEdit[K]) {
    setEdits((prev) => ({
      ...prev,
      [payTypeId]: { ...(prev[payTypeId] ?? {}), [field]: value },
    }));
  }

  function validateEdit(edit: UpsertGlMappingDto): string | null {
    if (edit.sapPayCategory !== 'SKIP') {
      const gl = edit.sapGlAccount?.trim() ?? '';
      if (!gl || gl === '[GL-PENDING]') return t('glMappingValidationGlAccount');
      if (edit.sapPayCategory === 'CREDIT_CARD' && !edit.sapPayTypeName?.trim())
        return t('glMappingValidationSapPayType');
    }
    return null;
  }

  const dirtyRows = useMemo(() => rows.filter(isRowDirty), [rows, edits]);
  const hasUnsaved = dirtyRows.length > 0;
  const isBusy = saveAllMutation.isPending || deleteMutation.isPending || addMutation.isPending;

  const filteredRows = useMemo(() => {
    let result = rows;

    if (categoryFilter === 'PENDING_GL') {
      result = result.filter((r) => isPendingGl(getEdit(r)));
    } else if (categoryFilter) {
      result = result.filter((r) => getEdit(r).sapPayCategory === categoryFilter);
    }

    const q = search.trim().toLowerCase();
    if (q) {
      result = result.filter((r) =>
        r.payTypeName.toLowerCase().includes(q) ||
        String(r.payTypeID).includes(q) ||
        (r.sapGlAccount ?? '').toLowerCase().includes(q)
      );
    }

    return result;
  }, [rows, search, categoryFilter, edits]);

  const filteredUnmapped = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return unmapped;
    return unmapped.filter(
      (u) =>
        u.payTypeName.toLowerCase().includes(q) ||
        String(u.payTypeID).includes(q)
    );
  }, [unmapped, search]);

  const mappedPager = useMappingPagination(filteredRows);
  const unmappedPager = useMappingPagination(filteredUnmapped);
  const paginationLabels = useMemo(() => mappingPaginationLabels(t), [t]);

  const stats = useMemo(() => {
    const active = rows.filter((r) => r.isActive).length;
    const pendingGl = rows.filter((r) => isPendingGl(snapshotRow(r))).length;
    return { total: rows.length, active, pendingGl, unmapped: unmapped.length };
  }, [rows, unmapped.length]);

  const CATEGORY_LABEL_KEY: Record<SapPayCategory, string> = {
    CASH: 'glMappingCatCash',
    TRANSFER: 'glMappingCatTransfer',
    CREDIT_CARD: 'glMappingCatCreditCard',
    SKIP: 'glMappingCatSkip',
  };

  function categoryLabel(c: SapPayCategory) {
    return t(CATEGORY_LABEL_KEY[c]);
  }

  function handleConfirm() {
    if (!confirm) return;
    if (confirm.type === 'save') {
      saveAllMutation.mutate(dirtyRows);
    } else if (confirm.type === 'discard') {
      setEdits({});
      clearConfirm();
    } else if (confirm.type === 'delete' && pendingDeleteId !== null) {
      deleteMutation.mutate(pendingDeleteId);
    } else if (confirm.type === 'add' && pendingAdd) {
      addMutation.mutate({
        payTypeId: pendingAdd.payTypeId,
        payTypeName: pendingAdd.payTypeName,
      });
    }
  }

  function clearConfirm() {
    setConfirm(null);
    setPendingDeleteId(null);
    setPendingAdd(null);
  }

  function handleExportExcel() {
    if (rows.length === 0) {
      toast.error(t('exportEmpty'));
      return;
    }
    const sorted = [...rows].sort((a, b) => a.payTypeID - b.payTypeID);
    downloadCsv(
      `gl-mapping-${todayStr()}.csv`,
      [
        'PayTypeID', 'PayTypeName', 'SapPayCategory', 'SapGlAccount',
        'SapPayTypeName', 'IsActive', 'SortOrder', 'Remarks',
      ],
      sorted.map((r) => {
        const e = getEdit(r);
        return [
          e.payTypeID,
          e.payTypeName,
          e.sapPayCategory,
          e.sapGlAccount ?? '',
          e.sapPayTypeName ?? '',
          e.isActive ? 1 : 0,
          e.sortOrder,
          e.remarks ?? '',
        ];
      })
    );
    toast.success(t('exportSuccess', { count: sorted.length }));
  }

  async function handleImportExcel(text: string) {
    const { rows: csvRows } = parseCsv(text);
    if (csvRows.length === 0) {
      toast.error(t('importMappingEmpty'));
      return;
    }

    let ok = 0;
    let skip = 0;
    let fail = 0;

    for (const raw of csvRows) {
      const payTypeID = csvInt(csvCell(raw, 'PayTypeID', 'payTypeID', 'paytypeid'));
      if (!payTypeID) {
        skip++;
        continue;
      }

      const catRaw = csvCell(raw, 'SapPayCategory', 'sapPayCategory').toUpperCase();
      const sapPayCategory = (CATEGORIES.includes(catRaw as SapPayCategory)
        ? catRaw
        : 'SKIP') as SapPayCategory;

      const existing = rows.find((r) => r.payTypeID === payTypeID);
      const unmappedHit = unmapped.find((u) => u.payTypeID === payTypeID);
      const payTypeName =
        csvNullable(csvCell(raw, 'PayTypeName', 'payTypeName'))
        ?? existing?.payTypeName
        ?? unmappedHit?.payTypeName
        ?? `PayType ${payTypeID}`;

      const payload: UpsertGlMappingDto = {
        payTypeID,
        payTypeName,
        sapPayCategory,
        sapGlAccount: csvNullable(csvCell(raw, 'SapGlAccount', 'sapGlAccount')),
        sapPayTypeName: csvNullable(csvCell(raw, 'SapPayTypeName', 'sapPayTypeName')),
        isActive: csvBool(csvCell(raw, 'IsActive', 'isActive'), existing?.isActive ?? true),
        sortOrder: csvInt(csvCell(raw, 'SortOrder', 'sortOrder'), existing?.sortOrder ?? 99),
        remarks: csvNullable(csvCell(raw, 'Remarks', 'remarks')),
      };

      try {
        await glMappingService.upsert(payload);
        ok++;
      } catch {
        fail++;
      }
    }

    await Promise.all([
      qc.invalidateQueries({ queryKey: ['glmapping'] }),
      qc.invalidateQueries({ queryKey: ['glmapping-unmapped'] }),
    ]);
    setEdits({});

    if (ok === 0 && fail === 0) {
      toast.error(t('importMappingInvalid'));
      return;
    }
    if (fail > 0 || skip > 0) {
      toast.error(t('importMappingPartial', { ok, skip, fail }));
    } else {
      toast.success(t('importMappingSuccess', { count: ok }));
    }
  }

  return (
    <div className="space-y-6 pb-24">
      <MappingPageHeader
        icon={Map}
        title={t('glMappingTitle')}
        subtitle={t('glMappingSubtitle')}
      />

      {isError && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          <span>{t('glMappingLoadError')}</span>
          <button type="button" onClick={() => refetch()} className="text-xs underline">{t('retry')}</button>
        </div>
      )}

      <MappingStatGrid
        items={[
          { label: t('glMappingStatTotal'), value: stats.total },
          { label: t('glMappingStatActive'), value: stats.active, accent: 'green' },
          { label: t('glMappingStatPendingGl'), value: stats.pendingGl, warn: stats.pendingGl > 0 },
          { label: t('glMappingStatUnmapped'), value: stats.unmapped, accent: 'muted' },
        ]}
      />

      {stats.pendingGl > 0 && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-500/40 dark:bg-amber-950/50 dark:text-amber-200">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{t('glMappingPendingGlHint')}</span>
        </div>
      )}

      <div className="flex flex-wrap gap-2 text-sm">
        {CATEGORIES.map((c) => (
          <span key={c} className={cn('rounded-md px-3 py-1 font-semibold shadow-sm', CATEGORY_BADGE[c])}>
            {categoryLabel(c)}
          </span>
        ))}
      </div>

      <MappingToolbar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder={t('glMappingSearchPlaceholder')}
        showClear={Boolean(search || categoryFilter)}
        onClear={() => { setSearch(''); setCategoryFilter(''); }}
        clearLabel={t('clearButton')}
        filter={(
          <div className="flex items-center gap-2">
            <label htmlFor="categoryFilter" className="whitespace-nowrap text-sm font-medium">
              {t('glMappingFilterCategory')}
            </label>
            <select
              id="categoryFilter"
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value as CategoryFilter)}
              className="min-w-44 rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">{t('glMappingFilterAll')}</option>
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>{categoryLabel(c)}</option>
              ))}
              <option value="PENDING_GL">{t('glMappingFilterPendingGl')}</option>
            </select>
          </div>
        )}
        actions={(
          <MappingExcelActions
            disabled={isBusy}
            exportDisabled={isLoading || rows.length === 0}
            onExport={handleExportExcel}
            onImportText={handleImportExcel}
          />
        )}
      />

      <MappingSection
        variant="mapped"
        title={t('mappingSectionMapped')}
        hint={t('glMappingSubtitle')}
        count={filteredRows.length}
        isEmpty={!isLoading && filteredRows.length === 0}
        emptyMessage={t('noData')}
      >
        <div className="overflow-x-auto">
          <table className={mappingTableClass}>
            <thead className={mappingTableHeadClass}>
              <tr>
                <th className="px-3 py-2 text-left w-16">{t('glMappingPayTypeID')}</th>
                <th className="px-3 py-2 text-left min-w-36">{t('glMappingPayTypeName')}</th>
                <th className="px-3 py-2 text-left w-40">{t('glMappingCategory')}</th>
                <th className="px-3 py-2 text-left min-w-32">{t('glMappingGlAccount')}</th>
                <th className="px-3 py-2 text-left min-w-36">{t('glMappingSapPayType')}</th>
                <th className="px-3 py-2 text-center w-16">{t('glMappingActive')}</th>
                <th className="px-3 py-2 text-left w-20">{t('glMappingSortOrder')}</th>
                <th className="px-3 py-2 text-left min-w-32">{t('glMappingRemarks')}</th>
                <th className="px-3 py-2 text-left w-36">{t('glMappingUpdatedAt')}</th>
                <th className="px-3 py-2 text-center w-20">{t('actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {isLoading && (
                <tr><td colSpan={10} className="px-3 py-8 text-center text-muted-foreground">{t('loading')}</td></tr>
              )}
              {mappedPager.paginated.map((row) => {
                const e = getEdit(row);
                const dirty = isRowDirty(row);
                const pending = isPendingGl(e);

                return (
                  <tr
                    key={row.payTypeID}
                    className={cn(
                      'transition-colors hover:bg-muted/30',
                      !e.isActive && 'opacity-50',
                      pending && 'bg-amber-50/60 dark:bg-amber-950/40',
                      dirty && 'bg-sky-50/50 ring-1 ring-inset ring-sky-200 dark:bg-sky-950/40 dark:ring-sky-500/30'
                    )}
                  >
                    <td className="px-3 py-1.5 font-mono text-xs text-muted-foreground">{row.payTypeID}</td>
                    <td className="px-3 py-1.5 font-medium">{row.payTypeName}</td>
                    <td className="px-3 py-1.5">
                      <div className={cn('flex items-center gap-2 border-l-4 pl-2', CATEGORY_BORDER[e.sapPayCategory])}>
                        <span className={cn('h-2.5 w-2.5 shrink-0 rounded-full', CATEGORY_DOT[e.sapPayCategory])} />
                        <select
                          value={e.sapPayCategory}
                          onChange={(ev) => setField(row.payTypeID, 'sapPayCategory', ev.target.value as SapPayCategory)}
                          className={cn(mappingInputClass, 'min-w-32 cursor-pointer font-medium')}
                        >
                          {CATEGORIES.map((c) => (
                            <option key={c} value={c}>{categoryLabel(c)}</option>
                          ))}
                        </select>
                      </div>
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="text"
                        value={e.sapGlAccount ?? ''}
                        onChange={(ev) => setField(row.payTypeID, 'sapGlAccount', ev.target.value || null)}
                        placeholder={t('glMappingGlAccountPlaceholder')}
                        disabled={e.sapPayCategory === 'SKIP'}
                        className={mappingInputClass}
                      />
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="text"
                        value={e.sapPayTypeName ?? ''}
                        onChange={(ev) => setField(row.payTypeID, 'sapPayTypeName', ev.target.value || null)}
                        placeholder={e.sapPayCategory === 'CREDIT_CARD' ? t('glMappingSapPayTypePlaceholder') : '—'}
                        disabled={e.sapPayCategory === 'SKIP' || e.sapPayCategory !== 'CREDIT_CARD'}
                        className={mappingInputClass}
                      />
                    </td>
                    <td className="px-3 py-1.5 text-center">
                      <input
                        type="checkbox"
                        checked={e.isActive}
                        onChange={(ev) => setField(row.payTypeID, 'isActive', ev.target.checked)}
                        className="h-4 w-4 cursor-pointer accent-primary"
                      />
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="number"
                        value={e.sortOrder}
                        onChange={(ev) => setField(row.payTypeID, 'sortOrder', parseInt(ev.target.value, 10) || 0)}
                        className="w-16 rounded-md border border-input bg-background px-2 py-1.5 text-sm"
                      />
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="text"
                        value={e.remarks ?? ''}
                        onChange={(ev) => setField(row.payTypeID, 'remarks', ev.target.value || null)}
                        placeholder={t('glMappingRemarksPlaceholder')}
                        className={mappingInputClass}
                      />
                    </td>
                    <td className="px-3 py-1.5 whitespace-nowrap text-xs text-muted-foreground">
                      {fmtDatetime(row.updatedAt)}
                    </td>
                    <td className="px-3 py-1.5 text-center">
                      <button
                        type="button"
                        onClick={() => {
                          setPendingDeleteId(row.payTypeID);
                          setConfirm({ type: 'delete', name: row.payTypeName });
                        }}
                        disabled={isBusy}
                        title={t('glMappingDelete')}
                        className="rounded p-1.5 text-destructive hover:bg-destructive/10 disabled:opacity-30"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <MappingPagination
          page={mappedPager.page}
          totalPages={mappedPager.totalPages}
          total={mappedPager.total}
          from={mappedPager.from}
          to={mappedPager.to}
          pageSize={mappedPager.pageSize}
          onPageChange={mappedPager.setPage}
          onPageSizeChange={mappedPager.setPageSize}
          disabled={isLoading}
          labels={paginationLabels}
        />
      </MappingSection>

      <MappingSection
        variant="available"
        title={t('mappingSectionAvailable')}
        hint={t('glMappingUnmapped')}
        count={filteredUnmapped.length}
        isEmpty={filteredUnmapped.length === 0}
        emptyMessage={t('noData')}
      >
        <div className="overflow-x-auto">
          <table className={mappingTableClass}>
            <thead className={mappingTableHeadClass}>
              <tr>
                <th className="px-3 py-2 text-left w-16">{t('glMappingPayTypeID')}</th>
                <th className="px-3 py-2 text-left">{t('glMappingPayTypeName')}</th>
                <th className="px-3 py-2 text-right w-36">{t('actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {unmappedPager.paginated.map((u) => (
                <tr key={u.payTypeID} className="hover:bg-muted/30">
                  <td className="px-3 py-1.5 font-mono text-xs text-muted-foreground">{u.payTypeID}</td>
                  <td className="px-3 py-1.5">{u.payTypeName}</td>
                  <td className="px-3 py-1.5 text-right">
                    <MappingActionButton
                      variant="add"
                      label={t('glMappingAddRow')}
                      disabled={isBusy}
                      onClick={() => {
                        setPendingAdd({ payTypeId: u.payTypeID, payTypeName: u.payTypeName });
                        setConfirm({ type: 'add', name: u.payTypeName });
                      }}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <MappingPagination
          page={unmappedPager.page}
          totalPages={unmappedPager.totalPages}
          total={unmappedPager.total}
          from={unmappedPager.from}
          to={unmappedPager.to}
          pageSize={unmappedPager.pageSize}
          onPageChange={unmappedPager.setPage}
          onPageSizeChange={unmappedPager.setPageSize}
          labels={paginationLabels}
        />
      </MappingSection>

      <MappingUnsavedBar
        visible={hasUnsaved}
        message={t('mappingUnsavedChanges')}
        discardLabel={t('mappingDiscard')}
        saveLabel={t('mappingSaveChanges')}
        saving={saveAllMutation.isPending}
        onDiscard={() => setConfirm({ type: 'discard' })}
        onSave={() => setConfirm({ type: 'save' })}
      />

      <MappingConfirmDialog
        confirm={confirm}
        t={t}
        isLoading={isBusy}
        saveCount={dirtyRows.length}
        onConfirm={handleConfirm}
        onCancel={clearConfirm}
      />
    </div>
  );
}
