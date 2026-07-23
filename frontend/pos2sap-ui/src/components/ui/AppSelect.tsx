import { ChevronDown, type LucideIcon } from 'lucide-react';
import type { SelectHTMLAttributes, ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { AppIcon } from '@/components/ui/AppIcon';

type AppSelectProps = SelectHTMLAttributes<HTMLSelectElement> & {
  /** Optional leading icon inside the control */
  icon?: LucideIcon;
  wrapperClassName?: string;
  children: ReactNode;
};

/** Premium native select — custom chevron, shared height/radius with date/input */
export function AppSelect({
  className,
  wrapperClassName,
  icon: Icon,
  children,
  disabled,
  ...rest
}: AppSelectProps) {
  return (
    <div className={cn('relative min-w-0', wrapperClassName)}>
      {Icon && (
        <span className="pointer-events-none absolute left-3 top-1/2 z-[1] -translate-y-1/2 text-muted-foreground">
          <AppIcon icon={Icon} className="h-4 w-4" />
        </span>
      )}
      <select
        disabled={disabled}
        className={cn(
          'app-select',
          Icon && 'pl-9',
          className
        )}
        {...rest}
      >
        {children}
      </select>
      <span className="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground">
        <AppIcon icon={ChevronDown} className="h-4 w-4 opacity-70" />
      </span>
    </div>
  );
}
