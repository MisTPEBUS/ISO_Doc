import { useState, type FormEvent } from "react";

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
import { useCurrentUser } from "@/features/auth/queries";
import { USER_ROLE } from "@/features/auth/types";
import { useCompanies } from "@/features/companies/queries";
import {
  useCreateIsoCategory,
  useDeleteIsoCategory,
  useIsoCategories,
  useUpdateIsoCategory,
} from "@/features/iso-categories/queries";
import {
  isoCategoryFormSchema,
  type IsoCategoryFormValues,
} from "@/features/iso-categories/schemas";
import type { IsoCategoryResponse } from "@/features/iso-categories/types";

const DEFAULT_PAGE_SIZE = 10;
const EMPTY_FORM: IsoCategoryFormValues = { name: "", isActive: true };

type FieldErrors = Partial<Record<keyof IsoCategoryFormValues, string>>;

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

export function IsoCategoriesPage() {
  const currentUser = useCurrentUser();
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin;
  const [selectedCompanyId, setSelectedCompanyId] = useState(
    currentUser.data?.companyId ?? "",
  );
  const [includeInactive, setIncludeInactive] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [editingCategory, setEditingCategory] = useState<IsoCategoryResponse>();
  const [formOpen, setFormOpen] = useState(false);
  const [formValues, setFormValues] =
    useState<IsoCategoryFormValues>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [deleteTarget, setDeleteTarget] = useState<IsoCategoryResponse>();
  const [deleteError, setDeleteError] = useState<string>();
  const [successMessage, setSuccessMessage] = useState<string>();

  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin);
  const companyId = isSystemAdmin
    ? selectedCompanyId || undefined
    : currentUser.data?.companyId;
  const categories = useIsoCategories({
    companyId,
    includeInactive,
    page,
    pageSize,
  });

  function handlePageSizeChange(nextPageSize: number) {
    setPageSize(nextPageSize);
    setPage(1);
  }
  const createCategory = useCreateIsoCategory();
  const updateCategory = useUpdateIsoCategory();
  const deleteCategory = useDeleteIsoCategory();
  const formPending = createCategory.isPending || updateCategory.isPending;

  function openCreateForm() {
    setEditingCategory(undefined);
    setFormValues(EMPTY_FORM);
    setFieldErrors({});
    setFormError(undefined);
    setSuccessMessage(undefined);
    setFormOpen(true);
  }

  function openEditForm(category: IsoCategoryResponse) {
    setEditingCategory(category);
    setFormValues({ name: category.name, isActive: category.isActive });
    setFieldErrors({});
    setFormError(undefined);
    setSuccessMessage(undefined);
    setFormOpen(true);
  }

  function closeForm() {
    if (formPending) return;
    setFormOpen(false);
  }

  function updateField<K extends keyof IsoCategoryFormValues>(
    field: K,
    value: IsoCategoryFormValues[K],
  ) {
    setFormValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError(undefined);
  }

  function handleApiError(error: unknown) {
    if (!(error instanceof ApiError)) {
      setFormError("目前無法連線到系統，請稍後再試。");
      return;
    }

    const errors = error.fieldErrors();
    setFieldErrors({
      name: firstMessage(errors.name),
    });

    if (error.status === 409) {
      setFieldErrors((current) => ({
        ...current,
        name: error.detail ?? "同一公司內已有相同名稱的品質系統。",
      }));
      return;
    }

    setFormError(
      error.detail ??
        firstMessage(errors.companyId) ??
        "無法儲存品質系統資料。",
    );
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const parsed = isoCategoryFormSchema.safeParse(formValues);
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors;
      setFieldErrors({
        name: firstMessage(errors.name),
      });
      return;
    }

    if (companyId === undefined) {
      setFormError("目前登入資料缺少公司 ID，無法建立品質系統。");
      return;
    }

    const onSuccess = () => {
      setFormOpen(false);
      setSuccessMessage(
        editingCategory ? "品質系統已更新。" : "品質系統已新增。",
      );
    };

    if (editingCategory) {
      updateCategory.mutate(
        {
          id: editingCategory.id,
          request: parsed.data,
        },
        { onSuccess, onError: handleApiError },
      );
      return;
    }

    createCategory.mutate(
      {
        companyId,
        name: parsed.data.name,
      },
      { onSuccess, onError: handleApiError },
    );
  }

  function confirmDelete() {
    if (!deleteTarget) return;

    setDeleteError(undefined);
    deleteCategory.mutate(deleteTarget.id, {
      onSuccess: () => {
        setDeleteTarget(undefined);
        setSuccessMessage("品質系統已停用。");
      },
      onError: (error) => {
        setDeleteError(
          error instanceof ApiError
            ? (error.detail ?? "無法刪除品質系統。")
            : "目前無法連線到系統，請稍後再試。",
        );
      },
    });
  }

  const columns: ReadonlyArray<TableColumn<IsoCategoryResponse>> = [
    {
      key: "name",
      header: "分類名稱",
      cellClassName: "font-medium",
      render: (category) => category.name,
    },
    {
      key: "status",
      header: "狀態",
      render: (category) => (
        <Badge variant={category.isActive ? "success" : "danger"}>
          {category.isActive ? "啟用" : "停用"}
        </Badge>
      ),
    },
    {
      key: "updatedAt",
      header: "最後更新",
      headerClassName: "w-52",
      cellClassName: "text-meta text-ink-muted tabular",
      render: (category) => formatDateTime(category.updatedAt),
    },
    {
      key: "actions",
      header: "操作",
      headerClassName: "w-44",
      render: (category) => (
        <div className="flex items-center gap-1">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => openEditForm(category)}
          >
            編輯
          </Button>
          {category.isActive && (
            <Button
              size="sm"
              variant="ghost"
              className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
              onClick={() => {
                setDeleteError(undefined);
                setSuccessMessage(undefined);
                setDeleteTarget(category);
              }}
            >
              刪除
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
          <p className="mb-1 text-label font-medium text-primary">基礎設定</p>
          <h1 className="text-page-title text-ink">ISO 品質系統維護</h1>
          <p className="mt-1 text-meta text-ink-muted">
            管理公司自行維護的品質系統分類，供文件指派與前台篩選使用。
          </p>
        </div>
        <Button disabled={companyId === undefined} onClick={openCreateForm}>
          新增分類
        </Button>
      </div>

      {isSystemAdmin && (
        <div className="mb-4 border border-line-strong bg-surface p-4">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <FormField label="公司" htmlFor="iso-category-company-filter">
              <Select
                id="iso-category-company-filter"
                value={selectedCompanyId}
                disabled={companies.isPending || companies.isError}
                onChange={(event) => {
                  setSelectedCompanyId(event.target.value);
                  setPage(1);
                  setSuccessMessage(undefined);
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
          </div>
        </div>
      )}

      {companies.isError && isSystemAdmin && (
        <Alert className="mb-4" variant="error" title="無法載入公司">
          {companies.error instanceof ApiError
            ? (companies.error.detail ?? "請稍後重新整理頁面。")
            : "目前無法連線到系統，請稍後再試。"}
        </Alert>
      )}

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

      {categories.isError && (
        <Alert className="mb-4" variant="error" title="無法載入品質系統">
          {categories.error instanceof ApiError
            ? (categories.error.detail ?? "請稍後重新整理頁面。")
            : "目前無法連線到系統，請稍後再試。"}
        </Alert>
      )}

      <div className="border border-line-strong bg-surface">
        <div className="flex h-row items-center justify-between border-b border-line px-4">
          <div className="flex items-center gap-4">
            <p className="text-meta text-ink-muted">
              共{" "}
              <span className="tabular font-medium text-ink">
                {categories.data?.totalCount ?? 0}
              </span>{" "}
              個分類
            </p>
            <label className="flex cursor-pointer items-center gap-2 text-label text-ink">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                checked={includeInactive}
                onChange={(event) => {
                  setIncludeInactive(event.target.checked);
                  setPage(1);
                }}
              />
              包含已停用分類
            </label>
          </div>
          <Button
            variant="secondary"
            size="sm"
            loading={categories.isFetching}
            loadingText="載入中"
            onClick={() => void categories.refetch()}
          >
            重新整理
          </Button>
        </div>
        <Table
          className="border-0"
          columns={columns}
          data={categories.data?.items ?? []}
          loading={categories.isPending}
          skeletonRows={6}
          getRowKey={(category) => category.id}
          caption="品質系統清單"
          emptyMessage={
            <div className="py-5">
              <p className="font-medium text-ink">尚未建立任何品質系統</p>
            </div>
          }
        />
        <Pagination
          className="border-t border-line px-4"
          page={categories.data?.page ?? page}
          pageSize={pageSize}
          totalCount={categories.data?.totalCount ?? 0}
          onPageChange={setPage}
          onPageSizeChange={handlePageSizeChange}
        />
      </div>

      <Modal
        open={formOpen}
        onClose={closeForm}
        title={editingCategory ? "編輯品質系統" : "新增品質系統"}
        description={
          editingCategory
            ? "更新分類名稱或啟用狀態。"
            : "建立一個可供文件指派與前台篩選使用的品質系統。"
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
              form="iso-category-form"
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
          id="iso-category-form"
          className="space-y-4"
          noValidate
          onSubmit={handleSubmit}
        >
          <FormField
            label="分類名稱"
            htmlFor="iso-category-name"
            error={fieldErrors.name}
            required
          >
            <Input
              id="iso-category-name"
              value={formValues.name}
              error={fieldErrors.name !== undefined}
              aria-describedby={
                fieldErrors.name ? "iso-category-name-error" : undefined
              }
              onChange={(event) => updateField("name", event.target.value)}
            />
          </FormField>
          {editingCategory && (
            <label className="flex cursor-pointer items-center gap-2 text-label text-ink">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                checked={formValues.isActive}
                onChange={(event) =>
                  updateField("isActive", event.target.checked)
                }
              />
              啟用分類
            </label>
          )}
        </form>
      </Modal>

      <Modal
        open={deleteTarget !== undefined}
        onClose={() => {
          if (!deleteCategory.isPending) setDeleteTarget(undefined);
        }}
        size="sm"
        title="刪除品質系統"
        description="刪除為軟刪除（停用），可透過編輯畫面的「啟用分類」重新啟用。"
        footer={
          <>
            <Button
              variant="secondary"
              disabled={deleteCategory.isPending}
              onClick={() => setDeleteTarget(undefined)}
            >
              取消
            </Button>
            <Button
              variant="danger"
              loading={deleteCategory.isPending}
              loadingText="刪除中"
              onClick={confirmDelete}
            >
              確認刪除
            </Button>
          </>
        }
      >
        {deleteError && (
          <Alert className="mb-4" variant="error" title="無法刪除">
            {deleteError}
          </Alert>
        )}
        <p className="text-cell text-ink">
          確定要刪除「
          <strong className="font-semibold">{deleteTarget?.name}</strong>
          」嗎？已指派此分類的文件不受影響，仍可正常查詢與下載。
        </p>
      </Modal>
    </section>
  );
}

export default IsoCategoriesPage;
