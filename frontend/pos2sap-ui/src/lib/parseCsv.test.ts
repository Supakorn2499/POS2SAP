import { describe, expect, it } from 'vitest';
import { csvBool, csvCell, csvInt, csvNullable, decodeCsvBytes, parseCsv } from './parseCsv';

describe('parseCsv', () => {
  it('parses simple CSV with headers', () => {
    const { headers, rows } = parseCsv('a,b\n1,2\n3,4\n');
    expect(headers).toEqual(['a', 'b']);
    expect(rows).toEqual([
      { a: '1', b: '2' },
      { a: '3', b: '4' },
    ]);
  });

  it('strips BOM and normalizes CRLF', () => {
    const { headers, rows } = parseCsv('\uFEFFname,qty\r\nfoo,1\r\n');
    expect(headers).toEqual(['name', 'qty']);
    expect(rows).toEqual([{ name: 'foo', qty: '1' }]);
  });

  it('handles quoted commas and escaped quotes', () => {
    const { rows } = parseCsv('name,note\n"A, B","say ""hi"""\n');
    expect(rows[0]).toEqual({ name: 'A, B', note: 'say "hi"' });
  });

  it('parses semicolon-delimited CSV (Thai Excel locale)', () => {
    const { headers, rows } = parseCsv('PayTypeID;SapPayTypeName\n1;Cash\n2;Grabfood\n');
    expect(headers).toEqual(['PayTypeID', 'SapPayTypeName']);
    expect(rows).toEqual([
      { PayTypeID: '1', SapPayTypeName: 'Cash' },
      { PayTypeID: '2', SapPayTypeName: 'Grabfood' },
    ]);
  });

  it('reads SapPayTypeName from gl-mapping style header', () => {
    const csv = 'PayTypeID,PayTypeName,SapPayCategory,SapGlAccount,SapPayTypeName,IsActive,SortOrder,Remarks\n'
      + '1,Cash,CASH,11101002,Cash,1,10,เงินสด\n'
      + '148,Grabfood Payment,TRANSFER,11101005,Grabfood,1,30,ยอดขาย Grabfood\n';
    const { rows } = parseCsv(csv);
    expect(csvCell(rows[0], 'SapPayTypeName')).toBe('Cash');
    expect(csvCell(rows[1], 'SapPayTypeName')).toBe('Grabfood');
  });
});

describe('decodeCsvBytes', () => {
  it('keeps UTF-8 BOM export intact', () => {
    const text = '\uFEFFPayTypeID,Remarks\n1,เงินสด\n';
    const bytes = new TextEncoder().encode(text);
    expect(decodeCsvBytes(bytes)).toContain('เงินสด');
  });

  it('decodes Thai Excel Windows-874 CSV', () => {
    // เงินสด in Windows-874 / TIS-620: E0 A7 D4 B9 CA B4
    const bytes = Uint8Array.from([
      ...new TextEncoder().encode('PayTypeID,Remarks\n1,'),
      0xe0, 0xa7, 0xd4, 0xb9, 0xca, 0xb4,
      0x0a,
    ]);
    const decoded = decodeCsvBytes(bytes);
    expect(decoded).toContain('เงินสด');
    const { rows } = parseCsv(decoded);
    expect(csvCell(rows[0], 'Remarks')).toBe('เงินสด');
  });
});

describe('csv helpers', () => {
  it('csvBool', () => {
    expect(csvBool('true')).toBe(true);
    expect(csvBool('0')).toBe(false);
    expect(csvBool('', true)).toBe(true);
    expect(csvBool(undefined, false)).toBe(false);
    expect(csvBool('maybe', true)).toBe(true);
  });

  it('csvInt', () => {
    expect(csvInt('42')).toBe(42);
    expect(csvInt('x', 7)).toBe(7);
    expect(csvInt('', 3)).toBe(3);
  });

  it('csvNullable', () => {
    expect(csvNullable(undefined)).toBeNull();
    expect(csvNullable('  ')).toBeNull();
    expect(csvNullable(' hi ')).toBe('hi');
  });

  it('csvCell is case-insensitive', () => {
    const row = { PayTypeID: '6' };
    expect(csvCell(row, 'paytypeid')).toBe('6');
    expect(csvCell(row, 'missing', 'PayTypeID')).toBe('6');
    expect(csvCell(row, 'nope')).toBe('');
  });
});
