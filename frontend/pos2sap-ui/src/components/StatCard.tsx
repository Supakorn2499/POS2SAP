// src/components/StatCard.tsx
import { cn } from '@/lib/utils';
import { AppIcon } from '@/components/ui/AppIcon';
import type { LucideIcon } from 'lucide-react';

interface Props {
  title: string;
  value: number | string;
  icon: LucideIcon;
  variant?: 'default' | 'success' | 'danger' | 'warning' | 'info' | 'orange';
  loading?: boolean;
}

type Variant = NonNullable<Props['variant']>;

/** Quiet accent — icon well only; card stays neutral in both themes */
const iconWell: Record<Variant, string> = {
  default: 'bg-muted text-muted-foreground',
  success: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-400',
  danger:  'bg-rose-500/10 text-rose-700 dark:text-rose-400',
  warning: 'bg-amber-500/10 text-amber-700 dark:text-amber-400',
  info:    'bg-sky-500/10 text-sky-700 dark:text-sky-400',
  orange:  'bg-orange-500/10 text-orange-700 dark:text-orange-400',
};

const valueTone: Record<Variant, string> = {
  default: 'text-foreground',
  success: 'text-emerald-700 dark:text-emerald-400',
  danger:  'text-rose-700 dark:text-rose-400',
  warning: 'text-amber-700 dark:text-amber-400',
  info:    'text-sky-700 dark:text-sky-400',
  orange:  'text-orange-700 dark:text-orange-400',
};

export function StatCard({ title, value, icon: Icon, variant = 'default', loading = false }: Props) {
  return (
    <div className="rounded-2xl border border-border bg-card p-4 text-card-foreground shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{title}</p>
        <div className={cn('rounded-lg p-2', iconWell[variant])}>
          <AppIcon icon={Icon} className="h-4 w-4" />
        </div>
      </div>
      <p
        className={cn(
          'mt-2.5 text-2xl font-semibold tracking-tight tabular-nums',
          loading ? 'text-muted-foreground' : valueTone[variant]
        )}
      >
        {loading ? '...' : value.toLocaleString('th-TH')}
      </p>
    </div>
  );
}
