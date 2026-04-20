// src/components/JsonViewer.tsx

import { useLanguage } from '@/contexts/LanguageContext';

interface Props {
  value?: string | null;
  title?: string;
}

export function JsonViewer({ value, title }: Props) {
  const { t } = useLanguage();

  if (!value) {
    return <p className="text-sm text-muted-foreground italic">{t('noData')}</p>;
  }

  let formatted = value;
  try {
    formatted = JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    // not JSON, show as-is
  }

  return (
    <div>
      {title && <p className="mb-1 text-xs font-medium text-muted-foreground uppercase tracking-wide">{title}</p>}
      <pre className="overflow-auto rounded-md bg-muted p-3 text-xs leading-relaxed max-h-96 whitespace-pre-wrap break-words">
        {formatted}
      </pre>
    </div>
  );
}
