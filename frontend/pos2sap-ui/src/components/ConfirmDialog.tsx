import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { AlertCircle, Check, X } from 'lucide-react';

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
  confirmText = 'ยืนยัน',
  cancelText = 'ยกเลิก',
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
        className="relative z-10 w-full max-w-md overflow-hidden rounded-xl bg-white shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div
          className={`flex items-start gap-4 p-6 ${
            isDangerous ? 'bg-red-50' : 'bg-blue-50'
          }`}
        >
          <div
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${
              isDangerous ? 'bg-red-100' : 'bg-blue-100'
            }`}
          >
            <AlertCircle
              className={`h-6 w-6 ${isDangerous ? 'text-red-600' : 'text-blue-600'}`}
            />
          </div>
          <div className="flex-1 min-w-0">
            <h3
              id="confirm-dialog-title"
              className={`text-lg font-semibold ${
                isDangerous ? 'text-red-900' : 'text-blue-900'
              }`}
            >
              {title}
            </h3>
          </div>
        </div>

        <div className="px-6 py-4">
          <p className="text-sm leading-relaxed text-gray-700">{message}</p>
        </div>

        <div className="flex justify-end gap-3 border-t bg-gray-50 px-6 py-4">
          <button
            type="button"
            onClick={onCancel}
            disabled={isLoading}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <X className="h-4 w-4" />
            {cancelText}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isLoading}
            className={`inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50 ${
              isDangerous
                ? 'bg-red-600 hover:bg-red-700'
                : 'bg-blue-600 hover:bg-blue-700'
            }`}
          >
            {isLoading ? (
              <>
                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                ...
              </>
            ) : (
              <>
                <Check className="h-4 w-4" />
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
