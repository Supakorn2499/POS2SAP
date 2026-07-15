// src/components/StatCard.tsx
import { cn } from '@/lib/utils';
import { useTheme } from '@/contexts/ThemeContext';
import type { LucideIcon } from 'lucide-react';

interface Props {
  title: string;
  value: number | string;
  icon: LucideIcon;
  variant?: 'default' | 'success' | 'danger' | 'warning' | 'info' | 'orange';
  loading?: boolean;
}

type Variant = NonNullable<Props['variant']>;

/** Soft pastel — for light mode only */
const lightCard: Record<Variant, string> = {
  default: 'border-gray-200 bg-white',
  success: 'border-emerald-200 bg-emerald-50',
  danger:  'border-rose-200 bg-rose-50',
  warning: 'border-amber-200 bg-amber-50',
  info:    'border-sky-200 bg-sky-50',
  orange:  'border-orange-200 bg-orange-50',
};

const lightIcon: Record<Variant, string> = {
  default: 'bg-gray-100 text-gray-600',
  success: 'bg-emerald-100 text-emerald-600',
  danger:  'bg-rose-100 text-rose-600',
  warning: 'bg-amber-100 text-amber-600',
  info:    'bg-sky-100 text-sky-600',
  orange:  'bg-orange-100 text-orange-600',
};

const lightValue: Record<Variant, string> = {
  default: 'text-slate-900',
  success: 'text-emerald-800',
  danger:  'text-rose-800',
  warning: 'text-amber-800',
  info:    'text-sky-800',
  orange:  'text-orange-800',
};

/** Richer tinted cards — for dark mode only */
const darkCard: Record<Variant, string> = {
  default: 'border-slate-600 bg-slate-800/90',
  success: 'border-emerald-500/40 bg-emerald-950/80',
  danger:  'border-rose-500/40 bg-rose-950/80',
  warning: 'border-amber-500/40 bg-amber-950/80',
  info:    'border-sky-500/40 bg-sky-950/80',
  orange:  'border-orange-500/40 bg-orange-950/80',
};

const darkIcon: Record<Variant, string> = {
  default: 'bg-slate-700 text-slate-200',
  success: 'bg-emerald-500 text-white',
  danger:  'bg-rose-500 text-white',
  warning: 'bg-amber-500 text-white',
  info:    'bg-sky-500 text-white',
  orange:  'bg-orange-500 text-white',
};

const darkValue: Record<Variant, string> = {
  default: 'text-slate-100',
  success: 'text-emerald-200',
  danger:  'text-rose-200',
  warning: 'text-amber-200',
  info:    'text-sky-200',
  orange:  'text-orange-200',
};

export function StatCard({ title, value, icon: Icon, variant = 'default', loading = false }: Props) {
  // ponytail: bind to ThemeContext so OS dark preference can't override app toggle
  const { theme } = useTheme();
  const isDark = theme === 'dark';

  return (
    <div className={cn('rounded-xl border p-4 shadow-sm', isDark ? darkCard[variant] : lightCard[variant])}>
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">{title}</p>
        <div className={cn('rounded-lg p-2', isDark ? darkIcon[variant] : lightIcon[variant])}>
          <Icon className="h-4 w-4" />
        </div>
      </div>
      <p
        className={cn(
          'mt-2 text-2xl font-bold',
          loading ? 'text-muted-foreground' : isDark ? darkValue[variant] : lightValue[variant]
        )}
      >
        {loading ? '...' : value.toLocaleString('th-TH')}
      </p>
    </div>
  );
}
