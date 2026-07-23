// src/components/StatusBadge.tsx
import { Clock, Loader2, CheckCircle2, XCircle, RotateCw } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import { AppIcon } from '@/components/ui/AppIcon';
import { useLanguage } from '@/contexts/LanguageContext';
import type { InterfaceStatus } from '@/types/monitor';

const statusConfig: Record<InterfaceStatus, { labelKey: string; className: string; icon: LucideIcon }> = {
  PENDING: {
    labelKey: 'statusLabel.PENDING',
    className: 'bg-muted text-muted-foreground border-border',
    icon: Clock,
  },
  PROCESSING: {
    labelKey: 'statusLabel.PROCESSING',
    className: 'bg-muted text-sky-700 border-border dark:text-sky-400',
    icon: Loader2,
  },
  SUCCESS: {
    labelKey: 'statusLabel.SUCCESS',
    className: 'bg-muted text-emerald-700 border-border dark:text-emerald-400',
    icon: CheckCircle2,
  },
  FAILED: {
    labelKey: 'statusLabel.FAILED',
    className: 'bg-muted text-rose-700 border-border dark:text-rose-400',
    icon: XCircle,
  },
  RETRY: {
    labelKey: 'statusLabel.RETRY',
    className: 'bg-muted text-orange-700 border-border dark:text-orange-400',
    icon: RotateCw,
  },
};

interface Props {
  status: InterfaceStatus | string;
  size?: 'sm' | 'md';
}

export function StatusBadge({ status, size = 'md' }: Props) {
  const { t } = useLanguage();
  const baseStatus = (status as string).split(' ')[0].toUpperCase() as InterfaceStatus;
  const config = statusConfig[baseStatus];
  const label = config ? t(config.labelKey) : String(status);

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border font-medium tracking-tight',
        size === 'sm' ? 'px-2 py-0.5 text-[11px]' : 'px-2.5 py-0.5 text-xs',
        config?.className ?? 'bg-muted text-muted-foreground border-border'
      )}
    >
      {config && (
        <AppIcon
          icon={config.icon}
          className={cn(
            size === 'sm' ? 'h-3 w-3' : 'h-3.5 w-3.5',
            baseStatus === 'PROCESSING' && 'animate-spin'
          )}
        />
      )}
      {label}
    </span>
  );
}
