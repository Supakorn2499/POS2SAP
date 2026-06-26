import { ConfirmDialog } from '@/components/ConfirmDialog';

export type MappingConfirmState =
  | { type: 'save' }
  | { type: 'discard' }
  | { type: 'add'; name: string }
  | { type: 'delete'; name: string }
  | { type: 'remove'; name: string }
  | null;

type TFn = (key: string, params?: Record<string, string | number>) => string;

export function resolveMappingConfirm(
  confirm: MappingConfirmState,
  t: TFn,
  options?: {
    saveCount?: number;
    saveMessage?: string;
  }
) {
  if (!confirm) {
    return {
      isOpen: false,
      title: '',
      message: '',
      confirmText: t('mappingConfirmYes'),
      isDangerous: false,
    };
  }

  const isDangerous =
    confirm.type === 'delete' ||
    confirm.type === 'discard' ||
    confirm.type === 'remove';

  let title = '';
  let message = '';
  let confirmText = t('mappingConfirmYes');

  switch (confirm.type) {
    case 'save':
      title = t('mappingConfirmSaveTitle');
      message =
        options?.saveMessage ??
        t('mappingConfirmSaveMessage', { count: options?.saveCount ?? 0 });
      break;
    case 'discard':
      title = t('mappingConfirmDiscardTitle');
      message = t('mappingConfirmDiscardMessage');
      confirmText = t('mappingConfirmDiscardYes');
      break;
    case 'add':
      title = t('mappingConfirmAddTitle');
      message = t('mappingConfirmAddMessage', { name: confirm.name });
      break;
    case 'delete':
      title = t('mappingConfirmDeleteTitle');
      message = t('mappingConfirmDeleteMessage', { name: confirm.name });
      confirmText = t('mappingConfirmDeleteYes');
      break;
    case 'remove':
      title = t('mappingConfirmRemoveTitle');
      message = t('mappingConfirmRemoveMessage', { name: confirm.name });
      confirmText = t('mappingConfirmRemoveYes');
      break;
  }

  return { isOpen: true, title, message, confirmText, isDangerous };
}

interface MappingConfirmDialogProps {
  confirm: MappingConfirmState;
  t: TFn;
  isLoading?: boolean;
  saveCount?: number;
  saveMessage?: string;
  onConfirm: () => void;
  onCancel: () => void;
}

export function MappingConfirmDialog({
  confirm,
  t,
  isLoading = false,
  saveCount,
  saveMessage,
  onConfirm,
  onCancel,
}: MappingConfirmDialogProps) {
  const props = resolveMappingConfirm(confirm, t, { saveCount, saveMessage });

  return (
    <ConfirmDialog
      isOpen={props.isOpen}
      title={props.title}
      message={props.message}
      confirmText={props.confirmText}
      cancelText={t('cancel')}
      isDangerous={props.isDangerous}
      isLoading={isLoading}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  );
}
