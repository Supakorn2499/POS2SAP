import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { Save, Search } from 'lucide-react';
import { cn } from '@/lib/utils';
import { AppIcon } from '@/components/ui/AppIcon';
import { PageHeader } from '@/components/PageHeader';

export const mappingTableHeadClass =
  'bg-muted/50 text-xs font-semibold text-muted-foreground uppercase';
export const mappingTableClass = 'w-full text-sm';
export const mappingInputClass =
  'app-control h-9 rounded-lg px-2 py-1.5 text-sm';

interface MappingPageHeaderProps {
  icon: LucideIcon;
  title: string;
  subtitle: string;
}

export function MappingPageHeader({ icon, title, subtitle }: MappingPageHeaderProps) {
  return <PageHeader icon={icon} title={title} subtitle={subtitle} />;
}

export interface MappingStatItem {
  label: string;
  value: number;
  warn?: boolean;
  accent?: 'green' | 'muted';
}

export function MappingStatGrid({ items }: { items: MappingStatItem[] }) {
  return (
    <div className={cn(
      'grid gap-3',
      items.length <= 3 ? 'grid-cols-1 sm:grid-cols-3' : 'grid-cols-2 xl:grid-cols-4'
    )}>
      {items.map((s) => (
        <div
          key={s.label}
          className={cn(
            'rounded-2xl border bg-card px-4 py-3 shadow-sm',
            s.warn && 'border-amber-300 bg-amber-50/50 dark:border-amber-500/40 dark:bg-amber-950/50'
          )}
        >
          <p className="text-xs font-medium text-muted-foreground">{s.label}</p>
          <p
            className={cn(
              'mt-1 text-2xl font-semibold tracking-tight tabular-nums',
              s.warn && 'text-amber-700 dark:text-amber-300',
              s.accent === 'green' && 'text-green-700 dark:text-emerald-300',
              s.accent === 'muted' && 'text-muted-foreground'
            )}
          >
            {s.value}
          </p>
        </div>
      ))}
    </div>
  );
}

interface MappingToolbarProps {
  search: string;
  onSearchChange: (v: string) => void;
  searchPlaceholder: string;
  filter?: ReactNode;
  actions?: ReactNode;
  showClear?: boolean;
  onClear?: () => void;
  clearLabel?: string;
}

export function MappingToolbar({
  search,
  onSearchChange,
  searchPlaceholder,
  filter,
  actions,
  showClear,
  onClear,
  clearLabel,
}: MappingToolbarProps) {
  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-center xl:grid-cols-[minmax(14rem,1fr)_auto_auto] xl:gap-3">
      <div className="relative min-w-0">
        <AppIcon icon={Search} className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <input
          type="search"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder={searchPlaceholder}
          className="app-control pl-9"
        />
      </div>
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        {filter}
        {showClear && onClear && (
          <button
            type="button"
            onClick={onClear}
            className="text-sm text-muted-foreground underline hover:text-foreground"
          >
            {clearLabel}
          </button>
        )}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2 md:justify-end">{actions}</div>}
    </div>
  );
}

interface MappingSectionProps {
  variant: 'mapped' | 'available';
  title: string;
  hint?: string;
  count?: number;
  isEmpty?: boolean;
  emptyMessage?: string;
  children: ReactNode;
}

export function MappingSection({
  variant,
  title,
  hint,
  count,
  isEmpty,
  emptyMessage,
  children,
}: MappingSectionProps) {
  const isMapped = variant === 'mapped';

  return (
    <section className="overflow-hidden rounded-xl border bg-card">
      <div
        className={cn(
          'border-b px-4 py-3',
          isMapped ? 'bg-emerald-50/80 dark:bg-emerald-950/30' : 'bg-muted/40'
        )}
      >
        <h2
          className={cn(
            'text-sm font-semibold',
            isMapped ? 'text-emerald-900 dark:text-emerald-100' : 'text-foreground'
          )}
        >
          {title}
          {count !== undefined ? ` (${count})` : ''}
        </h2>
        {hint && (
          <p
            className={cn(
              'mt-0.5 text-xs',
              isMapped ? 'text-emerald-800/80 dark:text-emerald-200/70' : 'text-muted-foreground'
            )}
          >
            {hint}
          </p>
        )}
      </div>
      {isEmpty ? (
        <p className="p-6 text-sm text-muted-foreground">{emptyMessage}</p>
      ) : (
        children
      )}
    </section>
  );
}

interface MappingPaginationProps {
  page: number;
  totalPages: number;
  total: number;
  from: number;
  to: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  disabled?: boolean;
  labels: {
    previous: string;
    next: string;
    pageOf: (page: number, total: number) => string;
    rowsPerPage: string;
    showing: (from: number, to: number, total: number) => string;
  };
}

export function MappingPagination({
  page,
  totalPages,
  total,
  from,
  to,
  pageSize,
  onPageChange,
  onPageSizeChange,
  disabled,
  labels,
}: MappingPaginationProps) {
  if (total === 0) return null;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t bg-muted/20 px-4 py-3">
      <p className="text-xs text-muted-foreground">
        {labels.showing(from, to, total)}
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>{labels.rowsPerPage}</span>
          <select
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            disabled={disabled}
            className="rounded-md border bg-background px-2 py-1 text-xs"
          >
            {[10, 20, 50, 100].map((n) => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>
        </label>
        {totalPages > 1 && (
          <div className="flex items-center gap-1.5">
            <button
              type="button"
              onClick={() => onPageChange(Math.max(1, page - 1))}
              disabled={disabled || page <= 1}
              className="rounded border bg-background px-2.5 py-1 text-xs hover:bg-muted disabled:opacity-40"
            >
              {labels.previous}
            </button>
            <span className="px-1 text-xs text-muted-foreground">
              {labels.pageOf(page, totalPages)}
            </span>
            <button
              type="button"
              onClick={() => onPageChange(Math.min(totalPages, page + 1))}
              disabled={disabled || page >= totalPages}
              className="rounded border bg-background px-2.5 py-1 text-xs hover:bg-muted disabled:opacity-40"
            >
              {labels.next}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export function mappingPaginationLabels(
  t: (key: string, params?: Record<string, string | number>) => string,
): MappingPaginationProps['labels'] {
  return {
    previous: t('previous'),
    next: t('next'),
    pageOf: (page, total) => t('pageOf', { page, total }),
    rowsPerPage: t('mappingRowsPerPage'),
    showing: (from, to, total) => t('mappingShowingRange', { from, to, total }),
  };
}

interface MappingUnsavedBarProps {
  visible: boolean;
  message: string;
  discardLabel: string;
  saveLabel: string;
  saving?: boolean;
  onDiscard: () => void;
  onSave: () => void;
}

export function MappingUnsavedBar({
  visible,
  message,
  discardLabel,
  saveLabel,
  saving,
  onDiscard,
  onSave,
}: MappingUnsavedBarProps) {
  if (!visible) return null;

  return (
    <div className="fixed bottom-0 right-0 z-40 border-t bg-background/95 px-4 py-4 shadow-lg backdrop-blur md:px-6 left-[var(--app-sidebar-w,0px)]">
      <div className="mx-auto flex max-w-5xl flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm font-medium text-amber-800 dark:text-amber-200">{message}</p>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={onDiscard}
            disabled={saving}
            className="rounded-lg border px-4 py-2 text-sm font-medium hover:bg-muted disabled:opacity-50"
          >
            {discardLabel}
          </button>
          <button
            type="button"
            onClick={onSave}
            disabled={saving}
            className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
          >
            <AppIcon icon={Save} className="h-4 w-4" />
            {saveLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

export function MappingActionButton({
  variant,
  label,
  onClick,
  disabled,
}: {
  variant: 'add' | 'remove' | 'delete';
  label: string;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition-colors disabled:opacity-40',
        variant === 'add' && 'bg-primary/10 text-primary hover:bg-primary/20',
        variant === 'remove' && 'bg-red-50 text-red-700 hover:bg-red-100',
        variant === 'delete' && 'bg-red-50 text-red-700 hover:bg-red-100'
      )}
    >
      {label}
    </button>
  );
}
