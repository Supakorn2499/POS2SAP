// src/pages/GlMappingPage.tsx
import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Save, Trash2, PlusCircle } from 'lucide-react';
import { toast } from 'sonner';
import glMappingService from '@/services/glMappingService';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { useLanguage } from '@/contexts/LanguageContext';
import type { PaytypeGlMappingDto, SapPayCategory, UpsertGlMappingDto } from '@/types/glMapping';
import { cn } from '@/lib/utils';

const CATEGORIES: SapPayCategory[] = ['CASH', 'TRANSFER', 'CREDIT_CARD', 'SKIP'];

const CATEGORY_BADGE: Record<SapPayCategory, string> = {
  CASH:        'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300',
  TRANSFER:    'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
  CREDIT_CARD: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300',
  SKIP:        'bg-gray-100 text-gray-500 dark:bg-gray-800/50 dark:text-gray-400',
};

type RowEdit = Omit<UpsertGlMappingDto, never>;

export default function GlMappingPage() {
  const { t } = useLanguage();
  const qc = useQueryClient();

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['glmapping'],
    queryFn: () => glMappingService.getAll(),
    staleTime: 30_000,
  });

  const { data: unmapped = [] } = useQuery({
    queryKey: ['glmapping-unmapped'],
    queryFn: () => glMappingService.getUnmapped(),
    staleTime: 30_000,
  });

  // Local edit state per row — keyed by payTypeID
  const [edits, setEdits] = useState<Record<number, Partial<RowEdit>>>({});
  const [deleteTarget, setDeleteTarget] = useState<PaytypeGlMappingDto | null>(null);

  const upsertMutation = useMutation({
    mutationFn: (dto: UpsertGlMappingDto) => glMappingService.upsert(dto),
    onSuccess: () => {
      toast.success(t('glMappingSaved'));
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
      setDeleteTarget(null);
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
      setDeleteTarget(null);
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

  function setField<K extends keyof RowEdit>(payTypeId: number, field: K, value: RowEdit[K]) {
    setEdits(prev => ({
      ...prev,
      [payTypeId]: { ...(prev[payTypeId] ?? {}), [field]: value },
    }));
  }

  function handleSave(row: PaytypeGlMappingDto) {
    upsertMutation.mutate(getEdit(row));
  }

  function handleAddUnmapped(payTypeId: number, payTypeName: string) {
    upsertMutation.mutate({
      payTypeID:      payTypeId,
      payTypeName,
      sapPayCategory: 'SKIP',
      sapGlAccount:   null,
      sapPayTypeName: null,
      isActive:       true,
      sortOrder:      99,
      remarks:        null,
    });
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-xl font-semibold">{t('glMappingTitle')}</h1>
        <p className="text-sm text-muted-foreground mt-1">{t('glMappingSubtitle')}</p>
      </div>

      {/* Legend */}
      <div className="flex flex-wrap gap-2 text-xs">
        {CATEGORIES.map(c => (
          <span key={c} className={cn('rounded-full px-2.5 py-0.5 font-medium', CATEGORY_BADGE[c])}>
            {c === 'CASH' && t('glMappingCatCash')}
            {c === 'TRANSFER' && t('glMappingCatTransfer')}
            {c === 'CREDIT_CARD' && t('glMappingCatCreditCard')}
            {c === 'SKIP' && t('glMappingCatSkip')}
          </span>
        ))}
      </div>

      {/* Mapping table */}
      <div className="rounded-lg border overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-xs text-muted-foreground uppercase">
            <tr>
              <th className="px-3 py-2 text-left w-16">{t('glMappingPayTypeID')}</th>
              <th className="px-3 py-2 text-left min-w-36">{t('glMappingPayTypeName')}</th>
              <th className="px-3 py-2 text-left w-36">{t('glMappingCategory')}</th>
              <th className="px-3 py-2 text-left min-w-32">{t('glMappingGlAccount')}</th>
              <th className="px-3 py-2 text-left min-w-36">{t('glMappingSapPayType')}</th>
              <th className="px-3 py-2 text-center w-16">{t('glMappingActive')}</th>
              <th className="px-3 py-2 text-left w-20">{t('glMappingSortOrder')}</th>
              <th className="px-3 py-2 text-left min-w-36">{t('glMappingRemarks')}</th>
              <th className="px-3 py-2 text-center w-24">{t('actions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {isLoading && (
              <tr>
                <td colSpan={9} className="px-3 py-8 text-center text-muted-foreground">{t('loading')}</td>
              </tr>
            )}
            {!isLoading && rows.length === 0 && (
              <tr>
                <td colSpan={9} className="px-3 py-8 text-center text-muted-foreground">{t('noData')}</td>
              </tr>
            )}
            {rows.map(row => {
              const e = getEdit(row);
              const isDirty = JSON.stringify(e) !== JSON.stringify({
                payTypeID: row.payTypeID, payTypeName: row.payTypeName,
                sapPayCategory: row.sapPayCategory, sapGlAccount: row.sapGlAccount,
                sapPayTypeName: row.sapPayTypeName, isActive: row.isActive,
                sortOrder: row.sortOrder, remarks: row.remarks,
              });
              return (
                <tr key={row.payTypeID} className={cn('hover:bg-muted/30 transition-colors', !e.isActive && 'opacity-50')}>
                  <td className="px-3 py-1.5 text-muted-foreground font-mono text-xs">{row.payTypeID}</td>
                  <td className="px-3 py-1.5 font-medium">{row.payTypeName}</td>

                  {/* Category dropdown */}
                  <td className="px-3 py-1.5">
                    <select
                      value={e.sapPayCategory}
                      onChange={ev => setField(row.payTypeID, 'sapPayCategory', ev.target.value as SapPayCategory)}
                      className={cn(
                        'rounded-full px-2.5 py-0.5 text-xs font-medium border-0 outline-none cursor-pointer',
                        CATEGORY_BADGE[e.sapPayCategory]
                      )}
                    >
                      {CATEGORIES.map(c => (
                        <option key={c} value={c}>{c}</option>
                      ))}
                    </select>
                  </td>

                  {/* GL Account */}
                  <td className="px-3 py-1.5">
                    <input
                      type="text"
                      value={e.sapGlAccount ?? ''}
                      onChange={ev => setField(row.payTypeID, 'sapGlAccount', ev.target.value || null)}
                      placeholder="GL Account..."
                      className="w-full rounded border border-input bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
                    />
                  </td>

                  {/* SAP Pay Type Name */}
                  <td className="px-3 py-1.5">
                    <input
                      type="text"
                      value={e.sapPayTypeName ?? ''}
                      onChange={ev => setField(row.payTypeID, 'sapPayTypeName', ev.target.value || null)}
                      placeholder={e.sapPayCategory === 'SKIP' ? '—' : 'SAP name...'}
                      disabled={e.sapPayCategory === 'SKIP'}
                      className="w-full rounded border border-input bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary disabled:opacity-40"
                    />
                  </td>

                  {/* Active toggle */}
                  <td className="px-3 py-1.5 text-center">
                    <input
                      type="checkbox"
                      checked={e.isActive}
                      onChange={ev => setField(row.payTypeID, 'isActive', ev.target.checked)}
                      className="h-4 w-4 cursor-pointer accent-primary"
                    />
                  </td>

                  {/* Sort Order */}
                  <td className="px-3 py-1.5">
                    <input
                      type="number"
                      value={e.sortOrder}
                      onChange={ev => setField(row.payTypeID, 'sortOrder', parseInt(ev.target.value) || 0)}
                      className="w-16 rounded border border-input bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
                    />
                  </td>

                  {/* Remarks */}
                  <td className="px-3 py-1.5">
                    <input
                      type="text"
                      value={e.remarks ?? ''}
                      onChange={ev => setField(row.payTypeID, 'remarks', ev.target.value || null)}
                      placeholder="Remarks..."
                      className="w-full rounded border border-input bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
                    />
                  </td>

                  {/* Actions */}
                  <td className="px-3 py-1.5">
                    <div className="flex items-center justify-center gap-1">
                      <button
                        onClick={() => handleSave(row)}
                        disabled={!isDirty || upsertMutation.isPending}
                        title={t('save')}
                        className="rounded p-1.5 text-primary hover:bg-primary/10 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                      >
                        <Save className="h-3.5 w-3.5" />
                      </button>
                      <button
                        onClick={() => setDeleteTarget(row)}
                        disabled={deleteMutation.isPending}
                        title={t('glMappingDelete')}
                        className="rounded p-1.5 text-destructive hover:bg-destructive/10 disabled:opacity-30 transition-colors"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Unmapped payment types */}
      {unmapped.length > 0 && (
        <div className="space-y-3">
          <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
            {t('glMappingUnmapped')} ({unmapped.length})
          </h2>
          <div className="rounded-lg border overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-muted/50 text-xs text-muted-foreground uppercase">
                <tr>
                  <th className="px-3 py-2 text-left w-16">{t('glMappingPayTypeID')}</th>
                  <th className="px-3 py-2 text-left">{t('glMappingPayTypeName')}</th>
                  <th className="px-3 py-2 text-center w-36">{t('glMappingAddRow')}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {unmapped.map(u => (
                  <tr key={u.payTypeID} className="hover:bg-muted/30">
                    <td className="px-3 py-1.5 text-muted-foreground font-mono text-xs">{u.payTypeID}</td>
                    <td className="px-3 py-1.5">{u.payTypeName}</td>
                    <td className="px-3 py-1.5 text-center">
                      <button
                        onClick={() => handleAddUnmapped(u.payTypeID, u.payTypeName)}
                        disabled={upsertMutation.isPending}
                        className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs bg-primary/10 text-primary hover:bg-primary/20 disabled:opacity-40 transition-colors"
                      >
                        <PlusCircle className="h-3.5 w-3.5" />
                        {t('glMappingAddRow')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Delete confirm dialog */}
      <ConfirmDialog
        isOpen={deleteTarget !== null}
        title={t('glMappingDelete')}
        message={t('glMappingDeleteConfirm').replace('{name}', deleteTarget?.payTypeName ?? '')}
        confirmText={t('glMappingDelete')}
        cancelText={t('detailBack')}
        isDangerous
        onConfirm={() => deleteTarget && deleteMutation.mutate(deleteTarget.payTypeID)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
