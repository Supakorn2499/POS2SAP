import { DateInputDdMmYyyy } from '@/components/DateInputDdMmYyyy';
import { AppSelect } from '@/components/ui/AppSelect';
import { useLanguage } from '@/contexts/LanguageContext';
import { getFieldMeta, type FieldType } from '@/lib/configLayout';
import { cn } from '@/lib/utils';

interface ConfigFieldRowProps {
  baseKey: string;
  storageKey: string;
  value: string;
  onChange: (storageKey: string, value: string) => void;
  onSave: (storageKey: string, value: string) => void;
  saving?: boolean;
  inheritedFromGlobal?: boolean;
}

export default function ConfigFieldRow({
  baseKey,
  storageKey,
  value,
  onChange,
  onSave,
  saving,
  inheritedFromGlobal,
}: ConfigFieldRowProps) {
  const { t } = useLanguage();
  const meta = getFieldMeta(baseKey);
  const label = t(`configLabel.${baseKey}`);
  const displayLabel = label.startsWith('configLabel.') ? baseKey : label;
  const hint = meta.hintKey ? t(meta.hintKey) : '';
  const displayHint = hint.startsWith('configHint.') ? '' : hint;

  const inputClass = 'app-control font-mono';

  function renderInput() {
    switch (meta.type as FieldType) {
      case 'boolean':
        return (
          <AppSelect
            value={value.toLowerCase() === 'true' ? 'true' : 'false'}
            onChange={(e) => onChange(storageKey, e.target.value)}
          >
            <option value="true">{t('configBool.true')}</option>
            <option value="false">{t('configBool.false')}</option>
          </AppSelect>
        );
      case 'select':
        return (
          <AppSelect
            value={value}
            onChange={(e) => onChange(storageKey, e.target.value)}
          >
            <option value="">{t('configPlaceholder')}</option>
            {meta.options?.map((opt) => (
              <option key={opt} value={opt}>
                {opt}
              </option>
            ))}
          </AppSelect>
        );
      case 'number':
        return (
          <input
            type="number"
            min={meta.min}
            max={meta.max}
            value={value}
            onChange={(e) => onChange(storageKey, e.target.value)}
            className={inputClass}
          />
        );
      case 'date':
        return (
          <DateInputDdMmYyyy
            value={value}
            onChange={(iso) => onChange(storageKey, iso)}
            className="max-w-xs"
          />
        );
      case 'time':
        if (meta.allowEmpty) {
          return (
            <div className="flex flex-wrap items-center gap-2">
              <input
                type="text"
                value={value}
                onChange={(e) => onChange(storageKey, e.target.value)}
                className={cn(inputClass, 'min-w-[8rem] flex-1')}
                placeholder="20:00"
                inputMode="numeric"
              />
              <button
                type="button"
                onClick={() => onChange(storageKey, '')}
                disabled={!value}
                className="app-btn-ghost h-10 shrink-0 px-3 text-xs disabled:opacity-40"
              >
                {t('configClearTime')}
              </button>
            </div>
          );
        }
        return (
          <input
            type="time"
            value={value}
            onChange={(e) => onChange(storageKey, e.target.value)}
            className={inputClass}
            placeholder="HH:mm"
          />
        );
      case 'password':
        return (
          <input
            type="password"
            value={value}
            onChange={(e) => onChange(storageKey, e.target.value)}
            className={inputClass}
            placeholder="••••••••"
            autoComplete="off"
          />
        );
      default:
        return (
          <input
            type="text"
            value={value}
            onChange={(e) => onChange(storageKey, e.target.value)}
            className={inputClass}
            placeholder={t('configPlaceholder')}
          />
        );
    }
  }

  return (
    <div className="grid gap-3 border-b border-border/60 px-4 py-4 last:border-b-0 sm:px-5 md:grid-cols-[minmax(160px,240px)_1fr_auto] md:items-start">
      <div>
        <p className="text-sm font-medium">{displayLabel}</p>
        <p className="mt-0.5 font-mono text-xs text-muted-foreground">{storageKey}</p>
        {inheritedFromGlobal && (
          <p className="mt-1 text-xs text-amber-700 dark:text-amber-400">{t('configInheritedGlobal')}</p>
        )}
        {displayHint && (
          <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">{displayHint}</p>
        )}
      </div>
      <div className="min-w-0">{renderInput()}</div>
      <button
        type="button"
        onClick={() => onSave(storageKey, value)}
        disabled={saving}
        className="app-btn-ghost h-10 shrink-0 self-start px-3 text-xs disabled:opacity-50"
      >
        {t('save')}
      </button>
    </div>
  );
}
