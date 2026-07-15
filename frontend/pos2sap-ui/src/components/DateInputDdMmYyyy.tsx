import { useEffect, useRef, useState } from 'react';
import { Calendar } from 'lucide-react';
import { cn, ddMmYyyyToIso, isoToDdMmYyyy } from '@/lib/utils';

type Props = {
  value: string; // yyyy-MM-dd
  onChange: (iso: string) => void;
  min?: string; // yyyy-MM-dd
  disabled?: boolean;
  className?: string;
};

/** Date input that displays/edits `dd/mm/yyyy` while keeping ISO `yyyy-MM-dd` value. */
export function DateInputDdMmYyyy({ value, onChange, min, disabled, className }: Props) {
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
      // Modern Chromium / Safari — opens native calendar UI
      el.showPicker();
    } catch {
      el.focus();
      el.click();
    }
  }

  return (
    <div className={cn('relative', className)}>
      <input
        type="text"
        inputMode="numeric"
        placeholder="dd/mm/yyyy"
        value={text}
        disabled={disabled}
        onChange={(e) => setText(e.target.value)}
        onBlur={(e) => commit(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.currentTarget.blur();
          }
        }}
        className="w-full rounded-md border bg-background px-3 py-2 pr-10 text-sm focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
      />
      <button
        type="button"
        disabled={disabled}
        onClick={openPicker}
        aria-label="Pick date"
        title="Pick date"
        className="absolute inset-y-0 right-0 inline-flex w-10 items-center justify-center rounded-r-md text-muted-foreground hover:bg-muted hover:text-foreground disabled:opacity-50"
      >
        <Calendar className="h-4 w-4" />
      </button>
      {/* Visually hidden native picker — opened via showPicker() from the button */}
      <input
        ref={pickerRef}
        type="date"
        value={value || ''}
        min={min}
        disabled={disabled}
        onChange={(e) => {
          onChange(e.target.value);
        }}
        tabIndex={-1}
        aria-hidden
        className="pointer-events-none absolute h-0 w-0 overflow-hidden opacity-0"
      />
    </div>
  );
}
