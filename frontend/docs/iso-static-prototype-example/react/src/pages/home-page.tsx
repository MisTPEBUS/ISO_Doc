import { useEffect, useMemo, useState } from "react";
import { DocumentFilterBar } from "../features/documents/components/document-filter-bar";
import { DocumentTable } from "../features/documents/components/document-table";
import { PaginationBar } from "../components/ui/pagination-bar";
import type {
  DocumentQuery,
  IsoDocument,
  SelectOption,
} from "../features/documents/types";

export interface HomePageProps {
  documents: IsoDocument[];
  categories: SelectOption[];
  companies: SelectOption[];
  onDownload: (id: string) => Promise<void>;
}

const INITIAL_QUERY: DocumentQuery = {
  keyword: "",
  categoryId: null,
  companyId: null,
  status: null,
  page: 1,
  pageSize: 50,
};

export function HomePage({
  documents,
  categories,
  companies,
  onDownload,
}: HomePageProps) {
  const [query, setQuery] = useState<DocumentQuery>(INITIAL_QUERY);
  const [debouncedKeyword, setDebouncedKeyword] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedKeyword(query.keyword.trim().toLowerCase());
    }, 300);

    return () => window.clearTimeout(timer);
  }, [query.keyword]);

  const filtered = useMemo(
    () =>
      documents.filter((document) => {
        const keywordMatched =
          debouncedKeyword.length === 0 ||
          document.code.toLowerCase().includes(debouncedKeyword) ||
          document.title.toLowerCase().includes(debouncedKeyword);

        return (
          keywordMatched &&
          (!query.categoryId || document.categoryId === query.categoryId) &&
          (!query.companyId || document.companyId === query.companyId) &&
          (!query.status || document.status === query.status)
        );
      }),
    [documents, debouncedKeyword, query.categoryId, query.companyId, query.status],
  );

  const start = (query.page - 1) * query.pageSize;
  const rows = filtered.slice(start, start + query.pageSize);

  return (
    <main className="mx-auto w-[min(1440px,calc(100%-32px))] py-4">
      <section className="flex min-h-[72px] items-center justify-between pb-4">
        <div>
          <h1 className="text-page-title">ISO 文件查閱</h1>
          <p className="mt-1 text-label text-ink-muted">
            依文件編號、名稱、公司或狀態查詢目前可檢視文件。
          </p>
        </div>
      </section>

      <section className="border border-line-strong bg-surface">
        <DocumentFilterBar
          value={query}
          categories={categories}
          companies={companies}
          onChange={setQuery}
        />

        <DocumentTable rows={rows} onDownload={onDownload} />

        <PaginationBar
          total={filtered.length}
          page={query.page}
          pageSize={query.pageSize}
          onPageChange={(page) => setQuery((current) => ({ ...current, page }))}
          onPageSizeChange={(pageSize) =>
            setQuery((current) => ({ ...current, page: 1, pageSize }))
          }
        />
      </section>
    </main>
  );
}
