import type {
  DocumentQuery,
  DocumentStatus,
  SelectOption,
} from "../types";

export interface DocumentFilterBarProps {
  value: DocumentQuery;
  onChange: (next: DocumentQuery) => void;
  categories: SelectOption[];
  companies: SelectOption[];
}

const STATUS_OPTIONS: readonly {
  value: DocumentStatus;
  label: string;
}[] = [
  { value: "active", label: "有效" },
  { value: "review", label: "待生效" },
  { value: "expiring", label: "即期換版" },
  { value: "obsolete", label: "已作廢" },
];

function parseDocumentStatus(value: string): DocumentStatus | null {
  if (
    value === "active" ||
    value === "review" ||
    value === "expiring" ||
    value === "obsolete"
  ) {
    return value;
  }

  return null;
}

export function DocumentFilterBar({
  value,
  onChange,
  categories,
  companies,
}: DocumentFilterBarProps) {
  const activeCount = [
    value.keyword.trim(),
    value.categoryId,
    value.companyId,
    value.status,
  ].filter(Boolean).length;

  const patch = (next: Partial<DocumentQuery>) => {
    onChange({
      ...value,
      ...next,
      page: 1,
    });
  };

  const clear = () => {
    onChange({
      keyword: "",
      categoryId: null,
      companyId: null,
      status: null,
      page: 1,
      pageSize: value.pageSize,
    });
  };

  return (
    <div className="border-b border-line bg-surface px-4 py-3">
      <div className="grid grid-cols-[minmax(220px,1fr)_160px_160px_144px] gap-2 max-lg:grid-cols-2 max-md:grid-cols-1">
        <label className="grid gap-1">
          <span className="text-label text-ink">關鍵字</span>
          <input
            className="h-control rounded-sm border border-line bg-surface px-2 text-control focus:border-primary focus:ring-1 focus:ring-primary"
            type="search"
            value={value.keyword}
            placeholder="文件編號或名稱"
            onChange={(event) => patch({ keyword: event.target.value })}
          />
        </label>

        <label className="grid gap-1">
          <span className="text-label text-ink">類別</span>
          <select
            className="h-control rounded-sm border border-line bg-surface px-2 text-control"
            value={value.categoryId ?? ""}
            onChange={(event) =>
              patch({ categoryId: event.target.value || null })
            }
          >
            <option value="">全部類別</option>
            {categories.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>
        </label>

        <label className="grid gap-1">
          <span className="text-label text-ink">公司別</span>
          <select
            className="h-control rounded-sm border border-line bg-surface px-2 text-control"
            value={value.companyId ?? ""}
            onChange={(event) =>
              patch({ companyId: event.target.value || null })
            }
          >
            <option value="">全部公司</option>
            {companies.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>
        </label>

        <label className="grid gap-1">
          <span className="text-label text-ink">生效狀態</span>
          <select
            className="h-control rounded-sm border border-line bg-surface px-2 text-control"
            value={value.status ?? ""}
            onChange={(event) =>
              patch({
                status: parseDocumentStatus(event.target.value),
              })
            }
          >
            <option value="">全部狀態</option>
            {STATUS_OPTIONS.map((item) => (
              <option key={item.value} value={item.value}>
                {item.label}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="mt-2 flex min-h-9 items-center justify-between gap-3 text-meta text-ink-muted">
        <span>已套用 {activeCount} 項條件</span>
        <button
          type="button"
          className="h-control-sm rounded-sm px-2 text-control hover:bg-surface-header hover:text-primary"
          onClick={clear}
        >
          清除
        </button>
      </div>
    </div>
  );
}
