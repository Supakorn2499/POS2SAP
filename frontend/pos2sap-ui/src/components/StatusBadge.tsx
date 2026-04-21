// src/components/StatusBadge.tsx
import { cn } from '@/lib/utils';
import { useLanguage } from '@/contexts/LanguageContext';
import type { InterfaceStatus } from '@/types/monitor';

const statusConfig: Record<InterfaceStatus, { labelKey: string; className: string }> = {
  PENDING:    { labelKey: 'statusLabel.PENDING',    className: 'bg-yellow-100 text-yellow-800 border-yellow-300' },
  PROCESSING: { labelKey: 'statusLabel.PROCESSING', className: 'bg-blue-100 text-blue-800 border-blue-300' },
  SUCCESS:    { labelKey: 'statusLabel.SUCCESS',    className: 'bg-green-100 text-green-800 border-green-300' },
  FAILED:     { labelKey: 'statusLabel.FAILED',     className: 'bg-red-100 text-red-800 border-red-300' },
  RETRY:      { labelKey: 'statusLabel.RETRY',      className: 'bg-orange-100 text-orange-800 border-orange-300' },
};

interface Props {
  status: InterfaceStatus | string;
  size?: 'sm' | 'md';
}

export function StatusBadge({ status, size = 'md' }: Props) {
  const { t } = useLanguage();
  // Extract base status (handle cases with extra text after space)
  const baseStatus = (status as string).split(' ')[0].toUpperCase() as InterfaceStatus;
  const config = statusConfig[baseStatus];
  const label = config ? t(config.labelKey) : String(status);

  return (
    <span className={cn(
      'inline-flex items-center rounded-full border font-medium',
      size === 'sm' ? 'px-2 py-0.5 text-xs' : 'px-2.5 py-0.5 text-sm',
      config?.className ?? 'bg-gray-100 text-gray-700 border-gray-300'
    )}>
      {label}
    </span>
  );
}
