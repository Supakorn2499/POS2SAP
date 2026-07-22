import { describe, expect, it } from 'vitest';
import { getTranslation } from './i18n';

describe('getTranslation', () => {
  it('returns known key for language', () => {
    expect(getTranslation('statusLabel.PENDING', 'en')).toBe('Pending');
    expect(getTranslation('statusLabel.PENDING', 'th')).toBe('รอส่ง');
  });

  it('falls back to key when missing', () => {
    expect(getTranslation('definitely.missing.key', 'en')).toBe('definitely.missing.key');
  });

  it('interpolates {param} placeholders', () => {
    expect(getTranslation('appLogsLastLines', 'en', { n: 50 })).toBe('Last 50 lines');
    expect(getTranslation('pageOf', 'th', { page: 2, total: 10 })).toBe('หน้า 2 / 10');
  });
});
