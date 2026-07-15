// src/lib/utils.ts
import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function fmt(n?: number | null): string {
  if (n == null) return '-';
  return n.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function fmtDate(s?: string | null): string {
  if (!s) return '-';
  try {
    return new Date(s).toLocaleDateString('th-TH', { year: 'numeric', month: '2-digit', day: '2-digit' });
  } catch {
    return s;
  }
}

export function fmtDatetime(s?: string | null): string {
  if (!s) return '-';
  try {
    return new Date(s).toLocaleString('th-TH', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    });
  } catch {
    return s;
  }
}

export function todayStr(): string {
  return new Date().toISOString().slice(0, 10);
}

/** Format ISO date `yyyy-MM-dd` → `dd/mm/yyyy` for UI inputs. */
export function isoToDdMmYyyy(iso?: string | null): string {
  if (!iso) return '';
  const m = String(iso).slice(0, 10).match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return '';
  return `${m[3]}/${m[2]}/${m[1]}`;
}

/** Parse `dd/mm/yyyy` → ISO `yyyy-MM-dd`, or null if invalid. */
export function ddMmYyyyToIso(text: string): string | null {
  const m = text.trim().match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
  if (!m) return null;
  const d = Number(m[1]);
  const mo = Number(m[2]);
  const y = Number(m[3]);
  if (mo < 1 || mo > 12 || d < 1 || d > 31) return null;
  const dt = new Date(y, mo - 1, d);
  if (dt.getFullYear() !== y || dt.getMonth() !== mo - 1 || dt.getDate() !== d) return null;
  return `${y}-${String(mo).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}
