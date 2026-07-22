import { describe, expect, it } from 'vitest';
import {
  isKnownConfigKey,
  readFieldValue,
  resolveStorageKey,
} from './configLayout';
import type { InterfaceConfigDto } from '@/types/config';

const configs = (keys: string[]): InterfaceConfigDto[] =>
  keys.map((configKey) => ({
    id: configKey,
    configKey,
    configValue: 'x',
    description: '',
    isActive: true,
    updatedAt: '2026-07-01T00:00:00Z',
  }));

describe('resolveStorageKey', () => {
  it('prefers interface-prefixed key when it exists', () => {
    expect(
      resolveStorageKey('sap_url_test', 'ARInvoice', configs(['ARInvoice.sap_url_test'])),
    ).toBe('ARInvoice.sap_url_test');
  });

  it('writes new sap_ keys to prefixed form', () => {
    expect(resolveStorageKey('sap_api_key', 'ARInvoice', configs([]))).toBe('ARInvoice.sap_api_key');
  });

  it('keeps base key when no interface', () => {
    expect(resolveStorageKey('schedule_enabled', null, configs([]))).toBe('schedule_enabled');
  });
});

describe('readFieldValue', () => {
  it('reads prefixed sap value when present', () => {
    const values = { 'ARInvoice.sap_url_test': 'https://ar', sap_url_test: 'https://base' };
    expect(readFieldValue('sap_url_test', 'ARInvoice', values, configs(['ARInvoice.sap_url_test']))).toBe(
      'https://ar',
    );
  });

  it('falls back to base key', () => {
    const values = { schedule_enabled: 'true' };
    expect(readFieldValue('schedule_enabled', null, values, configs([]))).toBe('true');
  });
});

describe('isKnownConfigKey', () => {
  it('recognizes base and prefixed keys', () => {
    expect(isKnownConfigKey('schedule_enabled')).toBe(true);
    expect(isKnownConfigKey('ARInvoice.sap_url_test')).toBe(true);
    expect(isKnownConfigKey('totally_unknown')).toBe(false);
  });
});
