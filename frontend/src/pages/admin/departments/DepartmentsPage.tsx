import { useState, type FormEvent } from 'react'

import { ApiError } from '@/api/httpClient'
import {
  Alert,
  Button,
  FormField,
  Input,
  Modal,
  Pagination,
  Select,
  Table,
  type TableColumn,
} from '@/components/common'
import { useCurrentUser } from '@/features/auth/queries'
import { USER_ROLE } from '@/features/auth/types'
import { useCompanies } from '@/features/companies/queries'
import {
  useCreateDept,
  useDeleteDept,
  useDepts,
  useUpdateDept,
} from '@/features/departments/queries'
import {
  deptFormSchema,
  type DeptFormValues,
} from '@/features/departments/schemas'
import type { DeptResponse } from '@/features/departments/types'

const DEFAULT_PAGE_SIZE = 10
const EMPTY_FORM: DeptFormValues = { name: '', seq: '' }

type FieldErrors = Partial<Record<keyof DeptFormValues, string>>

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0]
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Taipei',
  }).format(new Date(value))
}

export function DepartmentsPage() {
  const currentUser = useCurrentUser()
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin
  const [selectedCompanyId, setSelectedCompanyId] = useState(
    currentUser.data?.companyId ?? '',
  )
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)
  const [editingDepartment, setEditingDepartment] = useState<DeptResponse>()
  const [formOpen, setFormOpen] = useState(false)
  const [formValues, setFormValues] = useState<DeptFormValues>(EMPTY_FORM)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string>()
  const [deleteTarget, setDeleteTarget] = useState<DeptResponse>()
  const [deleteError, setDeleteError] = useState<string>()
  const [successMessage, setSuccessMessage] = useState<string>()

  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin)
  const companyId = isSystemAdmin
    ? (selectedCompanyId || undefined)
    : currentUser.data?.companyId
  const departments = useDepts({ companyId, page, pageSize })

  function handlePageSizeChange(nextPageSize: number) {
    setPageSize(nextPageSize)
    setPage(1)
  }
  const createDepartment = useCreateDept()
  const updateDepartment = useUpdateDept()
  const deleteDepartment = useDeleteDept()
  const formPending = createDepartment.isPending || updateDepartment.isPending

  function openCreateForm() {
    setEditingDepartment(undefined)
    setFormValues(EMPTY_FORM)
    setFieldErrors({})
    setFormError(undefined)
    setSuccessMessage(undefined)
    setFormOpen(true)
  }

  function openEditForm(department: DeptResponse) {
    setEditingDepartment(department)
    setFormValues({
      name: department.name,
      seq: department.seq === null ? '' : String(department.seq),
    })
    setFieldErrors({})
    setFormError(undefined)
    setSuccessMessage(undefined)
    setFormOpen(true)
  }

  function closeForm() {
    if (formPending) return
    setFormOpen(false)
  }

  function updateField(field: keyof DeptFormValues, value: string) {
    setFormValues((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => ({ ...current, [field]: undefined }))
    setFormError(undefined)
  }

  function handleApiError(error: unknown) {
    if (!(error instanceof ApiError)) {
      setFormError('目前無法連線到系統，請稍後再試。')
      return
    }

    const errors = error.fieldErrors()
    setFieldErrors({
      name: firstMessage(errors.name),
      seq: firstMessage(errors.seq),
    })

    if (error.status === 409) {
      setFieldErrors((current) => ({
        ...current,
        name: error.detail ?? '同一公司內已有相同名稱的部門。',
      }))
      return
    }

    setFormError(error.detail ?? firstMessage(errors.companyId) ?? '無法儲存部門資料。')
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(undefined)

    const parsed = deptFormSchema.safeParse(formValues)
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors
      setFieldErrors({
        name: firstMessage(errors.name),
        seq: firstMessage(errors.seq),
      })
      return
    }

    if (companyId === undefined) {
      setFormError('目前登入資料缺少公司 ID，無法建立部門。')
      return
    }

    const onSuccess = () => {
      setFormOpen(false)
      setSuccessMessage(editingDepartment ? '部門資料已更新。' : '部門已新增。')
    }

    if (editingDepartment) {
      updateDepartment.mutate({
        id: editingDepartment.id,
        request: parsed.data,
      }, { onSuccess, onError: handleApiError })
      return
    }

    createDepartment.mutate({
      companyId,
      ...parsed.data,
    }, { onSuccess, onError: handleApiError })
  }

  function confirmDelete() {
    if (!deleteTarget) return

    setDeleteError(undefined)
    deleteDepartment.mutate(deleteTarget.id, {
      onSuccess: () => {
        setDeleteTarget(undefined)
        setSuccessMessage('部門已刪除。')
      },
      onError: (error) => {
        if (error instanceof ApiError && error.status === 409) {
          setDeleteError(
            error.detail ?? '此部門仍有在職使用者，請先調整使用者所屬部門後再刪除。',
          )
          return
        }

        setDeleteError(
          error instanceof ApiError
            ? (error.detail ?? '無法刪除部門。')
            : '目前無法連線到系統，請稍後再試。',
        )
      },
    })
  }

  const columns: ReadonlyArray<TableColumn<DeptResponse>> = [
    {
      key: 'name',
      header: '部門名稱',
      cellClassName: 'font-medium',
      render: (department) => department.name,
    },
    {
      key: 'seq',
      header: '排序',
      headerClassName: 'w-28 text-right',
      cellClassName: 'text-right tabular text-ink-muted',
      render: (department) => department.seq ?? '－',
    },
    {
      key: 'updatedAt',
      header: '最後更新',
      headerClassName: 'w-52',
      cellClassName: 'text-meta text-ink-muted tabular',
      render: (department) => formatDateTime(department.updatedAt),
    },
    {
      key: 'actions',
      header: '操作',
      headerClassName: 'w-44',
      render: (department) => (
        <div className="flex items-center gap-1">
          <Button size="sm" variant="ghost" onClick={() => openEditForm(department)}>編輯</Button>
          <Button
            size="sm"
            variant="ghost"
            className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
            onClick={() => {
              setDeleteError(undefined)
              setSuccessMessage(undefined)
              setDeleteTarget(department)
            }}
          >
            刪除
          </Button>
        </div>
      ),
    },
  ]

  return (
    <section>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="mb-1 text-label font-medium text-primary">基礎設定</p>
          <h1 className="text-page-title text-ink">部門維護</h1>
          <p className="mt-1 text-meta text-ink-muted">管理公司內可用的部門名稱與顯示順序。</p>
        </div>
        <Button disabled={companyId === undefined} onClick={openCreateForm}>新增部門</Button>
      </div>

      {isSystemAdmin && (
        <div className="mb-4 border border-line-strong bg-surface p-4">
          <FormField
            label="公司"
            htmlFor="department-company-filter"
            hint="選擇公司以查詢該公司的部門。"
          >
            <Select
              id="department-company-filter"
              className="max-w-md"
              value={selectedCompanyId}
              disabled={companies.isPending || companies.isError}
              onChange={(event) => {
                setSelectedCompanyId(event.target.value)
                setPage(1)
                setSuccessMessage(undefined)
              }}
            >
              {companies.isPending && <option value="">公司載入中</option>}
              {companies.isError && <option value="">無法載入公司</option>}
              {companies.data?.items.length === 0 && <option value="">查無公司</option>}
              {companies.data?.items.map((company) => (
                <option key={company.id} value={company.id}>
                  {company.code} — {company.name}
                </option>
              ))}
            </Select>
          </FormField>
        </div>
      )}

      {companies.isError && isSystemAdmin && (
        <Alert className="mb-4" variant="error" title="無法載入公司">
          {companies.error instanceof ApiError
            ? (companies.error.detail ?? '請稍後重新整理頁面。')
            : '目前無法連線到系統，請稍後再試。'}
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

      {departments.isError && (
        <Alert className="mb-4" variant="error" title="無法載入部門">
          {departments.error instanceof ApiError
            ? (departments.error.detail ?? '請稍後重新整理頁面。')
            : '目前無法連線到系統，請稍後再試。'}
        </Alert>
      )}

      <div className="border border-line-strong bg-surface">
        <div className="flex h-row items-center justify-between border-b border-line px-4">
          <p className="text-meta text-ink-muted">
            共 <span className="tabular font-medium text-ink">{departments.data?.totalCount ?? 0}</span> 個部門
          </p>
          <Button variant="secondary" size="sm" loading={departments.isFetching} loadingText="載入中" onClick={() => void departments.refetch()}>
            重新整理
          </Button>
        </div>
        <Table
          className="border-0"
          columns={columns}
          data={departments.data?.items ?? []}
          loading={departments.isPending}
          skeletonRows={6}
          getRowKey={(department) => department.id}
          caption="部門清單"
          emptyMessage={(
            <div className="py-5">
              <p className="font-medium text-ink">尚未建立任何部門</p>
              <p className="mt-1 text-meta">建立第一個部門後，即可指派使用者與文件權限。</p>
              <Button className="mt-4" onClick={openCreateForm}>新增部門</Button>
            </div>
          )}
        />
        <Pagination
          className="border-t border-line px-4"
          page={departments.data?.page ?? page}
          pageSize={pageSize}
          totalCount={departments.data?.totalCount ?? 0}
          onPageChange={setPage}
          onPageSizeChange={handlePageSizeChange}
        />
      </div>

      <Modal
        open={formOpen}
        onClose={closeForm}
        title={editingDepartment ? '編輯部門' : '新增部門'}
        description={editingDepartment ? '更新部門名稱或顯示順序。' : '建立一個可供使用者與文件權限使用的部門。'}
        footer={(
          <>
            <Button variant="secondary" disabled={formPending} onClick={closeForm}>取消</Button>
            <Button type="submit" form="department-form" loading={formPending} loadingText="儲存中">儲存</Button>
          </>
        )}
      >
        {formError && <Alert className="mb-4" variant="error">{formError}</Alert>}
        <form id="department-form" className="space-y-4" noValidate onSubmit={handleSubmit}>
          <FormField label="部門名稱" htmlFor="department-name" error={fieldErrors.name} required>
            <Input
              id="department-name"
              value={formValues.name}
              error={fieldErrors.name !== undefined}
              aria-describedby={fieldErrors.name ? 'department-name-error' : undefined}
              onChange={(event) => updateField('name', event.target.value)}
            />
          </FormField>
          <FormField label="顯示順序" htmlFor="department-seq" error={fieldErrors.seq} hint="選填；請輸入整數。">
            <Input
              id="department-seq"
              type="number"
              step="1"
              inputMode="numeric"
              value={formValues.seq}
              error={fieldErrors.seq !== undefined}
              aria-describedby={fieldErrors.seq ? 'department-seq-error' : 'department-seq-hint'}
              onChange={(event) => updateField('seq', event.target.value)}
            />
          </FormField>
        </form>
      </Modal>

      <Modal
        open={deleteTarget !== undefined}
        onClose={() => {
          if (!deleteDepartment.isPending) setDeleteTarget(undefined)
        }}
        size="sm"
        title="刪除部門"
        description="此操作無法復原。"
        footer={(
          <>
            <Button variant="secondary" disabled={deleteDepartment.isPending} onClick={() => setDeleteTarget(undefined)}>取消</Button>
            <Button variant="danger" loading={deleteDepartment.isPending} loadingText="刪除中" onClick={confirmDelete}>確認刪除</Button>
          </>
        )}
      >
        {deleteError && <Alert className="mb-4" variant="error" title="無法刪除">{deleteError}</Alert>}
        <p className="text-cell text-ink">
          確定要刪除「<strong className="font-semibold">{deleteTarget?.name}</strong>」嗎？若部門仍有在職使用者，系統會拒絕刪除。
        </p>
      </Modal>
    </section>
  )
}

export default DepartmentsPage
