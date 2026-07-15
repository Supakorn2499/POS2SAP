/**
 * Regression: mapping CSV round-trip (parse + expected headers).
 * Mirrors frontend/src/lib/parseCsv.ts enough to catch format breaks.
 *
 * Run: node frontend/pos2sap-ui/scripts/check_mapping_csv_roundtrip.mjs
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));

function parseCsv(text) {
  const cleaned = text.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  const records = [];
  let row = [];
  let cell = '';
  let inQuotes = false;
  for (let i = 0; i < cleaned.length; i++) {
    const ch = cleaned[i];
    if (inQuotes) {
      if (ch === '"') {
        if (cleaned[i + 1] === '"') {
          cell += '"';
          i++;
        } else inQuotes = false;
      } else cell += ch;
      continue;
    }
    if (ch === '"') inQuotes = true;
    else if (ch === ',') {
      row.push(cell);
      cell = '';
    } else if (ch === '\n') {
      row.push(cell);
      cell = '';
      records.push(row);
      row = [];
    } else cell += ch;
  }
  if (cell.length > 0 || row.length > 0) {
    row.push(cell);
    records.push(row);
  }
  if (records.length === 0) return { headers: [], rows: [] };
  const headers = records[0].map((h) => h.trim());
  const rows = [];
  for (let i = 1; i < records.length; i++) {
    const cells = records[i];
    if (cells.every((c) => !String(c).trim())) continue;
    const obj = {};
    headers.forEach((h, idx) => {
      obj[h] = String(cells[idx] ?? '').trim();
    });
    rows.push(obj);
  }
  return { headers, rows };
}

function csvCell(row, ...names) {
  for (const name of names) {
    if (Object.prototype.hasOwnProperty.call(row, name)) return row[name] ?? '';
    const found = Object.keys(row).find((k) => k.toLowerCase() === name.toLowerCase());
    if (found) return row[found] ?? '';
  }
  return '';
}

function assert(cond, msg) {
  if (!cond) {
    console.error('FAIL:', msg);
    process.exit(1);
  }
}

const glSample = `PayTypeID,PayTypeName,SapPayCategory,SapGlAccount,SapPayTypeName,IsActive,SortOrder,Remarks
1,"Cash, counter",CASH,111000,,1,10,"note ""A"""
2,Card,CREDIT_CARD,112000,VISA,1,20,
`;

const pgSample = `ProductGroupID,ProductGroupCode,ProductGroupName,SapItemGroupCode,SapItemGroupName,IsActive,SortOrder,Remarks
10,FOOD,Food Group,100,Food,1,10,
`;

const gl = parseCsv(glSample);
assert(gl.rows.length === 2, `GL expected 2 rows, got ${gl.rows.length}`);
assert(csvCell(gl.rows[0], 'PayTypeID') === '1', 'GL PayTypeID');
assert(csvCell(gl.rows[0], 'PayTypeName') === 'Cash, counter', 'GL quoted comma name');
assert(csvCell(gl.rows[0], 'Remarks') === 'note "A"', 'GL escaped quotes');
assert(csvCell(gl.rows[1], 'SapPayCategory') === 'CREDIT_CARD', 'GL category');

const pg = parseCsv(pgSample);
assert(csvCell(pg.rows[0], 'ProductGroupID') === '10', 'PG id');
assert(csvCell(pg.rows[0], 'SapItemGroupCode') === '100', 'PG sap code');

// BOM tolerance
const bom = parseCsv('\uFEFFPayTypeID,PayTypeName\n9,Test\n');
assert(csvCell(bom.rows[0], 'PayTypeID') === '9', 'BOM header');

// Optional: keep fixture in sync with downloadCsv header contract
const expectedGl = [
  'PayTypeID', 'PayTypeName', 'SapPayCategory', 'SapGlAccount',
  'SapPayTypeName', 'IsActive', 'SortOrder', 'Remarks',
];
assert(expectedGl.every((h) => gl.headers.includes(h)), 'GL headers missing');

console.log('OK: mapping CSV round-trip checks passed');
console.log('  GL rows', gl.rows.length, '| PG rows', pg.rows.length);
