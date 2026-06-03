// src/pages/ConfigPage.tsx
import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Save } from 'lucide-react';
import { toast } from 'sonner';
import { useLanguage } from '@/contexts/LanguageContext';
import configService from '@/services/configService';

const LABEL_MAP: Record<string, string> = {
  sap_url_test:               'SAP URL (Test)',
  sap_url_prod:               'SAP URL (Production)',
  sap_env:                    'Environment ที่ใช้งาน (TST / PRD)',
  sap_auth_type:              'ประเภท Auth (None / ApiKey / Basic)',
  sap_api_key:                'SAP API Key',
  sap_basic_username:         'SAP Basic Auth Username',
  sap_basic_password:         'SAP Basic Auth Password',
  schedule_interval_minutes:  'ช่วงเวลา Schedule (นาที)',
  schedule_enabled:           'เปิดใช้ Schedule (true / false)',
  max_retry_count:            'จำนวน Retry สูงสุด',
};

const SENSITIVE_KEYS = new Set(['sap_api_key', 'sap_basic_password']);

export default function ConfigPage() {
  const { t } = useLanguage();
  const queryClient = useQueryClient();
  const { data: configs = [], isLoading } = useQuery({
    queryKey: ['configs'],
    queryFn: () => configService.getConfigs(),
    staleTime: 60_000,
  });

  const [values, setValues] = useState<Record<string, string>>({});
  const [interfaceType, setInterfaceType] = useState('ARInvoice');
  const [showGlobal, setShowGlobal] = useState(false);
  const [newKey, setNewKey] = useState('');
  const [newValue, setNewValue] = useState('');
  const [prefixWithInterface, setPrefixWithInterface] = useState(true);

  const getConfigLabel = (key: string) => {
    const label = t(`configLabel.${key}`);
    return label.startsWith('configLabel.') ? key : label;
  };

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

  async function handleSaveAll() {
    for (const config of configs) {
      const val = values[config.configKey] ?? '';
      if (val !== (config.configValue ?? '')) {
        await mutation.mutateAsync({ key: config.configKey, value: val });
      }
    }
    toast.success(t('saveSuccess'));
  }

  async function handleAddConfig() {
    if (!newKey || !newKey.trim()) {
      toast.error(t('configKeyRequired') || 'Config key is required');
      return;
    }
    const keyToUse = prefixWithInterface && !newKey.includes('.')
      ? `${interfaceType}.${newKey}`
      : newKey;
    try {
      await mutation.mutateAsync({ key: keyToUse, value: newValue ?? '' });
      setNewKey('');
      setNewValue('');
      toast.success(t('addConfigSuccess') || 'Config added');
    } catch (err) {
      // mutation onError will show toast
    }
  }

  async function copyGlobalToInterface() {
    // copy keys like sap_* (global) to interface-prefixed keys
    for (const cfg of configs) {
      if (!cfg.configKey.includes('.') && cfg.configKey.startsWith('sap_')) {
        const targetKey = `${interfaceType}.${cfg.configKey}`;
        const current = values[targetKey] ?? '';
        const val = cfg.configValue ?? '';
        if (current !== val) {
          await mutation.mutateAsync({ key: targetKey, value: val });
          setValues((v) => ({ ...v, [targetKey]: val }));
        }
      }
    }
    toast.success(t('copySuccess') || 'Copied global configs to interface');
  }

  async function handleTestConnection() {
    try {
      const res = await configService.testConfig(interfaceType);
      if (res.success) toast.success(res.message || t('testSuccess') || 'Connection successful');
      else toast.error(res.message || t('testFailed') || 'Test failed');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : t('testFailed') || 'Test failed');
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold">{t('configTitle')}</h1>
          <p className="text-sm text-muted-foreground">{t('configSubtitle')}</p>
        </div>
        <div className="flex items-center gap-3">
          <select value={interfaceType} onChange={(e) => setInterfaceType(e.target.value)} className="rounded-md border bg-background px-3 py-2 text-sm">
            <option value="ARInvoice">ARInvoice</option>
            <option value="IncomingPayment">IncomingPayment</option>
            <option value="Delivery">Delivery</option>
          </select>
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <input type="checkbox" checked={showGlobal} onChange={(e) => setShowGlobal(e.target.checked)} />
            {t('showGlobalConfigs')}
          </label>
          <button onClick={copyGlobalToInterface} className="rounded-md border px-3 py-2 text-sm hover:bg-muted">{t('copyGlobal') || 'Copy global → interface'}</button>
          <button onClick={handleTestConnection} className="rounded-md border px-3 py-2 text-sm hover:bg-muted">{t('testConnection') || 'Test connection'}</button>
          <button
          onClick={handleSaveAll}
          disabled={mutation.isPending}
          className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          <Save className="h-4 w-4" />
          {mutation.isPending ? t('saving') : t('saveAll')}
          </button>
        </div>
      </div>

      <div className="rounded-md border bg-card p-4">
        <h2 className="text-sm font-medium mb-2">{t('addConfigTitle') ?? 'Add Config'}</h2>
        <div className="flex gap-3">
          <input
            placeholder={t('configKeyPlaceholder') ?? 'key (e.g. sap_url_test)'}
            value={newKey}
            onChange={(e) => setNewKey(e.target.value)}
            className="rounded-md border bg-background px-3 py-2 text-sm w-64"
          />
          <input
            placeholder={t('configValuePlaceholder') ?? 'value'}
            value={newValue}
            onChange={(e) => setNewValue(e.target.value)}
            className="rounded-md border bg-background px-3 py-2 text-sm flex-1"
          />
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <input type="checkbox" checked={prefixWithInterface} onChange={(e) => setPrefixWithInterface(e.target.checked)} />
            {t('prefixWithInterface') ?? 'Prefix with interface'}
          </label>
          <button
            onClick={handleAddConfig}
            disabled={mutation.isPending}
            className="shrink-0 rounded-md border px-3 py-2 text-xs hover:bg-muted disabled:opacity-50"
          >
            {t('add') ?? 'Add'}
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">{t('loading')}</div>
      ) : (
        <div className="rounded-xl border bg-card shadow-sm divide-y">
          {configs
            .filter((config) => {
              const key = config.configKey.toLowerCase();
              const iface = interfaceType.toLowerCase();
              if (showGlobal && key.startsWith('sap_')) return true;
              return key.includes(iface);
            })
            .map((config) => {
            const currentValue = values[config.configKey] ?? '';
            const isPerInterface = config.configKey.toLowerCase().startsWith(interfaceType.toLowerCase() + '.');
            const isGlobalSap = !config.configKey.includes('.') && config.configKey.startsWith('sap_');

            return (
            <div key={config.configKey} className="flex items-center gap-4 px-5 py-3">
              <div className="w-64 shrink-0">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium">{getConfigLabel(config.configKey)}</p>
                  {isPerInterface && (
                    <span className="text-xs bg-green-100 text-green-800 px-2 py-0.5 rounded">per-interface</span>
                  )}
                  {isGlobalSap && (
                    <span className="text-xs bg-yellow-100 text-yellow-800 px-2 py-0.5 rounded">global</span>
                  )}
                </div>
                <p className="text-xs text-muted-foreground font-mono mt-0.5">{config.configKey}</p>
              </div>
              <input
                type={SENSITIVE_KEYS.has(config.configKey) ? 'password' : 'text'}
                value={currentValue}
                onChange={(e) => setValues((v) => ({ ...v, [config.configKey]: e.target.value }))}
                className="flex-1 rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring font-mono"
                placeholder={SENSITIVE_KEYS.has(config.configKey) ? '••••••••' : t('configPlaceholder')}
              />
              <button
                onClick={() => mutation.mutate({ key: config.configKey, value: currentValue })}
                disabled={mutation.isPending}
                className="shrink-0 rounded-md border px-3 py-2 text-xs hover:bg-muted disabled:opacity-50"
              >
                {t('save')}
              </button>
            </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
