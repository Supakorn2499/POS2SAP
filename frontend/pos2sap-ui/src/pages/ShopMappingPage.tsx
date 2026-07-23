// src/pages/ShopMappingPage.tsx
import { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Building2, Trash2, AlertTriangle } from 'lucide-react';
import { AppIcon } from '@/components/ui/AppIcon';
import { AppSelect } from '@/components/ui/AppSelect';
import { toast } from 'sonner';
import shopMappingService from '@/services/shopMappingService';
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
import type { ShopSapMappingDto, UpsertShopMappingDto } from '@/types/shopMapping';
import { cn, fmtDatetime, todayStr } from '@/lib/utils';
import { downloadCsv } from '@/lib/downloadCsv';
import { csvBool, csvCell, csvInt, csvNullable, parseCsv } from '@/lib/parseCsv';
import { useMappingPagination } from '@/hooks/useMappingPagination';

type RowEdit = Omit<UpsertShopMappingDto, never>;
type StatusFilter = '' | 'PENDING_SAP' | 'ACTIVE' | 'INACTIVE';
type PendingAdd = { id: number; code: string; name: string };

function isPendingSap(edit: UpsertShopMappingDto): boolean {
  if (!edit.isActive) return false;
  return !edit.sapCardCode?.trim() || !edit.sapBranchCode?.trim() || !edit.sapVatBranch?.trim();
}

function snapshotRow(row: ShopSapMappingDto): UpsertShopMappingDto {
  return {
    shopID: row.shopID,
    shopCode: row.shopCode,
    shopName: row.shopName,
    sapCardCode: row.sapCardCode,
    sapBranchCode: row.sapBranchCode,
    sapBranchName: row.sapBranchName,
    sapVatBranch: row.sapVatBranch,
    isActive: row.isActive,
    sortOrder: row.sortOrder,
    remarks: row.remarks,
  };
}

export default function ShopMappingPage() {
  const { t } = useLanguage();
  const qc = useQueryClient();

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('');
  const [edits, setEdits] = useState<Record<number, Partial<RowEdit>>>({});
  const [confirm, setConfirm] = useState<MappingConfirmState>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<number | null>(null);
  const [pendingAdd, setPendingAdd] = useState<PendingAdd | null>(null);

  const { data: rows = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['shop-mapping'],
    queryFn: () => shopMappingService.getAll(),
    staleTime: 30_000,
  });

  const { data: unmapped = [] } = useQuery({
    queryKey: ['shop-mapping-unmapped'],
    queryFn: () => shopMappingService.getUnmapped(),
    staleTime: 30_000,
  });

  function getEdit(row: ShopSapMappingDto): UpsertShopMappingDto {
    return { ...snapshotRow(row), ...(edits[row.shopID] ?? {}) };
  }

  function isRowDirty(row: ShopSapMappingDto): boolean {
    const snap = snapshotRow(row);
    const edit = getEdit(row);
    return (Object.keys(snap) as (keyof UpsertShopMappingDto)[]).some((k) => snap[k] !== edit[k]);
  }

  function setField<K extends keyof RowEdit>(id: number, field: K, value: RowEdit[K]) {
    setEdits((prev) => ({ ...prev, [id]: { ...(prev[id] ?? {}), [field]: value } }));
  }

  function validateEdit(edit: UpsertShopMappingDto): string | null {
    if (edit.isActive && isPendingSap(edit)) return t('shopMappingValidationRequired');
    return null;
  }

  const saveAllMutation = useMutation({
    mutationFn: async (dirtyRows: ShopSapMappingDto[]) => {
      for (const row of dirtyRows) {
        const edit = getEdit(row);
        const err = validateEdit(edit);
        if (err) throw new Error(err);
        await shopMappingService.upsert(edit);
      }
    },
    onSuccess: () => {
      toast.success(t('shopMappingSaved'));
      setEdits({});
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['shop-mapping'] });
      qc.invalidateQueries({ queryKey: ['shop-mapping-unmapped'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => shopMappingService.remove(id),
    onSuccess: () => {
      toast.success(t('shopMappingDeleted'));
      qc.invalidateQueries({ queryKey: ['shop-mapping'] });
      qc.invalidateQueries({ queryKey: ['shop-mapping-unmapped'] });
      clearConfirm();
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
      clearConfirm();
    },
  });

  const addMutation = useMutation({
    mutationFn: (payload: PendingAdd) =>
      shopMappingService.upsert({
        shopID: payload.id,
        shopCode: payload.code,
        shopName: payload.name,
        sapCardCode: '',
        sapBranchCode: payload.code,
        sapBranchName: payload.name,
        sapVatBranch: '',
        isActive: true,
        sortOrder: payload.id,
        remarks: null,
      }),
    onSuccess: () => {
      toast.success(t('shopMappingSaved'));
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['shop-mapping'] });
      qc.invalidateQueries({ queryKey: ['shop-mapping-unmapped'] });
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
    if (statusFilter === 'PENDING_SAP') result = result.filter((r) => isPendingSap(getEdit(r)));
    else if (statusFilter === 'ACTIVE') result = result.filter((r) => getEdit(r).isActive);
    else if (statusFilter === 'INACTIVE') result = result.filter((r) => !getEdit(r).isActive);

    const q = search.trim().toLowerCase();
    if (q) {
      result = result.filter((r) => {
        const e = getEdit(r);
        return (
          r.shopName.toLowerCase().includes(q) ||
          r.shopCode.toLowerCase().includes(q) ||
          String(r.shopID).includes(q) ||
          (e.sapBranchCode ?? '').toLowerCase().includes(q) ||
          (e.sapCardCode ?? '').toLowerCase().includes(q)
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
        u.shopName.toLowerCase().includes(q) ||
        u.shopCode.toLowerCase().includes(q) ||
        String(u.shopID).includes(q)
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

  function clearConfirm() {
    setConfirm(null);
    setPendingDeleteId(null);
    setPendingAdd(null);
  }

  function handleConfirm() {
    if (!confirm) return;
    if (confirm.type === 'save') saveAllMutation.mutate(dirtyRows);
    else if (confirm.type === 'discard') {
      setEdits({});
      clearConfirm();
    } else if (confirm.type === 'delete' && pendingDeleteId !== null) deleteMutation.mutate(pendingDeleteId);
    else if (confirm.type === 'add' && pendingAdd) addMutation.mutate(pendingAdd);
  }

  function handleExportExcel() {
    if (rows.length === 0) {
      toast.error(t('exportEmpty'));
      return;
    }
    const sorted = [...rows].sort((a, b) => a.shopID - b.shopID);
    downloadCsv(
      `shop-mapping-${todayStr()}.csv`,
      [
        'ShopID', 'ShopCode', 'ShopName',
        'SapCardCode', 'SapBranchCode', 'SapBranchName', 'SapVatBranch',
        'IsActive', 'SortOrder', 'Remarks',
      ],
      sorted.map((r) => {
        const e = getEdit(r);
        return [
          e.shopID, e.shopCode, e.shopName,
          e.sapCardCode ?? '', e.sapBranchCode ?? '', e.sapBranchName ?? '', e.sapVatBranch ?? '',
          e.isActive ? 1 : 0, e.sortOrder, e.remarks ?? '',
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
    let ok = 0, skip = 0, fail = 0;
    for (const raw of csvRows) {
      const shopID = csvInt(csvCell(raw, 'ShopID', 'shopID'));
      if (!shopID) { skip++; continue; }
      const existing = rows.find((r) => r.shopID === shopID);
      const unmappedHit = unmapped.find((u) => u.shopID === shopID);
      const payload: UpsertShopMappingDto = {
        shopID,
        shopCode: csvNullable(csvCell(raw, 'ShopCode', 'shopCode')) ?? existing?.shopCode ?? unmappedHit?.shopCode ?? String(shopID),
        shopName: csvNullable(csvCell(raw, 'ShopName', 'shopName')) ?? existing?.shopName ?? unmappedHit?.shopName ?? `Shop ${shopID}`,
        sapCardCode: csvNullable(csvCell(raw, 'SapCardCode', 'sapCardCode')),
        sapBranchCode: csvNullable(csvCell(raw, 'SapBranchCode', 'sapBranchCode')),
        sapBranchName: csvNullable(csvCell(raw, 'SapBranchName', 'sapBranchName')),
        sapVatBranch: csvNullable(csvCell(raw, 'SapVatBranch', 'sapVatBranch')),
        isActive: csvBool(csvCell(raw, 'IsActive', 'isActive'), existing?.isActive ?? true),
        sortOrder: csvInt(csvCell(raw, 'SortOrder', 'sortOrder'), existing?.sortOrder ?? shopID),
        remarks: csvNullable(csvCell(raw, 'Remarks', 'remarks')),
      };
      try {
        await shopMappingService.upsert(payload);
        ok++;
      } catch {
        fail++;
      }
    }
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['shop-mapping'] }),
      qc.invalidateQueries({ queryKey: ['shop-mapping-unmapped'] }),
    ]);
    setEdits({});
    if (ok > 0) toast.success(t('importMappingSuccess', { count: ok }));
    if (fail > 0 || skip > 0) toast.error(t('importMappingPartial', { ok, skip, fail }));
  }

  if (isError) {
    return (
      <div className="space-y-4 p-1">
        <MappingPageHeader icon={Building2} title={t('shopMappingTitle')} subtitle={t('shopMappingSubtitle')} />
        <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">
          {t('shopMappingLoadError')}
          <button type="button" className="ml-3 underline" onClick={() => refetch()}>{t('retry')}</button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 pb-24">
      <MappingPageHeader icon={Building2} title={t('shopMappingTitle')} subtitle={t('shopMappingSubtitle')} />

      <MappingStatGrid
        items={[
          { label: t('shopMappingStatTotal'), value: stats.total },
          { label: t('shopMappingStatActive'), value: stats.active, accent: 'green' },
          { label: t('shopMappingStatPendingSap'), value: stats.pendingSap, warn: stats.pendingSap > 0 },
          { label: t('shopMappingStatUnmapped'), value: stats.unmapped, accent: 'muted' },
        ]}
      />

      {stats.pendingSap > 0 && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-300/60 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-500/40 dark:bg-amber-950/40 dark:text-amber-100">
          <AppIcon icon={AlertTriangle} className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{t('shopMappingPendingSapHint')}</span>
        </div>
      )}

      <MappingToolbar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder={t('shopMappingSearchPlaceholder')}
        filter={
          <AppSelect
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
            wrapperClassName="min-w-[10rem]"
            aria-label={t('shopMappingFilterStatus')}
          >
            <option value="">{t('shopMappingFilterAll')}</option>
            <option value="PENDING_SAP">{t('shopMappingFilterPending')}</option>
            <option value="ACTIVE">{t('shopMappingFilterActive')}</option>
            <option value="INACTIVE">{t('shopMappingFilterInactive')}</option>
          </AppSelect>
        }
        actions={
          <MappingExcelActions
            disabled={isBusy}
            onExport={handleExportExcel}
            onImportText={handleImportExcel}
          />
        }
      />

      <MappingSection
        variant="mapped"
        title={t('mappingSectionMapped')}
        hint={t('shopMappingSubtitle')}
        count={filteredRows.length}
        isEmpty={!isLoading && filteredRows.length === 0}
        emptyMessage={t('noData')}
      >
        <div className="overflow-x-auto">
          <table className={mappingTableClass}>
            <thead className={mappingTableHeadClass}>
              <tr>
                <th className="px-3 py-2 text-left w-14">{t('shopMappingShopID')}</th>
                <th className="px-3 py-2 text-left w-24">{t('shopMappingShopCode')}</th>
                <th className="px-3 py-2 text-left min-w-36">{t('shopMappingShopName')}</th>
                <th className="px-3 py-2 text-left min-w-28">{t('shopMappingSapCardCode')}</th>
                <th className="px-3 py-2 text-left min-w-28">{t('shopMappingSapBranchCode')}</th>
                <th className="px-3 py-2 text-left min-w-36">{t('shopMappingSapBranchName')}</th>
                <th className="px-3 py-2 text-left min-w-24">{t('shopMappingSapVatBranch')}</th>
                <th className="px-3 py-2 text-center w-14">{t('glMappingActive')}</th>
                <th className="px-3 py-2 text-left min-w-28">{t('glMappingRemarks')}</th>
                <th className="px-3 py-2 text-left w-32">{t('glMappingUpdatedAt')}</th>
                <th className="px-3 py-2 text-center w-16">{t('actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {isLoading && (
                <tr><td colSpan={11} className="px-3 py-8 text-center text-muted-foreground">{t('loading')}</td></tr>
              )}
              {mappedPager.paginated.map((row) => {
                const e = getEdit(row);
                const dirty = isRowDirty(row);
                const pending = isPendingSap(e);
                return (
                  <tr
                    key={row.shopID}
                    className={cn(
                      'transition-colors hover:bg-muted/30',
                      !e.isActive && 'opacity-50',
                      pending && 'bg-amber-50/60 dark:bg-amber-950/40',
                      dirty && 'bg-sky-50/50 ring-1 ring-inset ring-sky-200 dark:bg-sky-950/40 dark:ring-sky-500/30'
                    )}
                  >
                    <td className="px-3 py-1.5 font-mono text-xs text-muted-foreground">{row.shopID}</td>
                    <td className="px-3 py-1.5 font-mono text-xs">{row.shopCode}</td>
                    <td className="px-3 py-1.5 font-medium">{row.shopName}</td>
                    <td className="px-3 py-1.5">
                      <input type="text" value={e.sapCardCode ?? ''} disabled={!e.isActive}
                        onChange={(ev) => setField(row.shopID, 'sapCardCode', ev.target.value || null)}
                        placeholder={row.posSloc || 'CardCode'} className={mappingInputClass} title={`POS SLOC: ${row.posSloc}`} />
                    </td>
                    <td className="px-3 py-1.5">
                      <input type="text" value={e.sapBranchCode ?? ''} disabled={!e.isActive}
                        onChange={(ev) => setField(row.shopID, 'sapBranchCode', ev.target.value || null)}
                        placeholder={row.posBranchCode || 'BranchCode'} className={mappingInputClass} title={`POS: ${row.posBranchCode}`} />
                    </td>
                    <td className="px-3 py-1.5">
                      <input type="text" value={e.sapBranchName ?? ''} disabled={!e.isActive}
                        onChange={(ev) => setField(row.shopID, 'sapBranchName', ev.target.value || null)}
                        placeholder={row.shopName} className={mappingInputClass} />
                    </td>
                    <td className="px-3 py-1.5">
                      <input type="text" value={e.sapVatBranch ?? ''} disabled={!e.isActive}
                        onChange={(ev) => setField(row.shopID, 'sapVatBranch', ev.target.value || null)}
                        placeholder={row.posVatBranch || 'VatBranch'} className={mappingInputClass} title={`POS BranchNo: ${row.posVatBranch}`} />
                    </td>
                    <td className="px-3 py-1.5 text-center">
                      <input type="checkbox" checked={e.isActive}
                        onChange={(ev) => setField(row.shopID, 'isActive', ev.target.checked)}
                        className="h-4 w-4 cursor-pointer accent-primary" />
                    </td>
                    <td className="px-3 py-1.5">
                      <input type="text" value={e.remarks ?? ''}
                        onChange={(ev) => setField(row.shopID, 'remarks', ev.target.value || null)}
                        placeholder={t('glMappingRemarksPlaceholder')} className={mappingInputClass} />
                    </td>
                    <td className="px-3 py-1.5 whitespace-nowrap text-xs text-muted-foreground">{fmtDatetime(row.updatedAt)}</td>
                    <td className="px-3 py-1.5 text-center">
                      <button type="button" disabled={isBusy} title={t('shopMappingDelete')}
                        onClick={() => { setPendingDeleteId(row.shopID); setConfirm({ type: 'delete', name: row.shopName }); }}
                        className="rounded p-1.5 text-destructive hover:bg-destructive/10 disabled:opacity-30">
                        <AppIcon icon={Trash2} className="h-3.5 w-3.5" />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <MappingPagination
          page={mappedPager.page} totalPages={mappedPager.totalPages} total={mappedPager.total}
          from={mappedPager.from} to={mappedPager.to} pageSize={mappedPager.pageSize}
          onPageChange={mappedPager.setPage} onPageSizeChange={mappedPager.setPageSize}
          disabled={isLoading} labels={paginationLabels}
        />
      </MappingSection>

      <MappingSection
        variant="available"
        title={t('mappingSectionAvailable')}
        hint={t('shopMappingUnmapped')}
        count={filteredUnmapped.length}
        isEmpty={filteredUnmapped.length === 0}
        emptyMessage={t('noData')}
      >
        <div className="overflow-x-auto">
          <table className={mappingTableClass}>
            <thead className={mappingTableHeadClass}>
              <tr>
                <th className="px-3 py-2 text-left w-16">{t('shopMappingShopID')}</th>
                <th className="px-3 py-2 text-left w-28">{t('shopMappingShopCode')}</th>
                <th className="px-3 py-2 text-left">{t('shopMappingShopName')}</th>
                <th className="px-3 py-2 text-right w-36">{t('actions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {unmappedPager.paginated.map((u) => (
                <tr key={u.shopID} className="hover:bg-muted/30">
                  <td className="px-3 py-1.5 font-mono text-xs text-muted-foreground">{u.shopID}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{u.shopCode}</td>
                  <td className="px-3 py-1.5">{u.shopName}</td>
                  <td className="px-3 py-1.5 text-right">
                    <MappingActionButton
                      variant="add"
                      label={t('shopMappingAddRow')}
                      disabled={isBusy}
                      onClick={() => {
                        setPendingAdd({ id: u.shopID, code: u.shopCode, name: u.shopName });
                        setConfirm({ type: 'add', name: u.shopName });
                      }}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <MappingPagination
          page={unmappedPager.page} totalPages={unmappedPager.totalPages} total={unmappedPager.total}
          from={unmappedPager.from} to={unmappedPager.to} pageSize={unmappedPager.pageSize}
          onPageChange={unmappedPager.setPage} onPageSizeChange={unmappedPager.setPageSize}
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
        onCancel={clearConfirm}
        onConfirm={handleConfirm}
      />
    </div>
  );
}
