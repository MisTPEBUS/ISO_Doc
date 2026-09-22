import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import {
  AppHeader,
  Button,
  Input,
  Pagination,
  Table,
  type TableColumn,
} from "@/components/common";
import { useCurrentUser, useLogout } from "@/features/auth/queries";
import { USER_ROLE, USER_ROLE_LABEL } from "@/features/auth/types";
import {
  MOCK_ATTACHMENTS_BY_DOCUMENT,
  MOCK_AVAILABLE_DOCUMENTS,
  type MockDocumentAttachment,
} from "@/features/documents/mockData";
import type { AvailableDocumentResponse } from "@/features/documents/types";

const PAGE_SIZE = 6;

function formatDate(value: string | null): string {
  return value?.replaceAll("-", "/") ?? "－";
}

export function UiKitPage() {
  const navigate = useNavigate();
  const currentUser = useCurrentUser();
  const logoutMutation = useLogout();
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [notice, setNotice] = useState<string>();
  const [isLoading, setIsLoading] = useState(true);
  const [expandedDocumentIds, setExpandedDocumentIds] = useState<
    ReadonlySet<string>
  >(() => new Set());

  useEffect(() => {
    if (!isLoading) return;

    const timer = window.setTimeout(() => setIsLoading(false), 650);
    return () => window.clearTimeout(timer);
  }, [isLoading]);

  const filteredDocuments = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLocaleLowerCase("zh-TW");
    if (normalizedKeyword.length === 0) return MOCK_AVAILABLE_DOCUMENTS;

    return MOCK_AVAILABLE_DOCUMENTS.filter((document) =>
      [document.documentNo, document.name, document.companyName].some((value) =>
        value.toLocaleLowerCase("zh-TW").includes(normalizedKeyword),
      ),
    );
  }, [keyword]);

  const pageCount = Math.max(
    1,
    Math.ceil(filteredDocuments.length / PAGE_SIZE),
  );
  const currentPage = Math.min(page, pageCount);
  const pageDocuments = filteredDocuments.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE,
  );

  function attachmentsFor(
    documentId: string,
  ): ReadonlyArray<MockDocumentAttachment> {
    return MOCK_ATTACHMENTS_BY_DOCUMENT[documentId] ?? [];
  }

  function updateKeyword(value: string) {
    setKeyword(value);
    setPage(1);
    setNotice(undefined);
    setExpandedDocumentIds(new Set());
  }

  function previewDownload(label: string) {
    setNotice(`假資料預覽：正式串接後將下載「${label}」。`);
  }

  function toggleDocument(document: AvailableDocumentResponse) {
    setExpandedDocumentIds((current) => {
      const next = new Set(current);
      if (next.has(document.documentId)) {
        next.delete(document.documentId);
      } else {
        next.add(document.documentId);
      }
      return next;
    });
  }

  function handleLogout() {
    if (logoutMutation.isPending) return;

    setNotice(undefined);
    logoutMutation.mutate(undefined, {
      onSuccess: () => navigate("/login", { replace: true }),
      onError: (error) => {
        setNotice(
          error instanceof ApiError
            ? (error.detail ?? "登出失敗，請稍後再試。")
            : "目前無法連線到系統，請稍後再試。",
        );
      },
    });
  }

  const attachmentColumns: ReadonlyArray<TableColumn<MockDocumentAttachment>> =
    [
      {
        key: "status",
        header: "檔案狀態",
        headerClassName: "w-28",
        render: (attachment) => (
          <span
            className={
              attachment.hasFile
                ? "text-label font-medium text-state-active"
                : "text-label text-state-expiring"
            }
          >
            {attachment.hasFile ? "可下載" : "待補檔"}
          </span>
        ),
      },
      {
        key: "attachmentNo",
        header: "表單及附件編號",
        headerClassName: "w-44",
        cellClassName: "font-mono text-code tabular",
        render: (attachment) => attachment.attachmentNo,
      },
      {
        key: "name",
        header: "表單及附件名稱",
        render: (attachment) => (
          <span className="font-medium">{attachment.name}</span>
        ),
      },
      {
        key: "download",
        header: "操作",
        headerClassName: "w-24",
        render: (attachment) =>
          attachment.hasFile ? (
            <button
              type="button"
              className="h-control-sm rounded-sm px-2 text-control font-medium text-primary hover:bg-primary-subtle"
              onClick={() =>
                previewDownload(`${attachment.attachmentNo} ${attachment.name}`)
              }
            >
              下載
            </button>
          ) : (
            <span className="text-ink-disabled">－</span>
          ),
      },
    ];

  const documentColumns: ReadonlyArray<TableColumn<AvailableDocumentResponse>> =
    [
      {
        key: "status",
        header: "狀態",
        headerClassName: "w-28",
        render: () => (
          <span className="inline-flex items-center gap-1.5 text-label font-medium text-state-active">
            <span
              className="size-1.5 rounded-full bg-state-active"
              aria-hidden="true"
            />
            已發布
          </span>
        ),
      },
      {
        key: "documentNo",
        header: "文件編號",
        headerClassName: "w-48",
        cellClassName: "font-mono text-code tabular",
        render: (document, _rowIndex, context) => (
          <div className="flex items-center">
            {context.expandable ? (
              <button
                type="button"
                className="mr-1 grid size-8 shrink-0 place-items-center rounded-sm text-primary hover:bg-primary-subtle"
                aria-expanded={context.expanded}
                aria-label={`${context.expanded ? "收合" : "展開"} ${document.documentNo} 表單及附件清單`}
                onClick={context.toggleExpansion}
              >
                <span
                  className={`text-control transition-transform ${context.expanded ? "rotate-90" : ""}`}
                  aria-hidden="true"
                >
                  ›
                </span>
              </button>
            ) : (
              <span className="mr-1 size-8 shrink-0" aria-hidden="true" />
            )}
            {document.documentNo}
          </div>
        ),
      },
      {
        key: "name",
        header: "名稱",
        headerClassName: "min-w-72",
        render: (document) => (
          <button
            type="button"
            className="h-control-sm rounded-sm px-1 text-left text-cell font-medium text-primary hover:underline"
            onClick={() =>
              previewDownload(`${document.documentNo} ${document.name} PDF`)
            }
          >
            {document.name}
          </button>
        ),
      },
      {
        key: "pageCount",
        header: "頁數",
        headerClassName: "w-20 text-right",
        cellClassName: "text-right text-meta text-ink-muted tabular",
        render: (document) => document.currentVersion.pageCount ?? "－",
      },
      {
        key: "version",
        header: "版本",
        headerClassName: "w-20",
        cellClassName: "font-mono text-revision tabular",
        render: (document) => `V${document.currentVersion.version}`,
      },
      {
        key: "effectiveDate",
        header: "生效日期",
        headerClassName: "w-36",
        cellClassName: "text-meta text-ink-muted tabular",
        render: (document) => formatDate(document.currentVersion.effectiveDate),
      },
      {
        key: "companyName",
        header: "公司別",
        headerClassName: "w-56",
        cellClassName: "text-meta text-ink-muted",
        render: (document) => document.companyName,
      },
    ];

  return (
    <div className="min-h-screen bg-canvas">
      <AppHeader
        companyName={currentUser.data?.companyName}
        departmentName={currentUser.data?.deptName}
        userName={currentUser.data?.name ?? "使用者"}
        roleLabel={
          currentUser.data ? USER_ROLE_LABEL[currentUser.data.role] : undefined
        }
        modeLink={
          currentUser.data?.role !== undefined &&
          currentUser.data.role !== USER_ROLE.User
            ? {
                label: "管理介面",
                href: "/admin/documents",
              }
            : undefined
        }
        changePasswordHref={
          currentUser.data ? "/change-password" : null
        }
        logoutLabel={logoutMutation.isPending ? "登出中" : "登出"}
        onLogout={handleLogout}
      />

      <main className="w-full px-3 py-5 sm:px-4 lg:px-6">
        <section className="mb-4 flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="mb-1 text-label font-medium text-primary">文件中心</p>
            <h1 className="text-page-title text-ink">ISO文件列表</h1>
            <p className="mt-1 text-meta text-ink-muted">
              查詢目前已發布 ISO 文件。
            </p>
          </div>
          <div className="border-l-2 border-primary pl-3 text-right">
            <p className="text-fine text-ink-muted">ISO文件列表</p>
            <p className="tabular text-page-title text-ink">
              {MOCK_AVAILABLE_DOCUMENTS.length}
            </p>
          </div>
        </section>

        <section
          className="border border-line-strong bg-surface"
          aria-label="文件清單"
        >
          <div className="flex flex-wrap items-end gap-3 border-b border-line bg-surface px-4 py-3">
            <div className="min-w-64 flex-1 sm:max-w-sm">
              <label
                className="mb-1 block text-label text-ink"
                htmlFor="document-keyword"
              >
                關鍵字
              </label>
              <Input
                id="document-keyword"
                type="search"
                placeholder="搜尋文件編號、名稱或公司"
                value={keyword}
                onChange={(event) => updateKeyword(event.target.value)}
              />
            </div>
            {keyword.length > 0 && (
              <Button variant="secondary" onClick={() => updateKeyword("")}>
                清除
              </Button>
            )}
            <Button
              variant="secondary"
              loading={isLoading}
              loadingText="載入中"
              onClick={() => {
                setNotice(undefined);
                setExpandedDocumentIds(new Set());
                setIsLoading(true);
              }}
            >
              重新整理
            </Button>
          </div>

          {notice && (
            <div
              className="border-b border-line bg-primary-subtle px-4 py-2.5 text-meta text-primary"
              role="status"
            >
              {notice}
            </div>
          )}

          <Table
            className="border-0"
            columns={documentColumns}
            data={isLoading ? [] : pageDocuments}
            loading={isLoading}
            skeletonRows={PAGE_SIZE}
            getRowKey={(document) => document.documentId}
            caption="目前使用者可閱讀的 ISO 文件"
            emptyMessage={
              <div className="py-5">
                <p className="font-medium text-ink">沒有符合條件的文件</p>
                <p className="mt-1 text-meta">
                  請調整關鍵字，或清除目前的搜尋條件。
                </p>
                <Button
                  className="mt-4"
                  variant="secondary"
                  onClick={() => updateKeyword("")}
                >
                  清除搜尋
                </Button>
              </div>
            }
            rowClassName={(_document, index) =>
              index % 2 === 1 ? "bg-surface-zebra" : undefined
            }
            expansion={{
              canExpand: (document) =>
                attachmentsFor(document.documentId).length > 0,
              isExpanded: (document) =>
                expandedDocumentIds.has(document.documentId),
              onToggle: toggleDocument,
              toggleOnRowClick: true,
              render: (document) => (
                <div>
                  <div className="mb-2 flex items-center justify-between gap-3">
                    <h2 className="text-section-label text-ink">
                      {document.documentNo} 表單及附件清單
                    </h2>
                    <span className="text-meta text-ink-muted">
                      共 {attachmentsFor(document.documentId).length} 筆
                    </span>
                  </div>
                  <Table
                    className="border-line"
                    columns={attachmentColumns}
                    data={attachmentsFor(document.documentId)}
                    getRowKey={(attachment) => attachment.attachmentId}
                    emptyMessage="此版本沒有表單及附件"
                    caption={`${document.documentNo} 表單及附件清單`}
                  />
                </div>
              ),
            }}
          />

          <Pagination
            className="border-t border-line px-4"
            page={currentPage}
            pageSize={PAGE_SIZE}
            totalCount={filteredDocuments.length}
            onPageChange={(nextPage) => {
              setPage(nextPage);
              setNotice(undefined);
              setExpandedDocumentIds(new Set());
            }}
          />
        </section>

        <p className="mt-3 text-fine text-ink-muted">
          目前使用 API 同欄位假資料預覽；表單及附件列為展開元件示意，正式表單及附件清單仍待
          API 提供。
        </p>
      </main>
    </div>
  );
}

export default UiKitPage;
