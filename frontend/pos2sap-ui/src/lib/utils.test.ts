import { describe, expect, it } from 'vitest';
import { ddMmYyyyToIso, fmt, fmtDate, isoToDdMmYyyy } from './utils';

describe('ddMmYyyyToIso / isoToDdMmYyyy', () => {
  it('round-trips valid dates', () => {
    expect(ddMmYyyyToIso('01/07/2026')).toBe('2026-07-01');
    expect(isoToDdMmYyyy('2026-07-01')).toBe('01/07/2026');
  });

  it('rejects invalid calendar dates', () => {
    expect(ddMmYyyyToIso('31/02/2024')).toBeNull();
    expect(ddMmYyyyToIso('32/01/2024')).toBeNull();
    expect(ddMmYyyyToIso('not-a-date')).toBeNull();
  });

  it('accepts single-digit day/month', () => {
    expect(ddMmYyyyToIso('1/7/2026')).toBe('2026-07-01');
  });

  it('isoToDdMmYyyy returns empty for bad input', () => {
    expect(isoToDdMmYyyy(null)).toBe('');
    expect(isoToDdMmYyyy('garbage')).toBe('');
  });
});

describe('fmt / fmtDate null branches', () => {
  it('fmt returns dash for nullish', () => {
    expect(fmt(null)).toBe('-');
    expect(fmt(undefined)).toBe('-');
  });

  it('fmtDate returns dash for empty', () => {
    expect(fmtDate(null)).toBe('-');
    expect(fmtDate('')).toBe('-');
  });
});
