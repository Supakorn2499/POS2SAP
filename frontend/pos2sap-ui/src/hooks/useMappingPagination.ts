import { useEffect, useMemo, useState } from 'react';

export const MAPPING_PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
export const MAPPING_DEFAULT_PAGE_SIZE = 20;

export function useMappingPagination<T>(
  items: T[],
  initialPageSize: number = MAPPING_DEFAULT_PAGE_SIZE,
) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize);

  const total = items.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize) || 1);

  useEffect(() => {
    setPage(1);
  }, [total, pageSize]);

  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  const paginated = useMemo(() => {
    if (total === 0) return [];
    const start = (page - 1) * pageSize;
    return items.slice(start, start + pageSize);
  }, [items, page, pageSize, total]);

  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return {
    page,
    setPage,
    pageSize,
    setPageSize,
    totalPages,
    total,
    paginated,
    from,
    to,
  };
}
