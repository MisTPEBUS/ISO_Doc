import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import {
  Alert,
  Badge,
  Button,
  FormField,
  Input,
  Modal,
  Pagination,
  Select,
  Table,
  type TableColumn,
} from "@/components/common";
import {
  useAdminDocuments,
  useCreateAdminDocument,
  useDeleteAdminDocument,
  useUpdateAdminDocument,
} from "@/features/admin-documents/queries";
import {
  createAdminDocumentFormSchema,
  updateAdminDocumentFormSchema,
  type AdminDocumentFormValues,
} from "@/features/admin-documents/schemas";
import type { AdminDocument } from "@/features/admin-documents/types";
import { useCurrentUser } from "@/features/auth/queries";
import { USER_ROLE } from "@/features/auth/types";
import { useCompanies } from "@/features/companies/queries";

const DEFAULT_PAGE_SIZE = 10;
const EMPTY_FORM: AdminDocumentFormValues = { documentNo: "", name: "" };

type FieldErrors = Partial<Record<keyof AdminDocumentFormValues, string>>;

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0];
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("zh-TW", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Asia/Taipei",
  }).format(new Date(value));
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError
    ? (error.detail ?? fallback)
    : "目前無法連線到系統，請稍後再試。";
}

export function AdminDocumentsPage() {
  const navigate = useNavigate();
  const currentUser = useCurrentUser();
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin;
  const [selectedCompanyId, setSelectedCompanyId] = useState(
    currentUser.data?.companyId ?? "",
  );
  const [keywordInput, setKeywordInput] = useState("");
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [editingDocument, setEditingDocument] = useState<AdminDocument>();
  const [formOpen, setFormOpen] = useState(false);
  const [formValues, setFormValues] =
    useState<AdminDocumentFormValues>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [deleteTarget, setDeleteTarget] = useState<AdminDocument>();
  const [deleteError, setDeleteError] = useState<string>();
  const [successMessage, setSuccessMessage] = useState<string>();

  const companyId = isSystemAdmin
    ? selectedCompanyId || undefined
    : currentUser.data?.companyId;
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin);
  const documents = useAdminDocuments({
    companyId,
    keyword: keyword || undefined,
    page,
    pageSize,
  });

  function handlePageSizeChange(nextPageSize: number) {
    setPageSize(nextPageSize);
    setPage(1);
  }

  const createDocument = useCreateAdminDocument();
  const updateDocument = useUpdateAdminDocument();
  const deleteDocument = useDeleteAdminDocument();
  const formPending = createDocument.isPending || updateDocument.isPending;
  const selectedCompany = companies.data?.items.find(
    (company) => company.id === companyId,
  );

  function openCreateForm() {
    setEditingDocument(undefined);
    setFormValues(EMPTY_FORM);
    setFieldErrors({});
    setFormError(undefined);
    setSuccessMessage(undefined);
    setFormOpen(true);
  }

  function openEditForm(document: AdminDocument) {
    setEditingDocument(document);
    setFormValues({ documentNo: document.documentNo, name: document.name });
    setFieldErrors({});
    setFormError(undefined);
    setSuccessMessage(undefined);
    setFormOpen(true);
  }

  function closeForm() {
    if (!formPending) setFormOpen(false);
  }

  function updateField(field: keyof AdminDocumentFormValues, value: string) {
    setFormValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError(undefined);
  }

  function applyServerError(error: ApiError) {
    const errors = error.fieldErrors();
    setFieldErrors({
      documentNo: firstMessage(errors.documentNo),
      name: firstMessage(errors.name),
    });
    if (Object.keys(errors).length === 0) {
      setFormError(errorMessage(error, "無法儲存文件資料。"));
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    if (editingDocument) {
      const parsed = updateAdminDocumentFormSchema.safeParse(formValues);
      if (!parsed.success) {
        setFieldErrors({
          name: firstMessage(parsed.error.flatten().fieldErrors.name),
        });
        return;
      }

      updateDocument.mutate(
        {
          id: editingDocument.id,
          request: { name: parsed.data.name },
        },
        {
          onSuccess: () => {
            setFormOpen(false);
            setSuccessMessage("文件名稱已更新。");
          },
          onError: (error) => {
            if (error instanceof ApiError) applyServerError(error);
            else setFormError(errorMessage(error, "無法更新文件。"));
          },
        },
      );
      return;
    }

    if (companyId === undefined) {
      setFormError("請先選擇公司。");
      return;
    }

    const parsed = createAdminDocumentFormSchema.safeParse(formValues);
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors;
      setFieldErrors({
        documentNo: firstMessage(errors.documentNo),
        name: firstMessage(errors.name),
      });
      return;
    }

    createDocument.mutate(
      {
        companyId,
        documentNo: parsed.data.documentNo,
        name: parsed.data.name,
      },
      {
        onSuccess: () => {
          setFormOpen(false);
          setSuccessMessage("ISO 文件主檔已建立。");
        },
        onError: (error) => {
          if (error instanceof ApiError) applyServerError(error);
          else setFormError(errorMessage(error, "無法建立文件。"));
        },
      },
    );
  }

  function confirmDelete() {
    if (!deleteTarget) return;

    setDeleteError(undefined);
    deleteDocument.mutate(deleteTarget.id, {
      onSuccess: () => {
        setDeleteTarget(undefined);
        setSuccessMessage("文件已停用。");
      },
      onError: (error) => setDeleteError(errorMessage(error, "無法停用文件。")),
    });
  }

  function clearKeyword() {
    setKeywordInput("");
    setKeyword("");
    setPage(1);
  }

  const columns: ReadonlyArray<TableColumn<AdminDocument>> = [
    {
      key: "status",
      header: "狀態",
      headerClassName: "w-24",
      render: (document) => (
        <Badge variant={document.isActive ? "success" : "danger"}>
          {document.isActive ? "啟用" : "停用"}
        </Badge>
      ),
    },
    {
      key: "documentNo",
      header: "文件編號",
      cellClassName: "font-mono text-code tabular",
      render: (document) => (
        <button
          type="button"
          className="rounded-xs font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          onClick={() => navigate(`/admin/documents/${document.id}`)}
        >
          {document.documentNo}
        </button>
      ),
    },
    {
      key: "name",
      header: "文件名稱",
      cellClassName: "min-w-64 font-medium",
      render: (document) => (
        <button
          type="button"
          className="rounded-xs text-left text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          onClick={() => navigate(`/admin/documents/${document.id}`)}
        >
          {document.name}
        </button>
      ),
    },
    {
      key: "updatedAt",
      header: "最後更新",
      headerClassName: "w-52",
      cellClassName: "text-meta text-ink-muted tabular whitespace-nowrap",
      render: (document) => formatDateTime(document.updatedAt),
    },
    {
      key: "actions",
      header: "操作",
      headerClassName: "w-60",
      render: (document) => (
        <div className="flex items-center gap-1">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => navigate(`/admin/documents/${document.id}`)}
          >
            詳情
          </Button>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => openEditForm(document)}
          >
            編輯
          </Button>
          {document.isActive && (
            <Button
              size="sm"
              variant="ghost"
              className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
              onClick={() => {
                setDeleteError(undefined);
                setDeleteTarget(document);
              }}
            >
              停用
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <section>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="mb-1 text-label font-medium text-primary">文件管理</p>
          <h1 className="text-page-title text-ink">ISO 文件維護</h1>
          <p className="mt-1 text-meta text-ink-muted">
            管理 ISO 文件主檔並檢視版本歷程。
          </p>
        </div>
        <Button disabled={companyId === undefined} onClick={openCreateForm}>
          新增文件
        </Button>
      </div>

      {successMessage && (
        <Alert
          className="mb-4"
          variant="success"
          title="操作完成"
          dismissAfterMs={3000}
          onDismiss={() => setSuccessMessage(undefined)}
        >
          {successMessage}
        </Alert>
      )}

      {(documents.isError || (isSystemAdmin && companies.isError)) && (
        <Alert className="mb-4" variant="error" title="無法載入文件資料">
          {errorMessage(
            documents.error ?? companies.error,
            "請稍後重新整理頁面。",
          )}
        </Alert>
      )}

      <form
        className="border border-line-strong bg-surface p-4"
        onSubmit={(event) => {
          event.preventDefault();
          setKeyword(keywordInput.trim());
          setPage(1);
        }}
      >
        <div className="flex flex-wrap items-end gap-3">
          {isSystemAdmin && (
            <FormField
              className="w-full md:w-72"
              label="公司"
              htmlFor="document-company-filter"
            >
              <Select
                id="document-company-filter"
                value={selectedCompanyId}
                disabled={companies.isPending || companies.isError}
                onChange={(event) => {
                  setSelectedCompanyId(event.target.value);
                  setPage(1);
                }}
              >
                {companies.isPending && <option value="">公司載入中</option>}
                {companies.data?.items.map((company) => (
                  <option key={company.id} value={company.id}>
                    {company.code} — {company.name}
                  </option>
                ))}
              </Select>
            </FormField>
          )}
          <FormField
            className="min-w-64 flex-1"
            label="關鍵字"
            htmlFor="document-keyword"
          >
            <Input
              id="document-keyword"
              value={keywordInput}
              placeholder="輸入文件編號或名稱"
              onChange={(event) => setKeywordInput(event.target.value)}
            />
          </FormField>
          <Button type="submit" variant="secondary">
            搜尋
          </Button>
        </div>
      </form>

      <div className="border-x border-b border-line-strong bg-surface">
        <div className="flex h-row items-center justify-between border-b border-line px-4">
          <p className="text-meta text-ink-muted">
            共{" "}
            <span className="tabular font-medium text-ink">
              {documents.data?.totalCount ?? 0}
            </span>{" "}
            份文件
          </p>
          <Button
            variant="secondary"
            size="sm"
            loading={documents.isFetching}
            loadingText="載入中"
            onClick={() => void documents.refetch()}
          >
            重新整理
          </Button>
        </div>
        <Table
          className="border-0"
          columns={columns}
          data={documents.data?.items ?? []}
          loading={documents.isPending}
          skeletonRows={6}
          getRowKey={(document) => document.id}
          caption="ISO 文件主檔清單"
          emptyMessage={
            <div className="py-5">
              <p className="font-medium text-ink">
                {keyword ? "沒有符合條件的文件" : "尚未建立任何 ISO 文件"}
              </p>
              <p className="mt-1 text-meta">
                {keyword
                  ? "請調整文件編號或名稱後再試一次。"
                  : "建立文件主檔後，即可進入詳情檢視版本歷程。"}
              </p>
              {keyword ? (
                <Button
                  className="mt-4"
                  variant="secondary"
                  onClick={clearKeyword}
                >
                  清除搜尋
                </Button>
              ) : (
                <Button
                  className="mt-4"
                  variant="secondary"
                  onClick={openCreateForm}
                >
                  新增文件
                </Button>
              )}
            </div>
          }
        />
        <Pagination
          className="border-t border-line px-4"
          page={documents.data?.page ?? page}
          pageSize={pageSize}
          totalCount={documents.data?.totalCount ?? 0}
          onPageChange={setPage}
          onPageSizeChange={handlePageSizeChange}
        />
      </div>

      <Modal
        open={formOpen}
        onClose={closeForm}
        title={editingDocument ? "編輯文件" : "新增文件"}
        description={
          editingDocument
            ? "文件編號建立後不可修改。"
            : "先建立 ISO 文件主檔；版本與檔案將於後續步驟新增。"
        }
        footer={
          <>
            <Button
              variant="secondary"
              disabled={formPending}
              onClick={closeForm}
            >
              取消
            </Button>
            <Button
              type="submit"
              form="admin-document-form"
              loading={formPending}
              loadingText="儲存中"
            >
              儲存
            </Button>
          </>
        }
      >
        {formError && (
          <Alert className="mb-4" variant="error">
            {formError}
          </Alert>
        )}
        <form
          id="admin-document-form"
          className="space-y-4"
          noValidate
          onSubmit={handleSubmit}
        >
          <FormField label="公司">
            <div className="flex h-control items-center border border-line bg-surface-header px-2 text-control text-ink-muted">
              {isSystemAdmin
                ? selectedCompany
                  ? `${selectedCompany.code} — ${selectedCompany.name}`
                  : "請先選擇公司"
                : (currentUser.data?.companyName ?? "目前公司")}
            </div>
          </FormField>
          <FormField
            label="文件編號"
            htmlFor="admin-document-no"
            error={fieldErrors.documentNo}
            hint="限英文字母、數字與連字號，開頭與結尾須為英文字母或數字。"
            required
          >
            <Input
              id="admin-document-no"
              value={formValues.documentNo}
              readOnly={editingDocument !== undefined}
              className={
                editingDocument ? "bg-surface-header text-ink-muted" : undefined
              }
              error={fieldErrors.documentNo !== undefined}
              aria-describedby={
                fieldErrors.documentNo
                  ? "admin-document-no-error"
                  : "admin-document-no-hint"
              }
              onChange={(event) =>
                updateField("documentNo", event.target.value)
              }
            />
          </FormField>
          <FormField
            label="文件名稱"
            htmlFor="admin-document-name"
            error={fieldErrors.name}
            required
          >
            <Input
              id="admin-document-name"
              value={formValues.name}
              error={fieldErrors.name !== undefined}
              aria-describedby={
                fieldErrors.name ? "admin-document-name-error" : undefined
              }
              onChange={(event) => updateField("name", event.target.value)}
            />
          </FormField>
        </form>
      </Modal>

      <Modal
        open={deleteTarget !== undefined}
        onClose={() => {
          if (!deleteDocument.isPending) setDeleteTarget(undefined);
        }}
        size="sm"
        title="停用 ISO 文件"
        description="停用後，文件將不再提供一般使用者查閱；主檔與版本歷程仍會保留。"
        footer={
          <>
            <Button
              variant="secondary"
              disabled={deleteDocument.isPending}
              onClick={() => setDeleteTarget(undefined)}
            >
              取消
            </Button>
            <Button
              variant="danger"
              loading={deleteDocument.isPending}
              loadingText="停用中"
              onClick={confirmDelete}
            >
              停用文件
            </Button>
          </>
        }
      >
        {deleteError && (
          <Alert className="mb-4" variant="error">
            {deleteError}
          </Alert>
        )}
        <p className="text-cell text-ink">
          確定要停用「
          <strong className="font-semibold">
            {deleteTarget?.documentNo} {deleteTarget?.name}
          </strong>
          」嗎？
        </p>
      </Modal>
    </section>
  );
}

export default AdminDocumentsPage;
