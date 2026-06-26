import { useState, useEffect, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Save, Clock, Database, Gauge, FileText, Receipt, Truck, Wrench } from 'lucide-react';
import { toast } from 'sonner';
import { useLanguage } from '@/contexts/LanguageContext';
import configService from '@/services/configService';
import ConfigFieldRow from '@/components/config/ConfigFieldRow';
import {
  CONFIG_SECTIONS,
  SAP_BASE_KEYS,
  SAP_INTERFACE_MAP,
  isKnownConfigKey,
  readFieldValue,
  resolveStorageKey,
  type ConfigSectionId,
} from '@/lib/configLayout';
import type { InterfaceConfigDto } from '@/types/config';

const NAV_ITEMS: { id: ConfigSectionId; labelKey: string; icon: typeof Clock }[] = [
  { id: 'auto', labelKey: 'configNav.auto', icon: Clock },
  { id: 'import', labelKey: 'configNav.import', icon: Database },
  { id: 'performance', labelKey: 'configNav.performance', icon: Gauge },
  { id: 'sap-ar', labelKey: 'configNav.sapAr', icon: FileText },
  { id: 'sap-ap', labelKey: 'configNav.sapAp', icon: Receipt },
  { id: 'sap-dl', labelKey: 'configNav.sapDl', icon: Truck },
  { id: 'advanced', labelKey: 'configNav.advanced', icon: Wrench },
];

export default function ConfigPage() {
  const { t } = useLanguage();
  const queryClient = useQueryClient();
  const [section, setSection] = useState<ConfigSectionId>('auto');
  const [values, setValues] = useState<Record<string, string>>({});
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');

  const { data: configs = [], isLoading } = useQuery({
    queryKey: ['configs'],
    queryFn: () => configService.getConfigs(),
    staleTime: 60_000,
  });

  useEffect(() => {
    const map: Record<string, string> = {};
    configs.forEach((c) => { map[c.configKey] = c.configValue ?? ''; });
    setValues(map);
  }, [configs]);

  const mutation = useMutation({
    mutationFn: ({ key, value }: { key: string; value: string }) =>
      configService.updateConfig(key, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configs'] });
      toast.success(t('saveSuccess'));
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : t('saveFailed'));
    },
  });

  const sapInterface = section.startsWith('sap-')
    ? SAP_INTERFACE_MAP[section as 'sap-ar' | 'sap-ap' | 'sap-dl']
    : null;

  const unknownConfigs = useMemo(
    () => configs.filter((c) => !isKnownConfigKey(c.configKey)),
    [configs],
  );

  function handleChange(storageKey: string, value: string) {
    setValues((v) => ({ ...v, [storageKey]: value }));
  }

  async function handleSaveKey(storageKey: string, value: string) {
    await mutation.mutateAsync({ key: storageKey, value });
  }

  async function handleSaveSection() {
    const keysToSave = getSectionKeys(section);
    let saved = 0;
    for (const baseKey of keysToSave) {
      const storageKey = resolveStorageKey(baseKey, sapInterface, configs);
      const val = values[storageKey] ?? '';
      const original = configs.find((c) => c.configKey === storageKey)?.configValue ?? '';
      if (val !== original) {
        await mutation.mutateAsync({ key: storageKey, value: val });
        saved++;
      }
    }
    if (saved === 0) toast.message(t('configNoChanges'));
    else toast.success(t('saveSuccess'));
  }

  async function handleTestConnection() {
    if (!sapInterface) return;
    try {
      const res = await configService.testConfig(sapInterface);
      if (res.success) toast.success(res.message || t('testSuccess'));
      else toast.error(res.message || t('testFailed'));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : t('testFailed'));
    }
  }

  async function handleCopyGlobalSap() {
    if (!sapInterface) return;
    for (const baseKey of SAP_BASE_KEYS) {
      const globalVal = values[baseKey] ?? configs.find((c) => c.configKey === baseKey)?.configValue ?? '';
      const targetKey = `${sapInterface}.${baseKey}`;
      await mutation.mutateAsync({ key: targetKey, value: globalVal });
      setValues((v) => ({ ...v, [targetKey]: globalVal }));
    }
    toast.success(t('copySuccess'));
  }

  async function handleAddConfig() {
    if (!newKey.trim()) {
      toast.error(t('configKeyRequired'));
      return;
    }
    await mutation.mutateAsync({ key: newKey.trim(), value: newValue ?? '' });
    setNewKey('');
    setNewValue('');
    toast.success(t('addConfigSuccess'));
  }

  const activeSectionDef = CONFIG_SECTIONS.find((s) => s.id === section);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h1 className="text-xl font-bold">{t('configTitle')}</h1>
          <p className="text-sm text-muted-foreground">{t('configSubtitleGrouped')}</p>
        </div>
      </div>

      <div className="flex flex-col gap-6 lg:flex-row">
        {/* Sidebar navigation */}
        <nav className="flex shrink-0 flex-row flex-wrap gap-1 lg:w-52 lg:flex-col">
          {NAV_ITEMS.map(({ id, labelKey, icon: Icon }) => (
            <button
              key={id}
              type="button"
              onClick={() => setSection(id)}
              className={`flex items-center gap-2 rounded-lg px-3 py-2.5 text-left text-sm transition-colors ${
                section === id
                  ? 'bg-primary text-primary-foreground font-medium'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground'
              }`}
            >
              <Icon className="h-4 w-4 shrink-0" />
              {t(labelKey)}
            </button>
          ))}
        </nav>

        {/* Main panel */}
        <div className="min-w-0 flex-1 space-y-4">
          {isLoading ? (
            <div className="text-sm text-muted-foreground">{t('loading')}</div>
          ) : (
            <>
              {/* Section header */}
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-card px-5 py-4">
                <div>
                  <h2 className="font-semibold">
                    {section === 'advanced'
                      ? t('configGroup.advanced')
                      : section.startsWith('sap-')
                        ? t(`configGroup.sap.${sapInterface}`)
                        : t(activeSectionDef?.titleKey ?? '')}
                  </h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {section === 'advanced'
                      ? t('configGroup.advancedDesc')
                      : section.startsWith('sap-')
                        ? t('configGroup.sapDesc')
                        : t(activeSectionDef?.descKey ?? '')}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {section.startsWith('sap-') && (
                    <>
                      <button
                        type="button"
                        onClick={handleTestConnection}
                        className="rounded-md border px-3 py-2 text-sm hover:bg-muted"
                      >
                        {t('testConnection')}
                      </button>
                      <button
                        type="button"
                        onClick={handleCopyGlobalSap}
                        className="rounded-md border px-3 py-2 text-sm hover:bg-muted"
                      >
                        {t('copyGlobal')}
                      </button>
                    </>
                  )}
                  {section !== 'advanced' && (
                    <button
                      type="button"
                      onClick={handleSaveSection}
                      disabled={mutation.isPending}
                      className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                    >
                      <Save className="h-4 w-4" />
                      {mutation.isPending ? t('saving') : t('configSaveSection')}
                    </button>
                  )}
                </div>
              </div>

              {/* Fields */}
              {section !== 'advanced' && (
                <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
                  {renderSectionFields(
                    section,
                    configs,
                    values,
                    sapInterface,
                    handleChange,
                    handleSaveKey,
                    mutation.isPending,
                  )}
                </div>
              )}

              {/* Advanced */}
              {section === 'advanced' && (
                <div className="space-y-4">
                  <div className="rounded-xl border bg-card p-4">
                    <h3 className="mb-3 text-sm font-medium">{t('addConfigTitle')}</h3>
                    <div className="flex flex-col gap-3 sm:flex-row">
                      <input
                        placeholder={t('configKeyPlaceholder')}
                        value={newKey}
                        onChange={(e) => setNewKey(e.target.value)}
                        className="rounded-md border bg-background px-3 py-2 text-sm font-mono sm:w-64"
                      />
                      <input
                        placeholder={t('configValuePlaceholder')}
                        value={newValue}
                        onChange={(e) => setNewValue(e.target.value)}
                        className="flex-1 rounded-md border bg-background px-3 py-2 text-sm font-mono"
                      />
                      <button
                        type="button"
                        onClick={handleAddConfig}
                        disabled={mutation.isPending}
                        className="rounded-md border px-4 py-2 text-sm hover:bg-muted disabled:opacity-50"
                      >
                        {t('add')}
                      </button>
                    </div>
                  </div>

                  {unknownConfigs.length > 0 ? (
                    <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
                      {unknownConfigs.map((cfg) => (
                        <ConfigFieldRow
                          key={cfg.configKey}
                          baseKey={cfg.configKey}
                          storageKey={cfg.configKey}
                          value={values[cfg.configKey] ?? ''}
                          onChange={handleChange}
                          onSave={handleSaveKey}
                          saving={mutation.isPending}
                        />
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm text-muted-foreground">{t('configAdvancedEmpty')}</p>
                  )}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function getSectionKeys(section: ConfigSectionId): string[] {
  if (section.startsWith('sap-')) return [...SAP_BASE_KEYS];
  const def = CONFIG_SECTIONS.find((s) => s.id === section);
  return def?.keys ?? [];
}

function renderSectionFields(
  section: ConfigSectionId,
  configs: InterfaceConfigDto[],
  values: Record<string, string>,
  sapInterface: string | null,
  onChange: (k: string, v: string) => void,
  onSave: (k: string, v: string) => void,
  saving: boolean,
) {
  const keys = getSectionKeys(section);
  if (keys.length === 0) return null;

  return keys.map((baseKey) => {
    const storageKey = resolveStorageKey(baseKey, sapInterface, configs);
    const value = readFieldValue(baseKey, sapInterface, values, configs);
    const prefixed = sapInterface ? `${sapInterface}.${baseKey}` : null;
    const inheritedFromGlobal =
      !!sapInterface &&
      !!prefixed &&
      storageKey === baseKey &&
      !configs.some((c) => c.configKey === prefixed);

    return (
      <ConfigFieldRow
        key={`${section}-${baseKey}`}
        baseKey={baseKey}
        storageKey={sapInterface && baseKey.startsWith('sap_') ? `${sapInterface}.${baseKey}` : storageKey}
        value={value}
        onChange={onChange}
        onSave={onSave}
        saving={saving}
        inheritedFromGlobal={inheritedFromGlobal}
      />
    );
  });
}
