// src/pages/ProductGroupMappingPage.tsx
import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layers, Trash2, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import productGroupMappingService from '@/services/productGroupMappingService';
import {
  MappingConfirmDialog,
  type MappingConfirmState,
} from '@/components/mapping/MappingConfirmDialog';
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
import { useLanguage } from '@/contexts/LanguageContext';
import type {
  ProductGroupSapMappingDto,
  UpsertProductGroupMappingDto,
} from '@/types/productGroupMapping';
import { cn, fmtDatetime, todayStr } from '@/lib/utils';
import { downloadCsv } from '@/lib/downloadCsv';
import { csvBool, csvCell, csvInt, csvNullable, parseCsv } from '@/lib/parseCsv';
import { useMappingPagination } from '@/hooks/useMappingPagination';

const SAP_PENDING = '[SAP-PENDING]';

type RowEdit = Omit<UpsertProductGroupMappingDto, never>;
type StatusFilter = '' | 'PENDING_SAP' | 'ACTIVE' | 'INACTIVE';

type PendingAdd = { id: number; code: string; name: string };

function isPendingSap(edit: UpsertProductGroupMappingDto): boolean {
  if (!edit.isActive) return false;
  const code = edit.sapItemGroupCode?.trim() ?? '';
  return !code || code === SAP_PENDING;
}

function snapshotRow(row: ProductGroupSapMappingDto): UpsertProductGroupMappingDto {
  return {
    productGroupID: row.productGroupID,
    productGroupCode: row.productGroupCode,
    productGroupName: row.productGroupName,
    sapItemGroupCode: row.sapItemGroupCode,
    sapItemGroupName: row.sapItemGroupName,
    isActive: row.isActive,
    sortOrder: row.sortOrder,
    remarks: row.remarks,
  };
}

export default function ProductGroupMappingPage() {
  const { t } = useLanguage();
  const qc = useQueryClient();

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('');
  const [edits, setEdits] = useState<Record<number, Partial<RowEdit>>>({});
  const [confirm, setConfirm] = useState<MappingConfirmState>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [pendingAdd, setPendingAdd] = useState<PendingAdd | null>(null);

  const { data: rows = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['productgroup-mapping'],
    queryFn: () => productGroupMappingService.getAll(),
    staleTime: 30_000,
  });

  const { data: unmapped = [] } = useQuery({
    queryKey: ['productgroup-mapping-unmapped'],
    queryFn: () => productGroupMappingService.getUnmapped(),
    staleTime: 30_000,
  });

  function getEdit(row: ProductGroupSapMappingDto): UpsertProductGroupMappingDto {
    const e = edits[row.productGroupID] ?? {};
    return {
      productGroupID:   row.productGroupID,
      productGroupCode: e.productGroupCode ?? row.productGroupCode,
      productGroupName: e.productGroupName ?? row.productGroupName,
      sapItemGroupCode: e.sapItemGroupCode !== undefined ? e.sapItemGroupCode : row.sapItemGroupCode,
      sapItemGroupName: e.sapItemGroupName !== undefined ? e.sapItemGroupName : row.sapItemGroupName,
      isActive:         e.isActive !== undefined ? e.isActive : row.isActive,
      sortOrder:        e.sortOrder !== undefined ? e.sortOrder : row.sortOrder,
      remarks:          e.remarks !== undefined ? e.remarks : row.remarks,
    };
  }

  function isRowDirty(row: ProductGroupSapMappingDto): boolean {
    return JSON.stringify(getEdit(row)) !== JSON.stringify(snapshotRow(row));
  }

  function setField<K extends keyof RowEdit>(id: number, field: K, value: RowEdit[K]) {
    setEdits((prev) => ({ ...prev, [id]: { ...(prev[id] ?? {}), [field]: value } }));
  }

  function validateEdit(edit: UpsertProductGroupMappingDto): string | null {
    if (edit.isActive) {
      const code = edit.sapItemGroupCode?.trim() ?? '';
      if (!code || code === SAP_PENDING) return t('pgMappingValidationSapCode');
    }
    return null;
  }

  const saveAllMutation = useMutation({
    mutationFn: async (dirtyRows: ProductGroupSapMappingDto[]) => {
      for (const row of dirtyRows) {
        const edit = getEdit(row);
        const err = validateEdit(edit);
        if (err) throw new Error(err);
        await productGroupMappingService.upsert(edit);
      }
    },
    onSuccess: () => {
      toast.success(t('pgMappingSaved'));
      setEdits({});
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['productgroup-mapping'] });
      qc.invalidateQueries({ queryKey: ['productgroup-mapping-unmapped'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => productGroupMappingService.remove(id),
    onSuccess: () => {
      toast.success(t('pgMappingDeleted'));
      qc.invalidateQueries({ queryKey: ['productgroup-mapping'] });
      qc.invalidateQueries({ queryKey: ['productgroup-mapping-unmapped'] });
      clearConfirm();
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
      clearConfirm();
    },
  });

  const addMutation = useMutation({
    mutationFn: (payload: PendingAdd) =>
      productGroupMappingService.upsert({
        productGroupID: payload.id,
        productGroupCode: payload.code,
        productGroupName: payload.name,
        sapItemGroupCode: SAP_PENDING,
        sapItemGroupName: null,
        isActive: true,
        sortOrder: payload.id,
        remarks: null,
      }),
    onSuccess: () => {
      toast.success(t('pgMappingSaved'));
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['productgroup-mapping'] });
      qc.invalidateQueries({ queryKey: ['productgroup-mapping-unmapped'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
    },
  });

  const dirtyRows = useMemo(() => rows.filter(isRowDirty), [rows, edits]);
  const hasUnsaved = dirtyRows.length > 0;
  const isBusy = saveAllMutation.isPending || deleteMutation.isPending || addMutation.isPending;

  const filteredRows = useMemo(() => {
    let result = rows;

    if (statusFilter === 'PENDING_SAP') {
      result = result.filter((r) => isPendingSap(getEdit(r)));
    } else if (statusFilter === 'ACTIVE') {
      result = result.filter((r) => getEdit(r).isActive);
    } else if (statusFilter === 'INACTIVE') {
      result = result.filter((r) => !getEdit(r).isActive);
    }

    const q = search.trim().toLowerCase();
    if (q) {
      result = result.filter((r) => {
        const edit = getEdit(r);
        return (
          r.productGroupName.toLowerCase().includes(q) ||
          r.productGroupCode.toLowerCase().includes(q) ||
          String(r.productGroupID).includes(q) ||
          (edit.sapItemGroupCode ?? '').toLowerCase().includes(q)
        );
      });
    }

    return result;
  }, [rows, search, statusFilter, edits]);

  const filteredUnmapped = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return unmapped;
    return unmapped.filter(
      (u) =>
        u.productGroupName.toLowerCase().includes(q) ||
        u.productGroupCode.toLowerCase().includes(q) ||
        String(u.productGroupID).includes(q)
    );
  }, [unmapped, search]);

  const mappedPager = useMappingPagination(filteredRows);
  const unmappedPager = useMappingPagination(filteredUnmapped);
  const paginationLabels = useMemo(() => mappingPaginationLabels(t), [t]);

  const stats = useMemo(() => {
    const active = rows.filter((r) => r.isActive).length;
    const pendingSap = rows.filter((r) => isPendingSap(snapshotRow(r))).length;
    return { total: rows.length, active, pendingSap, unmapped: unmapped.length };
  }, [rows, unmapped.length]);

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
      addMutation.mutate(pendingAdd);
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
    const sorted = [...rows].sort((a, b) => a.productGroupID - b.productGroupID);
    downloadCsv(
      `product-group-mapping-${todayStr()}.csv`,
      [
        'ProductGroupID', 'ProductGroupCode', 'ProductGroupName',
        'SapItemGroupCode', 'SapItemGroupName', 'IsActive', 'SortOrder', 'Remarks',
      ],
      sorted.map((r) => {
        const e = getEdit(r);
        return [
          e.productGroupID,
          e.productGroupCode,
          e.productGroupName,
          e.sapItemGroupCode ?? '',
          e.sapItemGroupName ?? '',
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
      const productGroupID = csvInt(csvCell(raw, 'ProductGroupID', 'productGroupID'));
      if (!productGroupID) {
        skip++;
        continue;
      }

      const existing = rows.find((r) => r.productGroupID === productGroupID);
      const unmappedHit = unmapped.find((u) => u.productGroupID === productGroupID);
      const productGroupCode =
        csvNullable(csvCell(raw, 'ProductGroupCode', 'productGroupCode'))
        ?? existing?.productGroupCode
        ?? unmappedHit?.productGroupCode
        ?? String(productGroupID);
      const productGroupName =
        csvNullable(csvCell(raw, 'ProductGroupName', 'productGroupName'))
        ?? existing?.productGroupName
        ?? unmappedHit?.productGroupName
        ?? `Group ${productGroupID}`;

      const payload: UpsertProductGroupMappingDto = {
        productGroupID,
        productGroupCode,
        productGroupName,
        sapItemGroupCode: csvNullable(csvCell(raw, 'SapItemGroupCode', 'sapItemGroupCode')),
        sapItemGroupName: csvNullable(csvCell(raw, 'SapItemGroupName', 'sapItemGroupName')),
        isActive: csvBool(csvCell(raw, 'IsActive', 'isActive'), existing?.isActive ?? true),
        sortOrder: csvInt(csvCell(raw, 'SortOrder', 'sortOrder'), existing?.sortOrder ?? productGroupID),
        remarks: csvNullable(csvCell(raw, 'Remarks', 'remarks')),
      };

      try {
        await productGroupMappingService.upsert(payload);
        ok++;
      } catch {
        fail++;
      }
    }

    await Promise.all([
      qc.invalidateQueries({ queryKey: ['productgroup-mapping'] }),
      qc.invalidateQueries({ queryKey: ['productgroup-mapping-unmapped'] }),
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
        icon={Layers}
        title={t('pgMappingTitle')}
        subtitle={t('pgMappingSubtitle')}
      />

      {isError && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          <span>{t('pgMappingLoadError')}</span>
          <button type="button" onClick={() => refetch()} className="text-xs underline">{t('retry')}</button>
        </div>
      )}

      <MappingStatGrid
        items={[
          { label: t('pgMappingStatTotal'), value: stats.total },
          { label: t('pgMappingStatActive'), value: stats.active, accent: 'green' },
          { label: t('pgMappingStatPendingSap'), value: stats.pendingSap, warn: stats.pendingSap > 0 },
          { label: t('pgMappingStatUnmapped'), value: stats.unmapped, accent: 'muted' },
        ]}
      />

      {stats.pendingSap > 0 && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-500/40 dark:bg-amber-950/50 dark:text-amber-200">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{t('pgMappingPendingSapHint')}</span>
        </div>
      )}

      <MappingToolbar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder={t('pgMappingSearchPlaceholder')}
        showClear={Boolean(search || statusFilter)}
        onClear={() => { setSearch(''); setStatusFilter(''); }}
        clearLabel={t('clearButton')}
        filter={(
          <div className="flex items-center gap-2">
            <label htmlFor="pgStatusFilter" className="whitespace-nowrap text-sm font-medium">
              {t('pgMappingFilterStatus')}
            </label>
            <select
              id="pgStatusFilter"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
              className="min-w-40 rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
            >
              <option value="">{t('pgMappingFilterAll')}</option>
              <option value="PENDING_SAP">{t('pgMappingFilterPendingSap')}</option>
              <option value="ACTIVE">{t('pgMappingFilterActive')}</option>
              <option value="INACTIVE">{t('pgMappingFilterInactive')}</option>
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
        hint={t('pgMappingSubtitle')}
        count={filteredRows.length}
        isEmpty={!isLoading && filteredRows.length === 0}
        emptyMessage={t('noData')}
      >
        <div className="overflow-x-auto">
          <table className={mappingTableClass}>
            <thead className={mappingTableHeadClass}>
              <tr>
                <th className="px-3 py-2 text-left w-16">{t('pgMappingGroupID')}</th>
                <th className="px-3 py-2 text-left w-28">{t('pgMappingGroupCode')}</th>
                <th className="px-3 py-2 text-left min-w-36">{t('pgMappingGroupName')}</th>
                <th className="px-3 py-2 text-left min-w-32">{t('pgMappingSapCode')}</th>
                <th className="px-3 py-2 text-left min-w-36">{t('pgMappingSapName')}</th>
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
                const pending = isPendingSap(e);

                return (
                  <tr
                    key={row.productGroupID}
                    className={cn(
                      'transition-colors hover:bg-muted/30',
                      !e.isActive && 'opacity-50',
                      pending && 'bg-amber-50/60 dark:bg-amber-950/40',
                      dirty && 'bg-sky-50/50 ring-1 ring-inset ring-sky-200 dark:bg-sky-950/40 dark:ring-sky-500/30'
                    )}
                  >
                    <td className="px-3 py-1.5 font-mono text-xs text-muted-foreground">{row.productGroupID}</td>
                    <td className="px-3 py-1.5 font-mono text-xs">{row.productGroupCode}</td>
                    <td className="px-3 py-1.5 font-medium">{row.productGroupName}</td>
                    <td className="px-3 py-1.5">
                      <input
                        type="text"
                        value={e.sapItemGroupCode ?? ''}
                        onChange={(ev) => setField(row.productGroupID, 'sapItemGroupCode', ev.target.value || null)}
                        placeholder={t('pgMappingSapCodePlaceholder')}
                        disabled={!e.isActive}
                        className={mappingInputClass}
                      />
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="text"
                        value={e.sapItemGroupName ?? ''}
                        onChange={(ev) => setField(row.productGroupID, 'sapItemGroupName', ev.target.value || null)}
                        placeholder={t('pgMappingSapNamePlaceholder')}
                        disabled={!e.isActive}
                        className={mappingInputClass}
                      />
                    </td>
                    <td className="px-3 py-1.5 text-center">
                      <input
                        type="checkbox"
                        checked={e.isActive}
                        onChange={(ev) => setField(row.productGroupID, 'isActive', ev.target.checked)}
                        className="h-4 w-4 cursor-pointer accent-primary"
                      />
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="number"
                        value={e.sortOrder}
                        onChange={(ev) => setField(row.productGroupID, 'sortOrder', parseInt(ev.target.value, 10) || 0)}
                        className="w-16 rounded-md border border-input bg-background px-2 py-1.5 text-sm"
                      />
                    </td>
                    <td className="px-3 py-1.5">
                      <input
                        type="text"
                        value={e.remarks ?? ''}
                        onChange={(ev) => setField(row.productGroupID, 'remarks', ev.target.value || null)}
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
                          setPendingDeleteId(row.productGroupID);
                          setConfirm({ type: 'delete', name: row.productGroupName });
                        }}
                        disabled={isBusy}
                        title={t('pgMappingDelete')}
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
        hint={t('pgMappingUnmapped')}
        count={filteredUnmapped.length}
        isEmpty={filteredUnmapped.length === 0}
        emptyMessage={t('noData')}
      >
        <div className="overflow-x-auto">
          <table className={mappingTableClass}>
            <thead className={mappingTableHeadClass}>
              <tr>
                <th className="px-3 py-2 text-left w-16">{t('pgMappingGroupID')}</th>
                <th className="px-3 py-2 text-left w-28">{t('pgMappingGroupCode')}</th>
                <th className="px-3 py-2 text-left">{t('pgMappingGroupName')}</th>
                <th className="px-3 py-2 text-right w-36">{t('actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {unmappedPager.paginated.map((u) => (
                <tr key={u.productGroupID} className="hover:bg-muted/30">
                  <td className="px-3 py-1.5 font-mono text-xs text-muted-foreground">{u.productGroupID}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{u.productGroupCode}</td>
                  <td className="px-3 py-1.5">{u.productGroupName}</td>
                  <td className="px-3 py-1.5 text-right">
                    <MappingActionButton
                      variant="add"
                      label={t('pgMappingAddRow')}
                      disabled={isBusy}
                      onClick={() => {
                        setPendingAdd({
                          id: u.productGroupID,
                          code: u.productGroupCode,
                          name: u.productGroupName,
                        });
                        setConfirm({ type: 'add', name: u.productGroupName });
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
