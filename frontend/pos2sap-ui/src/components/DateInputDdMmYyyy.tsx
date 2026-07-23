import { useEffect, useRef, useState } from 'react';
import { Calendar } from 'lucide-react';
import { cn, ddMmYyyyToIso, isoToDdMmYyyy } from '@/lib/utils';
import { AppIcon } from '@/components/ui/AppIcon';
import { useLanguage } from '@/contexts/LanguageContext';

type Props = {
  value: string; // yyyy-MM-dd
  onChange: (iso: string) => void;
  min?: string; // yyyy-MM-dd
  disabled?: boolean;
  className?: string;
  inputClassName?: string;
};

/** Date input that displays/edits `dd/mm/yyyy` while keeping ISO `yyyy-MM-dd` value. */
export function DateInputDdMmYyyy({ value, onChange, min, disabled, className, inputClassName }: Props) {
  const { t } = useLanguage();
  const [text, setText] = useState(() => isoToDdMmYyyy(value));
  const pickerRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setText(isoToDdMmYyyy(value));
  }, [value]);

  function commit(raw: string) {
    if (!raw.trim()) {
      onChange('');
      setText('');
      return;
    }
    const iso = ddMmYyyyToIso(raw);
    if (!iso) {
      setText(isoToDdMmYyyy(value));
      return;
    }
    if (min && iso < min) {
      setText(isoToDdMmYyyy(value));
      return;
    }
    onChange(iso);
    setText(isoToDdMmYyyy(iso));
  }

  function openPicker() {
    const el = pickerRef.current;
    if (!el || disabled) return;
    try {
      el.showPicker();
    } catch {
      el.focus();
      el.click();
    }
  }

  return (
    <div className={cn('relative min-w-0', className)}>
      <input
        type="text"
        inputMode="numeric"
        placeholder="dd/mm/yyyy"
        value={text}
        disabled={disabled}
        onChange={(e) => setText(e.target.value)}
        onBlur={(e) => commit(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') e.currentTarget.blur();
        }}
        className={cn('app-date', inputClassName)}
      />
      <button
        type="button"
        disabled={disabled}
        onClick={openPicker}
        aria-label={t('pickDate')}
        title={t('pickDate')}
        className="absolute inset-y-0.5 right-0.5 inline-flex w-9 items-center justify-center rounded-lg text-primary/80 transition hover:bg-primary/10 hover:text-primary disabled:opacity-50"
      >
        <AppIcon icon={Calendar} className="h-4 w-4" />
      </button>
      <input
        ref={pickerRef}
        type="date"
        value={value || ''}
        min={min}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        tabIndex={-1}
        aria-hidden
        className="pointer-events-none absolute h-0 w-0 overflow-hidden opacity-0"
      />
    </div>
  );
}
