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

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold">{t('configTitle')}</h1>
          <p className="text-sm text-muted-foreground">{t('configSubtitle')}</p>
        </div>
        <button
          onClick={handleSaveAll}
          disabled={mutation.isPending}
          className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          <Save className="h-4 w-4" />
          {mutation.isPending ? t('saving') : t('saveAll')}
        </button>
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">{t('loading')}</div>
      ) : (
        <div className="rounded-xl border bg-card shadow-sm divide-y">
          {configs.map((config) => {
            const currentValue = values[config.configKey] ?? '';

            return (
            <div key={config.configKey} className="flex items-center gap-4 px-5 py-3">
              <div className="w-64 shrink-0">
                <p className="text-sm font-medium">{getConfigLabel(config.configKey)}</p>
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
