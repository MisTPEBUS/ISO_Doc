import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import {
  AppHeader,
  Button,
  Input,
  Pagination,
  Select,
  Table,
  type TableColumn,
} from "@/components/common";
import { useCurrentUser, useLogout } from "@/features/auth/queries";
import { USER_ROLE, USER_ROLE_LABEL } from "@/features/auth/types";
import {
  useAvailableDocuments,
  useDownloadAttachment,
  useDownloadDocument,
} from "@/features/documents/queries";
import type {
  AvailableDocumentAttachment,
  AvailableDocumentResponse,
  ListAvailableDocumentsParams,
} from "@/features/documents/types";
import { useIsoCategories } from "@/features/iso-categories/queries";

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 350;

function formatDate(value: string | null): string {
  return value?.replaceAll("-", "/") ?? "－";
}

function displayAttachmentNo(value: string | null | undefined): string {
  if (value === null || value === undefined) return "";
  return value.trim().toLowerCase() === "null" ? "" : value;
}

function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.detail ?? error.title ?? fallback;
  }

  return fallback;
}

export function HomePage() {
  const navigate = useNavigate();
  const currentUser = useCurrentUser();
  const logoutMutation = useLogout();
  const downloadDocumentMutation = useDownloadDocument();
  const downloadAttachmentMutation = useDownloadAttachment();
  const [keyword, setKeyword] = useState("");
  const [debouncedKeyword, setDebouncedKeyword] = useState("");
  const [isoCategoryId, setIsoCategoryId] = useState("");
  const [page, setPage] = useState(1);
  const [notice, setNotice] = useState<string>();
  const [expandedDocumentIds, setExpandedDocumentIds] = useState<
    ReadonlySet<string>
  >(() => new Set());

  useEffect(() => {
    const timer = window.setTimeout(
      () => setDebouncedKeyword(keyword.trim()),
      SEARCH_DEBOUNCE_MS,
    );
    return () => window.clearTimeout(timer);
  }, [keyword]);

  const queryParams = useMemo<ListAvailableDocumentsParams>(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      keyword: debouncedKeyword || undefined,
      isoCategoryId: isoCategoryId || undefined,
    }),
    [debouncedKeyword, isoCategoryId, page],
  );
  const documentsQuery = useAvailableDocuments(queryParams);
  const documents = documentsQuery.data?.items ?? [];
  const totalCount = documentsQuery.data?.totalCount ?? 0;
  const isoCategoriesQuery = useIsoCategories(
    { companyId: currentUser.data?.companyId, page: 1, pageSize: 100 },
    currentUser.data?.companyId !== undefined,
  );

  useEffect(() => {
    if (
      documentsQuery.error instanceof ApiError &&
      documentsQuery.error.status === 401
    ) {
      navigate("/login", { replace: true });
    }
  }, [documentsQuery.error, navigate]);

  function updateKeyword(value: string) {
    setKeyword(value);
    setPage(1);
    setNotice(undefined);
    setExpandedDocumentIds(new Set());
  }

  function updateIsoCategoryFilter(value: string) {
    setIsoCategoryId(value);
    setPage(1);
    setNotice(undefined);
    setExpandedDocumentIds(new Set());
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

  function handleRequestError(error: unknown, fallback: string) {
    if (error instanceof ApiError && error.status === 401) {
      navigate("/login", { replace: true });
      return;
    }

    setNotice(errorMessage(error, fallback));
  }

  function handleDocumentDownload(document: AvailableDocumentResponse) {
    if (!document.currentVersion.hasFile) return;

    setNotice(undefined);
    downloadDocumentMutation.mutate(
      {
        documentId: document.documentId,
        versionId: document.currentVersion.versionId,
        fallbackFileName: `${document.documentNo}.pdf`,
      },
      {
        onSuccess: (result) => setNotice(`已下載「${result.fileName}」。`),
        onError: (error) =>
          handleRequestError(error, "ISO管理程序下載失敗，請稍後再試。"),
      },
    );
  }

  function handleAttachmentDownload(attachment: AvailableDocumentAttachment) {
    const version = attachment.currentVersion;
    if (version?.hasFile !== true) return;

    setNotice(undefined);
    downloadAttachmentMutation.mutate(
      {
        attachmentId: attachment.attachmentId,
        versionId: version.versionId,
        fallbackFileName: attachment.name,
      },
      {
        onSuccess: (result) => setNotice(`已下載「${result.fileName}」。`),
        onError: (error) =>
          handleRequestError(error, "表單及附件下載失敗，請稍後再試。"),
      },
    );
  }

  function handleLogout() {
    if (logoutMutation.isPending) return;

    setNotice(undefined);
    logoutMutation.mutate(undefined, {
      onSuccess: () => navigate("/login", { replace: true }),
      onError: (error) => {
        setNotice(errorMessage(error, "目前無法連線到系統，請稍後再試。"));
      },
    });
  }

  const attachmentColumns: ReadonlyArray<
    TableColumn<AvailableDocumentAttachment>
  > = [
    {
      key: "status",
      header: "檔案狀態",
      headerClassName: "w-28",
      render: (attachment) => {
        const hasFile = attachment.currentVersion?.hasFile === true;
        return (
          <span
            className={
              hasFile
                ? "text-label font-medium text-state-active"
                : "text-label text-state-expiring"
            }
          >
            {hasFile ? "可下載" : "待補檔"}
          </span>
        );
      },
    },
    {
      key: "attachmentNo",
      header: "表單及附件編號",
      headerClassName: "w-44",
      cellClassName: "font-mono text-code tabular",
      render: (attachment) => displayAttachmentNo(attachment.attachmentNo),
    },
    {
      key: "name",
      header: "表單及附件名稱",
      render: (attachment) => (
        <span className="font-medium">{attachment.name}</span>
      ),
    },
    {
      key: "version",
      header: "版本",
      headerClassName: "w-20",
      cellClassName: "font-mono text-revision tabular",
      render: (attachment) =>
        attachment.currentVersion
          ? `V${attachment.currentVersion.version}`
          : "－",
    },
    {
      key: "download",
      header: "操作",
      headerClassName: "w-24",
      render: (attachment) => {
        const canDownload = attachment.currentVersion?.hasFile === true;
        const isDownloading =
          downloadAttachmentMutation.isPending &&
          downloadAttachmentMutation.variables?.attachmentId ===
            attachment.attachmentId;

        return canDownload ? (
          <button
            type="button"
            className="h-control-sm rounded-sm px-2 text-control font-medium text-primary hover:bg-primary-subtle disabled:cursor-wait disabled:text-ink-disabled"
            disabled={isDownloading}
            onClick={() => handleAttachmentDownload(attachment)}
          >
            {isDownloading ? "下載中" : "下載"}
          </button>
        ) : (
          <span className="text-ink-disabled">－</span>
        );
      },
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
        render: (document) => {
          const isDownloading =
            downloadDocumentMutation.isPending &&
            downloadDocumentMutation.variables?.documentId ===
              document.documentId;

          return document.currentVersion.hasFile ? (
            <button
              type="button"
              className="h-control-sm rounded-sm px-1 text-left text-cell font-medium text-primary hover:underline disabled:cursor-wait disabled:text-ink-disabled"
              disabled={isDownloading}
              onClick={() => handleDocumentDownload(document)}
            >
              {isDownloading ? "下載中…" : document.name}
            </button>
          ) : (
            <span className="font-medium text-ink-disabled">
              {document.name}
            </span>
          );
        },
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
      {
        key: "deptName",
        header: "發行單位",
        headerClassName: "w-40",
        cellClassName: "text-meta text-ink-muted",
        render: (document) => document.deptName ?? "",
      },
      {
        key: "isoCategoryName",
        header: "品質系統",
        headerClassName: "w-40",
        cellClassName: "text-meta text-ink-muted",
        render: (document) => document.isoCategoryName ?? "－",
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
        changePasswordHref={currentUser.data ? "/change-password" : null}
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
            <p className="tabular text-page-title text-ink">{totalCount}</p>
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
                placeholder="搜尋文件編號或名稱"
                value={keyword}
                onChange={(event) => updateKeyword(event.target.value)}
              />
            </div>
            <div className="min-w-48">
              <label
                className="mb-1 block text-label text-ink"
                htmlFor="document-iso-category"
              >
                品質系統
              </label>
              <Select
                id="document-iso-category"
                value={isoCategoryId}
                disabled={isoCategoriesQuery.isPending}
                onChange={(event) =>
                  updateIsoCategoryFilter(event.target.value)
                }
              >
                <option value="">全部分類</option>
                {isoCategoriesQuery.data?.items.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </Select>
            </div>
            {keyword.length > 0 && (
              <Button variant="secondary" onClick={() => updateKeyword("")}>
                清除
              </Button>
            )}
            <Button
              variant="secondary"
              loading={documentsQuery.isFetching}
              loadingText="載入中"
              onClick={() => {
                setNotice(undefined);
                setExpandedDocumentIds(new Set());
                void documentsQuery.refetch();
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

          {documentsQuery.isError && documentsQuery.data === undefined && (
            <div
              className="border-b border-line bg-danger-subtle px-4 py-3 text-meta text-danger"
              role="alert"
            >
              {errorMessage(
                documentsQuery.error,
                "文件清單載入失敗，請稍後再試。",
              )}
            </div>
          )}

          <Table
            className="border-0"
            columns={documentColumns}
            data={documents}
            loading={documentsQuery.isPending}
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
              canExpand: (document) => document.attachments.length > 0,
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
                      共 {document.attachments.length} 筆
                    </span>
                  </div>
                  <Table
                    className="border-line"
                    columns={attachmentColumns}
                    data={document.attachments}
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
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            onPageChange={(nextPage) => {
              setPage(nextPage);
              setNotice(undefined);
              setExpandedDocumentIds(new Set());
            }}
          />
        </section>
      </main>
    </div>
  );
}

export default HomePage;
