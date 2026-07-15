/** Minimal RFC4180-ish CSV parser (Excel UTF-8 / BOM friendly). */
export function parseCsv(text: string): { headers: string[]; rows: Record<string, string>[] } {
  const cleaned = text.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  const records = splitCsvRecords(cleaned);
  if (records.length === 0) return { headers: [], rows: [] };

  const headers = records[0].map((h) => h.trim());
  const rows: Record<string, string>[] = [];
  for (let i = 1; i < records.length; i++) {
    const cells = records[i];
    if (cells.every((c) => !c.trim())) continue;
    const row: Record<string, string> = {};
    headers.forEach((h, idx) => {
      row[h] = (cells[idx] ?? '').trim();
    });
    rows.push(row);
  }
  return { headers, rows };
}

function splitCsvRecords(text: string): string[][] {
  const records: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        cell += ch;
      }
      continue;
    }

    if (ch === '"') {
      inQuotes = true;
    } else if (ch === ',') {
      row.push(cell);
      cell = '';
    } else if (ch === '\n') {
      row.push(cell);
      cell = '';
      records.push(row);
      row = [];
    } else {
      cell += ch;
    }
  }

  if (cell.length > 0 || row.length > 0) {
    row.push(cell);
    records.push(row);
  }
  return records;
}

export function csvBool(v: string | undefined, fallback = true): boolean {
  if (v == null || v === '') return fallback;
  const s = v.trim().toLowerCase();
  if (['1', 'true', 'yes', 'y', 'on'].includes(s)) return true;
  if (['0', 'false', 'no', 'n', 'off'].includes(s)) return false;
  return fallback;
}

export function csvInt(v: string | undefined, fallback = 0): number {
  if (v == null || v === '') return fallback;
  const n = Number.parseInt(v, 10);
  return Number.isFinite(n) ? n : fallback;
}

export function csvNullable(v: string | undefined): string | null {
  if (v == null) return null;
  const s = v.trim();
  return s === '' ? null : s;
}

/** Case-insensitive header lookup (Excel often changes casing). */
export function csvCell(row: Record<string, string>, ...names: string[]): string {
  for (const name of names) {
    if (Object.prototype.hasOwnProperty.call(row, name)) return row[name] ?? '';
    const found = Object.keys(row).find((k) => k.toLowerCase() === name.toLowerCase());
    if (found) return row[found] ?? '';
  }
  return '';
}
