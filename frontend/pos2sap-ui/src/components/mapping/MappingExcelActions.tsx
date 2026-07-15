// src/components/mapping/MappingExcelActions.tsx
import { useRef, useState } from 'react';
import { Download, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { useLanguage } from '@/contexts/LanguageContext';

type Props = {
  onExport: () => void;
  /** Return parsed file text; caller does upsert/persist. */
  onImportText: (text: string) => Promise<void>;
  disabled?: boolean;
  exportDisabled?: boolean;
};

function isCsvFile(file: File): boolean {
  const name = file.name.toLowerCase();
  return name.endsWith('.csv') || file.type === 'text/csv' || file.type === 'application/vnd.ms-excel';
}

export function MappingExcelActions({
  onExport,
  onImportText,
  disabled = false,
  exportDisabled = false,
}: Props) {
  const { t } = useLanguage();
  const inputRef = useRef<HTMLInputElement>(null);
  const [importing, setImporting] = useState(false);

  async function handleFile(file: File | undefined) {
    if (!file) return;
    // Decision: CSV UTF-8 only (Excel opens export + Save As CSV). No .xlsx parser dependency.
    if (!isCsvFile(file)) {
      toast.error(t('importCsvOnly'));
      if (inputRef.current) inputRef.current.value = '';
      return;
    }
    setImporting(true);
    try {
      const text = await file.text();
      await onImportText(text);
    } catch (err: unknown) {
      console.error(err);
      toast.error(err instanceof Error ? err.message : t('importMappingInvalid'));
    } finally {
      setImporting(false);
      if (inputRef.current) inputRef.current.value = '';
    }
  }

  return (
    <div className="flex flex-col items-stretch gap-1 sm:items-end">
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={onExport}
          disabled={disabled || exportDisabled}
          className="inline-flex items-center gap-1.5 rounded-md border border-input bg-background px-3 py-2 text-sm font-medium hover:bg-muted disabled:opacity-40"
        >
          <Download className="h-4 w-4" />
          {t('exportToExcel')}
        </button>
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          disabled={disabled || importing}
          className="inline-flex items-center gap-1.5 rounded-md border border-input bg-background px-3 py-2 text-sm font-medium hover:bg-muted disabled:opacity-40"
        >
          <Upload className="h-4 w-4" />
          {importing ? t('importing') : t('importFromExcel')}
        </button>
        <input
          ref={inputRef}
          type="file"
          accept=".csv,text/csv"
          className="hidden"
          onChange={(e) => void handleFile(e.target.files?.[0])}
        />
      </div>
      <p className="text-[11px] text-muted-foreground">{t('importCsvHint')}</p>
    </div>
  );
}
