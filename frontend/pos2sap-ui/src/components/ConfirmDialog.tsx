import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { AlertCircle, Check, X } from 'lucide-react';
import { AppIcon } from '@/components/ui/AppIcon';
import { cn } from '@/lib/utils';

interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  isDangerous?: boolean;
  isLoading?: boolean;
  onConfirm: () => void | Promise<void>;
  onCancel: () => void;
}

export function ConfirmDialog({
  isOpen,
  title,
  message,
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  isDangerous = false,
  isLoading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  useEffect(() => {
    if (!isOpen) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = prev;
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-[9999] flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
    >
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={isLoading ? undefined : onCancel}
        aria-hidden="true"
      />

      <div
        className="relative z-10 w-full max-w-md overflow-hidden rounded-2xl border border-border/80 bg-card shadow-2xl shadow-black/10"
        onClick={(e) => e.stopPropagation()}
      >
        <div
          className={cn(
            'flex items-start gap-4 p-6',
            isDangerous ? 'bg-rose-50 dark:bg-rose-950/40' : 'bg-sky-50 dark:bg-sky-950/40'
          )}
        >
          <div
            className={cn(
              'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border shadow-sm',
              isDangerous
                ? 'border-rose-200/80 bg-rose-100 text-rose-700 dark:border-rose-500/30 dark:bg-rose-900/50 dark:text-rose-200'
                : 'border-sky-200/80 bg-sky-100 text-sky-700 dark:border-sky-500/30 dark:bg-sky-900/50 dark:text-sky-200'
            )}
          >
            <AppIcon icon={AlertCircle} className="h-5 w-5" />
          </div>
          <div className="min-w-0 flex-1">
            <h3
              id="confirm-dialog-title"
              className={cn(
                'text-lg font-semibold tracking-tight',
                isDangerous
                  ? 'text-rose-900 dark:text-rose-100'
                  : 'text-sky-900 dark:text-sky-100'
              )}
            >
              {title}
            </h3>
          </div>
        </div>

        <div className="px-6 py-4">
          <p className="text-sm leading-relaxed text-muted-foreground">{message}</p>
        </div>

        <div className="flex justify-end gap-3 border-t border-border/80 bg-muted/30 px-6 py-4">
          <button
            type="button"
            onClick={onCancel}
            disabled={isLoading}
            className="inline-flex items-center gap-2 rounded-xl border border-input bg-background px-4 py-2 text-sm font-medium text-foreground transition hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
          >
            <AppIcon icon={X} className="h-4 w-4" />
            {cancelText}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isLoading}
            className={cn(
              'inline-flex items-center gap-2 rounded-xl px-4 py-2 text-sm font-medium text-white shadow-sm transition disabled:cursor-not-allowed disabled:opacity-50',
              isDangerous ? 'bg-rose-600 hover:bg-rose-700' : 'bg-primary hover:bg-primary/90'
            )}
          >
            {isLoading ? (
              <>
                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                ...
              </>
            ) : (
              <>
                <AppIcon icon={Check} className="h-4 w-4" />
                {confirmText}
              </>
            )}
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
