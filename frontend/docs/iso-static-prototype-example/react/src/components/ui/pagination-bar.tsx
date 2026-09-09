export interface PaginationBarProps {
  total: number;
  page: number;
  pageSize: 25 | 50 | 100;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: 25 | 50 | 100) => void;
}

function parsePageSize(value: string): 25 | 50 | 100 {
  if (value === "25") return 25;
  if (value === "100") return 100;
  return 50;
}

export function PaginationBar({
  total,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
}: PaginationBarProps) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const start = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(total, page * pageSize);

  return (
    <div className="grid h-pagination grid-cols-[1fr_auto_1fr] items-center gap-4 border-t border-line bg-surface px-4 text-meta text-ink-muted max-md:h-auto max-md:grid-cols-1 max-md:py-2">
      <span className="tabular">
        {start}-{end} / {total}
      </span>

      <label className="flex items-center gap-2">
        <span>每頁</span>
        <select
          className="h-control-sm rounded-sm border border-line bg-surface px-2"
          value={pageSize}
          onChange={(event) =>
            onPageSizeChange(parsePageSize(event.target.value))
          }
        >
          <option value={25}>25</option>
          <option value={50}>50</option>
          <option value={100}>100</option>
        </select>
        <span>筆</span>
      </label>

      <div className="flex justify-end gap-1 max-md:justify-start">
        <button
          type="button"
          className="h-control-sm min-w-control-sm rounded-sm hover:bg-surface-header disabled:text-ink-disabled"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          ‹
        </button>
        <span className="grid h-control-sm min-w-control-sm place-items-center rounded-sm bg-primary px-2 text-on-primary">
          {page}
        </span>
        <button
          type="button"
          className="h-control-sm min-w-control-sm rounded-sm hover:bg-surface-header disabled:text-ink-disabled"
          disabled={page >= pageCount}
          onClick={() => onPageChange(page + 1)}
        >
          ›
        </button>
      </div>
    </div>
  );
}
