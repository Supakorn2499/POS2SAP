import { describe, expect, it } from 'vitest';
import { interfaceTypeLabel, interfaceTypeToTrigger } from './interfaceResend';

describe('interfaceTypeToTrigger', () => {
  it('maps DB codes to API names', () => {
    expect(interfaceTypeToTrigger('AP')).toBe('IncomingPayment');
    expect(interfaceTypeToTrigger('DL')).toBe('Delivery');
    expect(interfaceTypeToTrigger('AR')).toBe('ARInvoice');
    expect(interfaceTypeToTrigger('')).toBe('ARInvoice');
  });
});

describe('interfaceTypeLabel', () => {
  it('uses translator callback', () => {
    const t = (k: string) => `T:${k}`;
    expect(interfaceTypeLabel('AP', t)).toBe('T:interfaceTypeAP');
    expect(interfaceTypeLabel('IncomingPayment', t)).toBe('T:interfaceTypeAP');
    expect(interfaceTypeLabel('DL', t)).toBe('T:interfaceTypeDL');
    expect(interfaceTypeLabel('Delivery', t)).toBe('T:interfaceTypeDL');
    expect(interfaceTypeLabel('AR', t)).toBe('T:interfaceTypeAR');
    expect(interfaceTypeLabel('ARInvoice', t)).toBe('T:interfaceTypeAR');
    expect(interfaceTypeLabel(undefined, t)).toBe('T:interfaceTypeAR');
  });
});
