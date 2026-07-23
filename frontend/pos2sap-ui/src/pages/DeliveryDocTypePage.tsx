import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileSpreadsheet, Trash2, Truck } from 'lucide-react';
import { AppIcon } from '@/components/ui/AppIcon';
import { toast } from 'sonner';
import deliveryDocTypeService from '@/services/deliveryDocTypeService';
import {
  MappingActionButton,
  MappingPageHeader,
  MappingSection,
  MappingStatGrid,
  MappingToolbar,
  MappingPagination,
  mappingPaginationLabels,
  mappingTableClass,
  mappingTableHeadClass,
} from '@/components/mapping/MappingPageLayout';
import {
  MappingConfirmDialog,
  type MappingConfirmState,
} from '@/components/mapping/MappingConfirmDialog';
import { useLanguage } from '@/contexts/LanguageContext';
import type { DeliveryDocTypeDto } from '@/types/deliveryDocType';
import { useMappingPagination } from '@/hooks/useMappingPagination';
import { downloadCsv } from '@/lib/downloadCsv';
import { todayStr } from '@/lib/utils';

function matchesSearch(row: DeliveryDocTypeDto, q: string) {
  if (!q) return true;
  const needle = q.toLowerCase();
  return (
    row.documentTypeCode.toLowerCase().includes(needle) ||
    row.documentTypeName.toLowerCase().includes(needle)
  );
}

function rowLabel(row: DeliveryDocTypeDto) {
  const code = row.documentTypeCode?.trim();
  if (code) return `${code} — ${row.documentTypeName}`;
  return row.documentTypeName;
}

export default function DeliveryDocTypePage() {
  const { t } = useLanguage();
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [confirm, setConfirm] = useState<MappingConfirmState>(null);
  const [pendingId, setPendingId] = useState<number | null>(null);

  const { data: rows = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['delivery-doctype'],
    queryFn: () => deliveryDocTypeService.getAll(),
    staleTime: 30_000,
  });

  useEffect(() => {
    setSelected(new Set(rows.filter((r) => r.isEnabled).map((r) => r.documentTypeId)));
  }, [rows]);

  const q = search.trim();

  const selectedRows = useMemo(
    () =>
      rows
        .filter((r) => selected.has(r.documentTypeId) && matchesSearch(r, q))
        .sort((a, b) => a.documentTypeCode.localeCompare(b.documentTypeCode)),
    [rows, selected, q]
  );

  const availableRows = useMemo(
    () =>
      rows
        .filter((r) => !selected.has(r.documentTypeId) && matchesSearch(r, q))
        .sort((a, b) => a.documentTypeCode.localeCompare(b.documentTypeCode)),
    [rows, selected, q]
  );

  const selectedPager = useMappingPagination(selectedRows);
  const availablePager = useMappingPagination(availableRows);
  const paginationLabels = useMemo(() => mappingPaginationLabels(t), [t]);

  const saveMutation = useMutation({
    mutationFn: (enabledIds: number[]) =>
      deliveryDocTypeService.save({ enabledDocumentTypeIds: enabledIds }),
    onSuccess: () => {
      toast.success(t('dlDocTypeSaved'));
      clearConfirm();
      qc.invalidateQueries({ queryKey: ['delivery-doctype'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
      clearConfirm();
    },
  });

  function clearConfirm() {
    setConfirm(null);
    setPendingId(null);
  }

  function handleExportExcel() {
    if (rows.length === 0) {
      toast.error(t('exportEmpty'));
      return;
    }
    const sorted = [...rows].sort((a, b) =>
      a.documentTypeCode.localeCompare(b.documentTypeCode)
    );
    downloadCsv(
      `delivery-doctype-${todayStr()}.csv`,
      ['DocumentTypeId', 'DocumentTypeCode', 'DocumentTypeName', 'Status', 'IsEnabled'],
      sorted.map((r) => {
        const on = selected.has(r.documentTypeId);
        return [
          r.documentTypeId,
          r.documentTypeCode,
          r.documentTypeName,
          on ? 'Enabled' : 'Disabled',
          on ? 1 : 0,
        ];
      })
    );
    toast.success(t('exportSuccess', { count: sorted.length }));
  }

  function handleConfirm() {
    if (!confirm || pendingId === null) return;

    if (confirm.type === 'add') {
      const nextIds = [...selected, pendingId];
      saveMutation.mutate(nextIds);
      return;
    }

    if (confirm.type === 'remove') {
      const nextIds = Array.from(selected).filter((id) => id !== pendingId);
      saveMutation.mutate(nextIds);
    }
  }

  return (
    <div className="space-y-6 pb-8">
      <MappingPageHeader
        icon={Truck}
        title={t('dlDocTypeTitle')}
        subtitle={t('dlDocTypeSubtitle')}
      />

      {isError && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-destructive bg-destructive/10 p-3 text-sm text-destructive">
          <span>{t('dlDocTypeLoadError')}</span>
          <button type="button" onClick={() => refetch()} className="text-xs underline">{t('retry')}</button>
        </div>
      )}

      <MappingStatGrid
        items={[
          { label: t('dlDocTypeStatTotal'), value: rows.length },
          { label: t('dlDocTypeStatEnabled'), value: selected.size, accent: 'green' },
          {
            label: t('dlDocTypeStatDisabled'),
            value: Math.max(0, rows.length - selected.size),
            accent: 'muted',
          },
        ]}
      />

      <MappingToolbar
        search={search}
        onSearchChange={setSearch}
        searchPlaceholder={t('dlDocTypeSearchPlaceholder')}
        showClear={Boolean(search)}
        onClear={() => setSearch('')}
        clearLabel={t('clearButton')}
        actions={
          <button
            type="button"
            onClick={handleExportExcel}
            disabled={isLoading || rows.length === 0}
            className="inline-flex items-center gap-1.5 rounded-md border border-input bg-background px-3 py-2 text-sm font-medium hover:bg-muted disabled:opacity-40"
          >
            <AppIcon icon={FileSpreadsheet} className="h-4 w-4" />
            {t('exportToExcel')}
          </button>
        }
      />

      <p className="text-xs text-muted-foreground">{t('dlDocTypeHint')}</p>

      {isLoading ? (
        <p className="text-sm text-muted-foreground">{t('loading')}</p>
      ) : (
        <div className="space-y-6">
          <MappingSection
            variant="mapped"
            title={t('mappingSectionMapped')}
            hint={t('dlDocTypeSectionSelectedHint')}
            count={selectedRows.length}
            isEmpty={selectedRows.length === 0}
            emptyMessage={t('dlDocTypeNoSelected')}
          >
            <div className="overflow-x-auto">
              <table className={mappingTableClass}>
                <thead className={mappingTableHeadClass}>
                  <tr>
                    <th className="px-3 py-2 text-left">{t('dlDocTypeCode')}</th>
                    <th className="px-3 py-2 text-left">{t('dlDocTypeName')}</th>
                    <th className="px-3 py-2 text-center w-20">{t('actions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {selectedPager.paginated.map((row) => (
                    <tr key={row.documentTypeId} className="bg-green-50/40 hover:bg-green-50/70">
                      <td className="px-3 py-1.5 font-mono text-xs">{row.documentTypeCode || '—'}</td>
                      <td className="px-3 py-1.5">{row.documentTypeName}</td>
                      <td className="px-3 py-1.5 text-center">
                        <button
                          type="button"
                          title={t('dlDocTypeRemove')}
                          disabled={saveMutation.isPending}
                          onClick={() => {
                            setPendingId(row.documentTypeId);
                            setConfirm({ type: 'remove', name: rowLabel(row) });
                          }}
                          className="rounded p-1.5 text-destructive hover:bg-destructive/10 disabled:opacity-30"
                        >
                          <AppIcon icon={Trash2} className="h-3.5 w-3.5" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <MappingPagination
              page={selectedPager.page}
              totalPages={selectedPager.totalPages}
              total={selectedPager.total}
              from={selectedPager.from}
              to={selectedPager.to}
              pageSize={selectedPager.pageSize}
              onPageChange={selectedPager.setPage}
              onPageSizeChange={selectedPager.setPageSize}
              disabled={isLoading || saveMutation.isPending}
              labels={paginationLabels}
            />
          </MappingSection>

          <MappingSection
            variant="available"
            title={t('mappingSectionAvailable')}
            hint={t('dlDocTypeSectionAvailableHint')}
            count={availableRows.length}
            isEmpty={availableRows.length === 0}
            emptyMessage={t('dlDocTypeNoAvailable')}
          >
            <div className="overflow-x-auto">
              <table className={mappingTableClass}>
                <thead className={mappingTableHeadClass}>
                  <tr>
                    <th className="px-3 py-2 text-left">{t('dlDocTypeCode')}</th>
                    <th className="px-3 py-2 text-left">{t('dlDocTypeName')}</th>
                    <th className="px-3 py-2 text-center w-36">{t('actions')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {availablePager.paginated.map((row) => (
                    <tr key={row.documentTypeId} className="hover:bg-muted/30">
                      <td className="px-3 py-1.5 font-mono text-xs">{row.documentTypeCode || '—'}</td>
                      <td className="px-3 py-1.5">{row.documentTypeName}</td>
                      <td className="px-3 py-1.5 text-center">
                        <MappingActionButton
                          variant="add"
                          label={t('dlDocTypeAdd')}
                          disabled={saveMutation.isPending}
                          onClick={() => {
                            setPendingId(row.documentTypeId);
                            setConfirm({ type: 'add', name: rowLabel(row) });
                          }}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <MappingPagination
              page={availablePager.page}
              totalPages={availablePager.totalPages}
              total={availablePager.total}
              from={availablePager.from}
              to={availablePager.to}
              pageSize={availablePager.pageSize}
              onPageChange={availablePager.setPage}
              onPageSizeChange={availablePager.setPageSize}
              disabled={isLoading || saveMutation.isPending}
              labels={paginationLabels}
            />
          </MappingSection>
        </div>
      )}

      <MappingConfirmDialog
        confirm={confirm}
        t={t}
        isLoading={saveMutation.isPending}
        onConfirm={handleConfirm}
        onCancel={clearConfirm}
      />
    </div>
  );
}
