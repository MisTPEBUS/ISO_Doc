import { useState } from "react";

import { ApiError } from "@/api/httpClient";
import { Alert, Button, FormField, Select } from "@/components/common";
import { useCurrentUser } from "@/features/auth/queries";
import { USER_ROLE } from "@/features/auth/types";
import { useCompanies } from "@/features/companies/queries";
import { PermissionMatrixTable } from "@/features/permissions/components/PermissionMatrixTable";
import {
  usePermissionMatrix,
  useUpdatePermissionMatrix,
} from "@/features/permissions/queries";
import type { PermissionMatrixItem } from "@/features/permissions/types";

const DEFAULT_PAGE_SIZE = 20;

type SelectionOverrides = Readonly<Record<string, ReadonlySet<string>>>;

function isSameAsSaved(
  selectedDepartmentIds: ReadonlySet<string>,
  savedDepartmentIds: readonly string[],
): boolean {
  return (
    selectedDepartmentIds.size === savedDepartmentIds.length &&
    savedDepartmentIds.every((id) => selectedDepartmentIds.has(id))
  );
}

// 勾選結果如果繞回文件目前已儲存的設定（例如點兩下等於沒動），就把這份文件從
// 「有異動」清單移除，而不是留著一個內容跟原本一樣的 override——否則使用者會被
// 要求儲存一份其實什麼都沒改的文件。
function applyDepartmentSelection(
  current: SelectionOverrides,
  document: PermissionMatrixItem,
  nextSelection: ReadonlySet<string>,
): SelectionOverrides {
  if (isSameAsSaved(nextSelection, document.departmentIds)) {
    if (!(document.documentId in current)) return current;

    return Object.fromEntries(
      Object.entries(current).filter(([documentId]) => documentId !== document.documentId),
    );
  }

  return { ...current, [document.documentId]: nextSelection };
}

function errorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) {
    return "目前無法連線到系統，請稍後再試。";
  }

  if (error.status === 403) {
    return "你沒有維護這家公司文件權限的權限。";
  }

  return error.detail ?? fallback;
}

export function PermissionsPage() {
  const [requestedCompanyCode, setRequestedCompanyCode] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [selectionOverrides, setSelectionOverrides] =
    useState<SelectionOverrides>({});
  const [saveError, setSaveError] = useState<string>();
  const [successMessage, setSuccessMessage] = useState<string>();

  const currentUser = useCurrentUser();
  // /api/companies 僅 SYSTEM_ADMIN 可呼叫；COMPANY_ADMIN 沒有跨公司選擇的需求，
  // 後端會依登入者自己的 companyId 強制限縮範圍（忽略傳入的 companyCode）。
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin;
  const isCompanyAdmin = currentUser.data?.role === USER_ROLE.CompanyAdmin;
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin);
  const selectedCompanyCode = isSystemAdmin
    ? requestedCompanyCode || companies.data?.items[0]?.code || ""
    : "";
  const matrix = usePermissionMatrix(
    { companyCode: selectedCompanyCode, page, pageSize },
    isSystemAdmin ? selectedCompanyCode.length > 0 : isCompanyAdmin,
  );
  const updateMatrix = useUpdatePermissionMatrix();

  const dirtyDocumentIds = Object.keys(selectionOverrides);
  const isDirty = dirtyDocumentIds.length > 0;

  function selectCompany(companyCode: string) {
    setRequestedCompanyCode(companyCode);
    setPage(1);
    setSelectionOverrides({});
    setSaveError(undefined);
    setSuccessMessage(undefined);
  }

  function cancelChanges() {
    if (updateMatrix.isPending) return;
    setSelectionOverrides({});
    setSaveError(undefined);
  }

  function saveChanges() {
    if (updateMatrix.isPending || !isDirty) return;

    setSaveError(undefined);
    setSuccessMessage(undefined);
    updateMatrix.mutate(
      {
        items: Object.entries(selectionOverrides).map(
          ([documentId, departmentIds]) => ({
            documentId,
            departmentIds: [...departmentIds],
          }),
        ),
      },
      {
        onSuccess: (response) => {
          setSelectionOverrides({});
          setSuccessMessage(`已儲存 ${response.items.length} 份文件的權限。`);
        },
        onError: (error) => {
          setSaveError(errorMessage(error, "無法儲存權限異動，請稍後再試。"));
        },
      },
    );
  }

  function togglePermission(
    document: PermissionMatrixItem,
    departmentId: string,
  ) {
    const validDepartmentIds = new Set(
      matrix.data?.departments.map((department) => department.id) ?? [],
    );

    setSelectionOverrides((current) => {
      const selectedDepartmentIds = new Set(
        [...(current[document.documentId] ?? document.departmentIds)].filter(
          (id) => validDepartmentIds.has(id),
        ),
      );

      if (selectedDepartmentIds.has(departmentId)) {
        selectedDepartmentIds.delete(departmentId);
      } else {
        selectedDepartmentIds.add(departmentId);
      }

      return applyDepartmentSelection(current, document, selectedDepartmentIds);
    });
  }

  function toggleAllDepartments(document: PermissionMatrixItem) {
    const departmentIds = matrix.data?.departments.map(
      (department) => department.id,
    ) ?? [];
    if (departmentIds.length === 0) return;

    setSelectionOverrides((current) => {
      const selectedDepartmentIds = new Set(
        current[document.documentId] ?? document.departmentIds,
      );
      const allDepartmentsSelected = departmentIds.every((departmentId) =>
        selectedDepartmentIds.has(departmentId),
      );
      const nextSelection = new Set(
        allDepartmentsSelected ? [] : departmentIds,
      );

      return applyDepartmentSelection(current, document, nextSelection);
    });
  }

  return (
    <section>
      <div className="mb-4 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="mb-1 text-label font-medium text-primary">文件管理</p>
          <h1 className="text-page-title text-ink">ISO 文件權限矩陣</h1>
          <p className="mt-1 text-meta text-ink-muted">
            勾選可查看各主文的部門；勾選後按「儲存」才會真正套用。
          </p>
        </div>
        {isSystemAdmin ? (
          <FormField
            className="w-full shrink-0 sm:w-64"
            label="選擇公司"
            htmlFor="permission-company"
          >
            <Select
              id="permission-company"
              value={selectedCompanyCode}
              disabled={companies.isPending || companies.isError}
              onChange={(event) => selectCompany(event.target.value)}
            >
              <option value="">
                {companies.isPending ? "公司載入中" : "請選擇公司"}
              </option>
              {companies.data?.items.map((company) => (
                <option key={company.id} value={company.code}>
                  {company.name}
                </option>
              ))}
            </Select>
          </FormField>
        ) : (
          currentUser.data && (
            <FormField className="w-full shrink-0 sm:w-64" label="公司">
              <p className="text-cell text-ink">{currentUser.data.companyName}</p>
            </FormField>
          )
        )}
      </div>

      {isSystemAdmin && companies.isError && (
        <Alert className="mb-4" variant="error" title="無法載入公司">
          {errorMessage(companies.error, "請稍後重新整理頁面。")}
        </Alert>
      )}

      <div className="overflow-hidden rounded-md border border-line-strong bg-surface">
        {matrix.isError ? (
          <Alert className="m-4" variant="error" title="無法載入權限矩陣">
            {errorMessage(matrix.error, "請稍後重新整理頁面。")}
          </Alert>
        ) : isSystemAdmin && selectedCompanyCode.length === 0 ? (
          <div className="p-12 text-center">
            <p className="text-cell font-medium text-ink">請先選擇公司</p>
            <p className="mt-1 text-meta text-ink-muted">
              選擇公司後即可查看 ISO 文件權限矩陣。
            </p>
          </div>
        ) : (
          <PermissionMatrixTable
            departments={matrix.data?.departments ?? []}
            items={matrix.data?.items ?? []}
            selectionOverrides={selectionOverrides}
            loading={matrix.isPending}
            page={matrix.data?.pagination.page ?? page}
            pageSize={matrix.data?.pagination.pageSize ?? pageSize}
            totalCount={matrix.data?.pagination.totalCount ?? 0}
            onToggle={togglePermission}
            onToggleAll={toggleAllDepartments}
            onPageChange={setPage}
            onPageSizeChange={(nextPageSize) => {
              setPageSize(nextPageSize);
              setPage(1);
            }}
          />
        )}
      </div>

      {successMessage && !isDirty && (
        <Alert
          className="mt-4"
          variant="success"
          title="已儲存"
          dismissAfterMs={3000}
          onDismiss={() => setSuccessMessage(undefined)}
        >
          {successMessage}
        </Alert>
      )}

      {isDirty && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border border-line-strong bg-surface px-4 py-3">
          <div className="min-w-0">
            <p className="text-meta text-ink-muted">
              {dirtyDocumentIds.length} 份文件的權限尚未儲存。
            </p>
            {saveError && (
              <p className="mt-1 text-meta text-state-danger" role="alert">
                {saveError}
              </p>
            )}
          </div>
          <div className="flex shrink-0 gap-2">
            <Button
              variant="secondary"
              disabled={updateMatrix.isPending}
              onClick={cancelChanges}
            >
              取消
            </Button>
            <Button
              loading={updateMatrix.isPending}
              loadingText="儲存中"
              onClick={saveChanges}
            >
              儲存
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}

export default PermissionsPage;
