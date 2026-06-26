// Config page layout — groups, field types, and key resolution

import type { InterfaceConfigDto } from '@/types/config';

export type ConfigSectionId =
  | 'auto'
  | 'import'
  | 'performance'
  | 'sap-ar'
  | 'sap-ap'
  | 'sap-dl'
  | 'advanced';

export type FieldType = 'text' | 'password' | 'boolean' | 'select' | 'date' | 'time' | 'number';

export interface FieldMeta {
  type: FieldType;
  options?: string[];
  hintKey?: string;
  min?: number;
  max?: number;
  /** time fields — empty string = no window limit (24h) */
  allowEmpty?: boolean;
}

export interface ConfigSectionDef {
  id: ConfigSectionId;
  titleKey: string;
  descKey: string;
  /** Base config keys (no interface prefix) */
  keys: string[];
}

export const CONFIG_SECTIONS: ConfigSectionDef[] = [
  {
    id: 'auto',
    titleKey: 'configGroup.auto',
    descKey: 'configGroup.autoDesc',
    keys: [
      'schedule_enabled',
      'schedule_interval_minutes',
      'schedule_window_start',
      'schedule_window_end',
      'schedule_timezone',
      'schedule_max_runtime_minutes',
    ],
  },
  {
    id: 'import',
    titleKey: 'configGroup.import',
    descKey: 'configGroup.importDesc',
    keys: [
      'interface_cutover_date',
      'import_date_to_mode',
      'import_batch_size',
      'import_chunk_days',
    ],
  },
  {
    id: 'performance',
    titleKey: 'configGroup.performance',
    descKey: 'configGroup.performanceDesc',
    keys: ['sap_http_timeout_seconds', 'max_retry_count'],
  },
];

export const SAP_BASE_KEYS = [
  'sap_env',
  'sap_url_test',
  'sap_url_prod',
  'sap_auth_type',
  'sap_api_key',
  'sap_basic_username',
  'sap_basic_password',
] as const;

export const SAP_INTERFACE_MAP: Record<'sap-ar' | 'sap-ap' | 'sap-dl', string> = {
  'sap-ar': 'ARInvoice',
  'sap-ap': 'IncomingPayment',
  'sap-dl': 'Delivery',
};

export const SENSITIVE_KEYS = new Set(['sap_api_key', 'sap_basic_password']);

export const FIELD_META: Record<string, FieldMeta> = {
  schedule_enabled: { type: 'boolean', hintKey: 'configHint.schedule_enabled' },
  schedule_interval_minutes: { type: 'number', min: 1, max: 1440, hintKey: 'configHint.schedule_interval_minutes' },
  schedule_window_start: { type: 'time', allowEmpty: true, hintKey: 'configHint.schedule_window_start' },
  schedule_window_end: { type: 'time', allowEmpty: true, hintKey: 'configHint.schedule_window_end' },
  schedule_timezone: { type: 'text', hintKey: 'configHint.schedule_timezone' },
  schedule_max_runtime_minutes: { type: 'number', min: 5, max: 720, hintKey: 'configHint.schedule_max_runtime_minutes' },
  interface_cutover_date: { type: 'date', hintKey: 'configHint.interface_cutover_date' },
  import_date_to_mode: { type: 'select', options: ['yesterday', 'today'], hintKey: 'configHint.import_date_to_mode' },
  import_batch_size: { type: 'number', min: 1, max: 1000, hintKey: 'configHint.import_batch_size' },
  import_chunk_days: { type: 'number', min: 1, max: 31, hintKey: 'configHint.import_chunk_days' },
  sap_http_timeout_seconds: { type: 'number', min: 10, max: 300, hintKey: 'configHint.sap_http_timeout_seconds' },
  max_retry_count: { type: 'number', min: 1, max: 10, hintKey: 'configHint.max_retry_count' },
  sap_env: { type: 'select', options: ['TST', 'PRD'], hintKey: 'configHint.sap_env' },
  sap_auth_type: { type: 'select', options: ['None', 'ApiKey', 'Basic'], hintKey: 'configHint.sap_auth_type' },
  sap_url_test: { type: 'text', hintKey: 'configHint.sap_url_test' },
  sap_url_prod: { type: 'text', hintKey: 'configHint.sap_url_prod' },
  sap_api_key: { type: 'password', hintKey: 'configHint.sap_api_key' },
  sap_basic_username: { type: 'text', hintKey: 'configHint.sap_basic_username' },
  sap_basic_password: { type: 'password', hintKey: 'configHint.sap_basic_password' },
};

const ALL_KNOWN_KEYS = new Set([
  ...CONFIG_SECTIONS.flatMap((s) => s.keys),
  ...SAP_BASE_KEYS,
]);

/** DB key used for read/write — prefers interface-prefixed row */
export function resolveStorageKey(
  baseKey: string,
  interfaceType: string | null,
  configs: InterfaceConfigDto[],
): string {
  if (interfaceType) {
    const prefixed = `${interfaceType}.${baseKey}`;
    if (configs.some((c) => c.configKey === prefixed)) return prefixed;
    // New edits on SAP tab always write to prefixed key
    if (baseKey.startsWith('sap_')) return prefixed;
  }
  return baseKey;
}

export function readFieldValue(
  baseKey: string,
  interfaceType: string | null,
  values: Record<string, string>,
  configs: InterfaceConfigDto[],
): string {
  if (interfaceType && baseKey.startsWith('sap_')) {
    const prefixed = `${interfaceType}.${baseKey}`;
    if (values[prefixed] !== undefined && values[prefixed] !== '') return values[prefixed];
    if (configs.some((c) => c.configKey === prefixed) && values[prefixed] !== undefined)
      return values[prefixed] ?? '';
  }
  return values[baseKey] ?? '';
}

export function isKnownConfigKey(key: string): boolean {
  const base = key.includes('.') ? key.split('.').slice(1).join('.') : key;
  return ALL_KNOWN_KEYS.has(base) || ALL_KNOWN_KEYS.has(key);
}

export function getFieldMeta(baseKey: string): FieldMeta {
  return FIELD_META[baseKey] ?? { type: 'text' };
}
