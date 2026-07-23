import type { LucideIcon, LucideProps } from 'lucide-react';
import { cn } from '@/lib/utils';

/** Premium defaults for Lucide — thin stroke, crisp at small sizes */
export const iconDefaults = {
  strokeWidth: 1.5,
  absoluteStrokeWidth: true,
} as const;

type AppIconProps = LucideProps & {
  icon: LucideIcon;
};

/** Shared icon renderer — use everywhere for consistent premium weight */
export function AppIcon({ icon: Icon, className, strokeWidth, absoluteStrokeWidth, ...rest }: AppIconProps) {
  return (
    <Icon
      className={cn('shrink-0', className)}
      strokeWidth={strokeWidth ?? iconDefaults.strokeWidth}
      absoluteStrokeWidth={absoluteStrokeWidth ?? iconDefaults.absoluteStrokeWidth}
      {...rest}
    />
  );
}
