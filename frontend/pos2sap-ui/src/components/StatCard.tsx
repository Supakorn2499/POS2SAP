// src/components/StatCard.tsx
import { cn } from '@/lib/utils';
import type { LucideIcon } from 'lucide-react';

interface Props {
  title: string;
  value: number | string;
  icon: LucideIcon;
  variant?: 'default' | 'success' | 'danger' | 'warning' | 'info' | 'orange';
  loading?: boolean;
}

const variantClass: Record<NonNullable<Props['variant']>, string> = {
  default: 'text-gray-600 bg-gray-50',
  success: 'text-green-600 bg-green-50',
  danger:  'text-red-600 bg-red-50',
  warning: 'text-yellow-600 bg-yellow-50',
  info:    'text-blue-600 bg-blue-50',
  orange:  'text-orange-600 bg-orange-50',
};

const cardVariantClass: Record<NonNullable<Props['variant']>, string> = {
  default: 'border-gray-200 bg-white',
  success: 'border-emerald-100 bg-emerald-50/60',
  danger:  'border-red-100 bg-red-50/60',
  warning: 'border-amber-100 bg-amber-50/60',
  info:    'border-sky-100 bg-sky-50/60',
  orange:  'border-orange-100 bg-orange-50/60',
};

export function StatCard({ title, value, icon: Icon, variant = 'default', loading = false }: Props) {
  return (
    <div className={cn('rounded-xl border p-4 shadow-sm', cardVariantClass[variant])}>
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">{title}</p>
        <div className={cn('rounded-lg p-2', variantClass[variant])}>
          <Icon className="h-4 w-4" />
        </div>
      </div>
      <p className={cn('mt-2 text-2xl font-bold', loading && 'text-muted-foreground')}>
        {loading ? '...' : value.toLocaleString('th-TH')}
      </p>
    </div>
  );
}
