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
