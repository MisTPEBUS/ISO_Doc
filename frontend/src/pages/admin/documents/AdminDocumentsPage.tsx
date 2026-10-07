import { useState, type FormEvent } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

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
  Textarea,
  toSortParams,
  type TableColumn,
  type TableSort,
} from "@/components/common";
import {
  useAdminDocuments,
  useCreateAdminDocumentWithVersion,
  useDeleteAdminDocument,
  useUpdateAdminDocument,
} from "@/features/admin-documents/queries";
import {
  createAdminDocumentWithVersionFormSchema,
  updateAdminDocumentFormSchema,
  type AdminDocumentWithVersionFormValues,
} from "@/features/admin-documents/schemas";
import {
  ADMIN_DOCUMENT_SORT_FIELD,
  type AdminDocument,
  type AdminDocumentSortField,
} from "@/features/admin-documents/types";
import { todayUtc } from "@/features/admin-documents/versionSchemas";
import { useCurrentUser } from "@/features/auth/queries";
import { USER_ROLE } from "@/features/auth/types";
import { useCompanies } from "@/features/companies/queries";
import { useDepts } from "@/features/departments/queries";
import { useIsoCategories } from "@/features/iso-categories/queries";
import { formatRocDateTime } from "@/lib/date";

const DEFAULT_PAGE_SIZE = 10;
const SORT_FIELDS = Object.values(ADMIN_DOCUMENT_SORT_FIELD) as string[];

function positiveInteger(value: string | null, fallback: number): number {
  const number = Number(value);
  return value !== null && Number.isSafeInteger(number) && number > 0
    ? number
    : fallback;
}

const EMPTY_FORM: AdminDocumentWithVersionFormValues = {
  documentNo: "",
  name: "",
  isoCategoryId: "",
  deptId: "",
  version: "1.0",
  effectiveDate: "",
  pageCount: "",
  memo: "",
  file: null,
};

type FieldErrors = Partial<
  Record<keyof AdminDocumentWithVersionFormValues, string>
>;

// 新增表單的品質系統預設值；比對時忽略空白與大小寫（例如「ISO 9001」也算）。
const DEFAULT_ISO_CATEGORY_NAME = "ISO9001";

function isDefaultIsoCategory(name: string): boolean {
  return name.replace(/\s+/g, "").toUpperCase() === DEFAULT_ISO_CATEGORY_NAME;
}

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0];
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError
    ? (error.detail ?? fallback)
    : "目前無法連線到系統，請稍後再試。";
}

export function AdminDocumentsPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const currentUser = useCurrentUser();
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin;
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin);
  const selectedCompanyId =
    searchParams.get("companyId") || companies.data?.items[0]?.id || "";
  const keyword = searchParams.get("keyword") ?? "";
  const [keywordInput, setKeywordInput] = useState(keyword);
  const page = positiveInteger(searchParams.get("page"), 1);
  const pageSize = positiveInteger(
    searchParams.get("pageSize"),
    DEFAULT_PAGE_SIZE,
  );
  const sortBy = searchParams.get("sortBy");
  const sortDirection = searchParams.get("sortDirection");
  const sort: TableSort<AdminDocumentSortField> | null =
    sortBy && SORT_FIELDS.includes(sortBy) &&
    (sortDirection === "asc" || sortDirection === "desc")
      ? { key: sortBy as AdminDocumentSortField, direction: sortDirection }
      : null;
  const [editingDocument, setEditingDocument] = useState<AdminDocument>();
  const [formOpen, setFormOpen] = useState(false);
  const [formKey, setFormKey] = useState(0);
  const [formValues, setFormValues] =
    useState<AdminDocumentWithVersionFormValues>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [deleteTarget, setDeleteTarget] = useState<AdminDocument>();
  const [deleteError, setDeleteError] = useState<string>();
  const [successMessage, setSuccessMessage] = useState<string>();

  const companyId = isSystemAdmin
    ? selectedCompanyId || undefined
    : currentUser.data?.companyId;
  const documents = useAdminDocuments({
    companyId,
    keyword: keyword || undefined,
    page,
    pageSize,
    ...toSortParams(sort),
  });
  const isoCategories = useIsoCategories(
    { companyId, page: 1, pageSize: 100 },
    companyId !== undefined,
  );
  const depts = useDepts(
    { companyId, page: 1, pageSize: 100 },
    companyId !== undefined,
  );
  const isoCategoryNameById = new Map(
    (isoCategories.data?.items ?? []).map((category) => [
      category.id,
      category.name,
    ]),
  );

  function updateListParams(changes: Record<string, string | null>) {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      for (const [key, value] of Object.entries(changes)) {
        if (value) next.set(key, value);
        else next.delete(key);
      }
      return next;
    });
  }

  function setPage(nextPage: number) {
    updateListParams({ page: nextPage === 1 ? null : String(nextPage) });
  }

  function handlePageSizeChange(nextPageSize: number) {
    updateListParams({ pageSize: String(nextPageSize), page: null });
  }

  function handleSortChange(nextSort: TableSort<AdminDocumentSortField> | null) {
    updateListParams({
      sortBy: nextSort?.key ?? null,
      sortDirection: nextSort?.direction ?? null,
      page: null,
    });
  }

  const createDocument = useCreateAdminDocumentWithVersion();
  const updateDocument = useUpdateAdminDocument();
  const deleteDocument = useDeleteAdminDocument();
  const formPending = createDocument.isPending || updateDocument.isPending;
  const selectedCompany = companies.data?.items.find(
    (company) => company.id === companyId,
  );

  function openCreateForm() {
    const defaultIsoCategoryId =
      isoCategories.data?.items.find((category) =>
        isDefaultIsoCategory(category.name),
      )?.id ?? "";
    setEditingDocument(undefined);
    setFormValues({
      ...EMPTY_FORM,
      isoCategoryId: defaultIsoCategoryId,
      effectiveDate: todayUtc(),
    });
    setFormKey((current) => current + 1);
    setFieldErrors({});
    setFormError(undefined);
    setSuccessMessage(undefined);
    setFormOpen(true);
  }

  function openEditForm(document: AdminDocument) {
    setEditingDocument(document);
    setFormValues({
      ...EMPTY_FORM,
      documentNo: document.documentNo,
      name: document.name,
      isoCategoryId: document.isoCategoryId ?? "",
      deptId: document.deptId ?? "",
    });
    setFieldErrors({});
    setFormError(undefined);
    setSuccessMessage(undefined);
    setFormOpen(true);
  }

  function closeForm() {
    if (!formPending) setFormOpen(false);
  }

  function updateField(
    field: Exclude<keyof AdminDocumentWithVersionFormValues, "file">,
    value: string,
  ) {
    setFormValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError(undefined);
  }

  function updateFile(file: File | null) {
    setFormValues((current) => ({ ...current, file }));
    setFieldErrors((current) => ({ ...current, file: undefined }));
    setFormError(undefined);
  }

  function applyServerError(error: ApiError) {
    const errors = error.fieldErrors();
    setFieldErrors({
      documentNo: firstMessage(errors.documentNo),
      name: firstMessage(errors.name),
      isoCategoryId: firstMessage(errors.isoCategoryId),
      deptId: firstMessage(errors.deptId),
      version: firstMessage(errors.version),
      effectiveDate: firstMessage(errors.effectiveDate),
      memo: firstMessage(errors.memo),
      file: firstMessage(errors.file),
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
        const errors = parsed.error.flatten().fieldErrors;
        setFieldErrors({
          name: firstMessage(errors.name),
          isoCategoryId: firstMessage(errors.isoCategoryId),
          deptId: firstMessage(errors.deptId),
        });
        return;
      }

      updateDocument.mutate(
        {
          id: editingDocument.id,
          request: {
            name: parsed.data.name,
            isoCategoryId: parsed.data.isoCategoryId || null,
            deptId: parsed.data.deptId || null,
          },
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

    const parsed =
      createAdminDocumentWithVersionFormSchema.safeParse(formValues);
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors;
      setFieldErrors({
        documentNo: firstMessage(errors.documentNo),
        name: firstMessage(errors.name),
        isoCategoryId: firstMessage(errors.isoCategoryId),
        deptId: firstMessage(errors.deptId),
        version: firstMessage(errors.version),
        effectiveDate: firstMessage(errors.effectiveDate),
        memo: firstMessage(errors.memo),
        file: firstMessage(errors.file),
      });
      return;
    }

    createDocument.mutate(
      { companyId, ...parsed.data },
      {
        onSuccess: (created) => {
          setFormOpen(false);
          setSuccessMessage(
            `ISO管理程序 ${created.document.documentNo} 已建立，版本 ${created.version.version} 已發布。`,
          );
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
    updateListParams({ keyword: null, page: null });
  }

  const columns: ReadonlyArray<
    TableColumn<AdminDocument, AdminDocumentSortField>
  > = [
    {
      key: "status",
      header: "文件狀態",
      headerClassName: "w-24",
      sortKey: ADMIN_DOCUMENT_SORT_FIELD.IsActive,
      render: (document) => (
        <Badge variant={document.isActive ? "success" : "danger"}>
          {document.isActive ? "啟用" : "停用"}
        </Badge>
      ),
    },
    {
      key: "documentNo",
      header: "文件編號",
      sortKey: ADMIN_DOCUMENT_SORT_FIELD.DocumentNo,
      cellClassName: "font-mono text-code tabular",
      render: (document) => (
        <button
          type="button"
          className="rounded-xs font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          onClick={() => navigate(`/admin/documents/${document.id}?${searchParams}`)}
        >
          {document.documentNo}
        </button>
      ),
    },
    {
      key: "name",
      header: "文件名稱",
      sortKey: ADMIN_DOCUMENT_SORT_FIELD.Name,
      cellClassName: "min-w-64 font-medium",
      render: (document) => (
        <button
          type="button"
          className="rounded-xs text-left text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          onClick={() => navigate(`/admin/documents/${document.id}?${searchParams}`)}
        >
          {document.name}
        </button>
      ),
    },
    {
      key: "isoCategory",
      header: "品質系統",
      sortKey: ADMIN_DOCUMENT_SORT_FIELD.IsoCategoryName,
      headerClassName: "w-40",
      cellClassName: "text-meta text-ink-muted",
      render: (document) =>
        document.isoCategoryId === null
          ? "－"
          : (isoCategoryNameById.get(document.isoCategoryId) ?? "－"),
    },
    {
      key: "dept",
      header: "發行單位",
      sortKey: ADMIN_DOCUMENT_SORT_FIELD.DeptName,
      headerClassName: "w-40",
      cellClassName: "text-meta text-ink-muted",
      render: (document) => document.deptName ?? "－",
    },
    {
      key: "updatedAt",
      header: "最後更新",
      sortKey: ADMIN_DOCUMENT_SORT_FIELD.UpdatedAt,
      headerClassName: "w-52",
      cellClassName: "text-meta text-ink-muted tabular whitespace-nowrap",
      render: (document) => formatRocDateTime(document.updatedAt),
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
        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            disabled={companyId === undefined}
            onClick={() => navigate("/admin/documents/attachments/import")}
          >
            批次新增表單及附件
          </Button>
          <Button
            variant="secondary"
            disabled={companyId === undefined}
            onClick={() =>
              navigate("/admin/documents/import", { state: { companyId } })
            }
          >
            匯入ISO管理程序 Excel
          </Button>
          <Button disabled={companyId === undefined} onClick={openCreateForm}>
            新增ISO管理程序
          </Button>
        </div>
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
          updateListParams({ keyword: keywordInput.trim(), page: null });
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
                  updateListParams({ companyId: event.target.value, page: null });
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
          sort={sort}
          onSortChange={handleSortChange}
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
        title={editingDocument ? "編輯ISO文件" : "新增ISO管理程序"}
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
              loadingText={editingDocument ? "儲存中" : "上傳中"}
            >
              {editingDocument ? "儲存" : "建立並發布"}
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
          key={formKey}
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
          <FormField
            label="品質系統"
            htmlFor="admin-document-iso-category"
            error={fieldErrors.isoCategoryId}
            hint=""
          >
            <Select
              id="admin-document-iso-category"
              value={formValues.isoCategoryId}
              disabled={isoCategories.isPending}
              error={fieldErrors.isoCategoryId !== undefined}
              onChange={(event) =>
                updateField("isoCategoryId", event.target.value)
              }
            >
              <option value="">不指定分類</option>
              {isoCategories.data?.items.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField
            label="發行單位"
            htmlFor="admin-document-dept"
            error={fieldErrors.deptId}
            hint=""
          >
            <Select
              id="admin-document-dept"
              value={formValues.deptId}
              disabled={depts.isPending}
              error={fieldErrors.deptId !== undefined}
              onChange={(event) => updateField("deptId", event.target.value)}
            >
              <option value="">不指定發行單位</option>
              {depts.data?.items.map((dept) => (
                <option key={dept.id} value={dept.id}>
                  {dept.name}
                </option>
              ))}
            </Select>
          </FormField>

          {!editingDocument && (
            <>
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField
                  label="版本號"
                  htmlFor="admin-document-version"
                  error={fieldErrors.version}
                  hint="例如 輸入 2 會儲存為 2.0。"
                  required
                >
                  <Input
                    id="admin-document-version"
                    type="text"
                    inputMode="decimal"
                    autoComplete="off"
                    value={formValues.version}
                    error={fieldErrors.version !== undefined}
                    aria-describedby={
                      fieldErrors.version
                        ? "admin-document-version-error"
                        : "admin-document-version-hint"
                    }
                    onChange={(event) =>
                      updateField("version", event.target.value)
                    }
                  />
                </FormField>

                <FormField
                  label="生效日期"
                  htmlFor="admin-document-effective-date"
                  error={fieldErrors.effectiveDate}
                  required
                >
                  <Input
                    id="admin-document-effective-date"
                    type="date"
                    min={todayUtc()}
                    value={formValues.effectiveDate}
                    error={fieldErrors.effectiveDate !== undefined}
                    aria-describedby={
                      fieldErrors.effectiveDate
                        ? "admin-document-effective-date-error"
                        : undefined
                    }
                    onChange={(event) =>
                      updateField("effectiveDate", event.target.value)
                    }
                  />
                </FormField>
              </div>

              <FormField
                label="備註"
                htmlFor="admin-document-memo"
                error={fieldErrors.memo}
                hint="選填。"
              >
                <Textarea
                  id="admin-document-memo"
                  value={formValues.memo}
                  error={fieldErrors.memo !== undefined}
                  aria-describedby={
                    fieldErrors.memo
                      ? "admin-document-memo-error"
                      : "admin-document-memo-hint"
                  }
                  onChange={(event) => updateField("memo", event.target.value)}
                />
              </FormField>

              <FormField
                label="ISO管理程序 PDF"
                htmlFor="admin-document-file"
                error={fieldErrors.file}
                required
              >
                <Input
                  id="admin-document-file"
                  type="file"
                  accept=".pdf,application/pdf"
                  error={fieldErrors.file !== undefined}
                  aria-describedby={
                    fieldErrors.file ? "admin-document-file-error" : undefined
                  }
                  onChange={(event) =>
                    updateFile(event.target.files?.[0] ?? null)
                  }
                />
              </FormField>
              <Alert variant="info" title="第一個版本">
                建立時需一併上傳ISO管理程序
                PDF，送出後此版本會立即發布；任一步驟失敗都不會留下文件。
              </Alert>
            </>
          )}
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
