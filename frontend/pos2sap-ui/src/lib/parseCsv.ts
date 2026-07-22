/** Minimal RFC4180-ish CSV parser (Excel UTF-8 / BOM / Thai-locale semicolon friendly). */
export function parseCsv(text: string): { headers: string[]; rows: Record<string, string>[] } {
  const cleaned = text.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  if (!cleaned.trim()) return { headers: [], rows: [] };

  const firstLineEnd = cleaned.indexOf('\n');
  const headerLine = firstLineEnd >= 0 ? cleaned.slice(0, firstLineEnd) : cleaned;
  const delimiter = detectCsvDelimiter(headerLine);

  const records = splitCsvRecords(cleaned, delimiter);
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

/** Thai Excel often Save As CSV with ';' — pick whichever appears more in the header. */
export function detectCsvDelimiter(headerLine: string): ',' | ';' {
  let commas = 0;
  let semis = 0;
  let inQuotes = false;
  for (let i = 0; i < headerLine.length; i++) {
    const ch = headerLine[i];
    if (ch === '"') {
      inQuotes = !inQuotes;
      continue;
    }
    if (inQuotes) continue;
    if (ch === ',') commas++;
    else if (ch === ';') semis++;
  }
  return semis > commas ? ';' : ',';
}

function splitCsvRecords(text: string, delimiter: ',' | ';' = ','): string[][] {
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
    } else if (ch === delimiter) {
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

function countThaiChars(s: string): number {
  let n = 0;
  for (let i = 0; i < s.length; i++) {
    const c = s.charCodeAt(i);
    if (c >= 0x0e00 && c <= 0x0e7f) n++;
  }
  return n;
}

function countReplacementChars(s: string): number {
  let n = 0;
  for (let i = 0; i < s.length; i++) {
    if (s.charCodeAt(i) === 0xfffd) n++;
  }
  return n;
}

/**
 * Decode CSV file bytes for Excel-on-Thai-Windows.
 * Export from this app is UTF-8 BOM; Excel "CSV" Save As is often Windows-874 (not UTF-8).
 */
export function decodeCsvBytes(bytes: Uint8Array): string {
  if (bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) {
    return new TextDecoder('utf-8').decode(bytes);
  }
  if (bytes.length >= 2 && bytes[0] === 0xff && bytes[1] === 0xfe) {
    return new TextDecoder('utf-16le').decode(bytes);
  }
  if (bytes.length >= 2 && bytes[0] === 0xfe && bytes[1] === 0xff) {
    return new TextDecoder('utf-16be').decode(bytes);
  }

  const utf8 = new TextDecoder('utf-8').decode(bytes);
  let ansi = '';
  try {
    ansi = new TextDecoder('windows-874').decode(bytes);
  } catch {
    return utf8;
  }

  const utf8Thai = countThaiChars(utf8);
  const ansiThai = countThaiChars(ansi);
  const utf8Bad = countReplacementChars(utf8);

  // Thai Excel ANSI CSV: UTF-8 decode looks broken / has no Thai while windows-874 has Thai
  if (ansiThai > 0 && (utf8Bad > 0 || ansiThai > utf8Thai)) {
    return ansi;
  }
  return utf8;
}
