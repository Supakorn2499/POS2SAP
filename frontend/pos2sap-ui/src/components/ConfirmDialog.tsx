import React from 'react';
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
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onCancel}
      />

      {/* Dialog */}
      <div className="relative z-10 bg-white rounded-lg shadow-xl max-w-sm w-full mx-4 overflow-hidden animate-in fade-in zoom-in-95">
        {/* Header */}
        <div className={`flex items-start gap-4 p-6 ${isDangerous ? 'bg-red-50' : 'bg-blue-50'}`}>
          <div className={`flex-shrink-0 h-10 w-10 rounded-full flex items-center justify-center ${
            isDangerous ? 'bg-red-100' : 'bg-blue-100'
          }`}>
            <AlertCircle className={`h-6 w-6 ${isDangerous ? 'text-red-600' : 'text-blue-600'}`} />
          </div>
          <div className="flex-1">
            <h3 className={`text-lg font-semibold ${isDangerous ? 'text-red-900' : 'text-blue-900'}`}>
              {title}
            </h3>
          </div>
        </div>

        {/* Content */}
        <div className="px-6 py-4">
          <p className="text-gray-700 text-sm leading-relaxed">
            {message}
          </p>
        </div>

        {/* Footer */}
        <div className="bg-gray-50 px-6 py-4 flex gap-3 justify-end border-t">
          <button
            onClick={onCancel}
            disabled={isLoading}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            <X className="h-4 w-4 inline mr-2" />
            {cancelText}
          </button>
          <button
            onClick={onConfirm}
            disabled={isLoading}
            className={`px-4 py-2 text-sm font-medium text-white rounded-lg flex items-center gap-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${
              isDangerous
                ? 'bg-red-600 hover:bg-red-700'
                : 'bg-blue-600 hover:bg-blue-700'
            }`}
          >
            {isLoading ? (
              <>
                <div className="h-4 w-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                กำลังประมวลผล...
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
    </div>
  );
}
