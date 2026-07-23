// src/components/JsonViewer.tsx
import { useMemo, useState } from 'react';
import { Check, Copy } from 'lucide-react';
import { toast } from 'sonner';
import { useLanguage } from '@/contexts/LanguageContext';
import { AppIcon } from '@/components/ui/AppIcon';

interface Props {
  value?: string | null;
  title?: string;
}

export function JsonViewer({ value, title }: Props) {
  const { t } = useLanguage();
  const [copied, setCopied] = useState(false);

  const formatted = useMemo(() => {
    if (!value) return '';
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }, [value]);

  async function handleCopy() {
    if (!formatted) return;
    try {
      await navigator.clipboard.writeText(formatted);
      setCopied(true);
      toast.success(t('copyJsonSuccess'));
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      toast.error(t('copyJsonError'));
    }
  }

  if (!value) {
    return <p className="text-sm italic text-muted-foreground">{t('noData')}</p>;
  }

  return (
    <div>
      <div className="mb-1 flex items-center justify-between gap-2">
        {title ? (
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{title}</p>
        ) : (
          <span />
        )}
        <button
          type="button"
          onClick={() => void handleCopy()}
          className="app-icon-well h-8 w-8 transition hover:border-primary/30 hover:text-primary"
          title={t('copyJson')}
          aria-label={t('copyJson')}
        >
          <AppIcon icon={copied ? Check : Copy} className="h-3.5 w-3.5" />
        </button>
      </div>
      <pre className="max-h-96 overflow-auto whitespace-pre-wrap break-words rounded-md bg-muted p-3 text-xs leading-relaxed">
        {formatted}
      </pre>
    </div>
  );
}
