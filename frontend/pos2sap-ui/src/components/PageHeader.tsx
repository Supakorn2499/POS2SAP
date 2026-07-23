import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { AppIcon } from '@/components/ui/AppIcon';
import { cn } from '@/lib/utils';

type PageHeaderProps = {
  icon: LucideIcon;
  title: string;
  subtitle?: string;
  actions?: ReactNode;
  className?: string;
};

/** Shared page title + icon well (matches mapping / GL / shop headers) */
export function PageHeader({ icon: Icon, title, subtitle, actions, className }: PageHeaderProps) {
  return (
    <div className={cn('flex flex-wrap items-start justify-between gap-3', className)}>
      <div className="flex min-w-0 items-start gap-3">
        <div className="app-icon-well h-10 w-10 text-primary shadow-sm">
          <AppIcon icon={Icon} className="h-5 w-5" />
        </div>
        <div className="min-w-0">
          <h1 className="text-xl font-semibold tracking-tight text-foreground">{title}</h1>
          {subtitle && (
            <p className="mt-1 text-sm text-muted-foreground">{subtitle}</p>
          )}
        </div>
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
