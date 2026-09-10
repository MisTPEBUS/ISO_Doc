import { useState, type FormEvent } from 'react'

import { ApiError } from '@/api/httpClient'
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
} from '@/components/common'
import { useCurrentUser } from '@/features/auth/queries'
import { USER_ROLE } from '@/features/auth/types'
import { useCompanies } from '@/features/companies/queries'
import { useDepts } from '@/features/departments/queries'
import {
  createUserFormSchema,
  updateUserFormSchema,
  type UserFormValues,
} from '@/features/users/schemas'
import {
  useCreateUser,
  useDeleteUser,
  useResetPassword,
  useUpdateUser,
  useUsers,
} from '@/features/users/queries'
import {
  MANAGED_USER_ROLE,
  MANAGED_USER_ROLE_LABEL,
  isManagedUserRole,
  type ManagedUserRole,
  type UserResponse,
} from '@/features/users/types'

const PAGE_SIZE = 10

const EMPTY_FORM: UserFormValues = {
  empno: '',
  name: '',
  email: '',
  deptId: '',
  role: MANAGED_USER_ROLE.User,
  password: '',
  passwordConfirmation: '',
  isActive: true,
  notifyEmailEnabled: true,
}

const ROLE_BADGE_VARIANT: Record<ManagedUserRole, 'neutral' | 'info' | 'warning'> = {
  [MANAGED_USER_ROLE.User]: 'neutral',
  [MANAGED_USER_ROLE.CompanyAdmin]: 'info',
  [MANAGED_USER_ROLE.SystemAdmin]: 'warning',
}

type FieldErrors = Partial<Record<keyof UserFormValues, string>>

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0]
}

function formatDateTime(value: string | null): string {
  if (value === null) return '尚未登入'

  return new Intl.DateTimeFormat('zh-TW', {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: 'Asia/Taipei',
  }).format(new Date(value))
}

function errorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) return '目前無法連線到系統，請稍後再試。'

  if (
    error.status === 403
    && /system administrator|系統管理員/i.test(error.detail ?? '')
  ) {
    return '公司管理員不可建立、修改或重設系統管理員帳號。'
  }

  return error.detail ?? fallback
}

export function UsersPage() {
  const currentUser = useCurrentUser()
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin
  const [selectedCompanyId, setSelectedCompanyId] = useState(
    currentUser.data?.companyId ?? '',
  )
  const [keywordInput, setKeywordInput] = useState('')
  const [keyword, setKeyword] = useState('')
  const [selectedDeptId, setSelectedDeptId] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [page, setPage] = useState(1)
  const [editingUser, setEditingUser] = useState<UserResponse>()
  const [formOpen, setFormOpen] = useState(false)
  const [formValues, setFormValues] = useState<UserFormValues>(EMPTY_FORM)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string>()
  const [deleteTarget, setDeleteTarget] = useState<UserResponse>()
  const [deleteError, setDeleteError] = useState<string>()
  const [resetTarget, setResetTarget] = useState<UserResponse>()
  const [resetError, setResetError] = useState<string>()
  const [temporaryPassword, setTemporaryPassword] = useState<string>()
  const [copyMessage, setCopyMessage] = useState<string>()
  const [successMessage, setSuccessMessage] = useState<string>()

  const companyId = isSystemAdmin
    ? (selectedCompanyId || undefined)
    : currentUser.data?.companyId
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin)
  const departments = useDepts(
    { companyId, page: 1, pageSize: 100 },
    companyId !== undefined,
  )
  const users = useUsers({
    companyId,
    deptId: selectedDeptId || undefined,
    keyword: keyword || undefined,
    includeInactive,
    page,
    pageSize: PAGE_SIZE,
  })
  const createUser = useCreateUser()
  const updateUser = useUpdateUser()
  const deleteUser = useDeleteUser()
  const resetPassword = useResetPassword()
  const formPending = createUser.isPending || updateUser.isPending
  const hasFilters = keyword.length > 0 || selectedDeptId.length > 0 || includeInactive
  const selectedCompany = companies.data?.items.find((company) => company.id === companyId)
  const departmentNames = new Map(
    (departments.data?.items ?? []).map((department) => [department.id, department.name]),
  )

  function openCreateForm() {
    setEditingUser(undefined)
    setFormValues({ ...EMPTY_FORM, deptId: selectedDeptId })
    setFieldErrors({})
    setFormError(undefined)
    setSuccessMessage(undefined)
    setFormOpen(true)
  }

  function openEditForm(user: UserResponse) {
    setEditingUser(user)
    setFormValues({
      empno: user.empno,
      name: user.name,
      email: user.email ?? '',
      deptId: user.deptId,
      role: user.role,
      password: '',
      passwordConfirmation: '',
      isActive: user.isActive,
      notifyEmailEnabled: user.notifyEmailEnabled,
    })
    setFieldErrors({})
    setFormError(undefined)
    setSuccessMessage(undefined)
    setFormOpen(true)
  }

  function closeForm() {
    if (!formPending) setFormOpen(false)
  }

  function updateField<Field extends keyof UserFormValues>(
    field: Field,
    value: UserFormValues[Field],
  ) {
    setFormValues((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => ({ ...current, [field]: undefined }))
    setFormError(undefined)
  }

  function applyServerErrors(error: ApiError) {
    const errors = error.fieldErrors()
    setFieldErrors({
      empno: firstMessage(errors.empno),
      name: firstMessage(errors.name),
      email: firstMessage(errors.email),
      deptId: firstMessage(errors.deptId),
      role: firstMessage(errors.role),
      password: firstMessage(errors.password),
      passwordConfirmation: firstMessage(errors.passwordConfirmation),
    })

    if (Object.keys(errors).length === 0 || error.status === 403) {
      setFormError(errorMessage(error, '無法儲存使用者資料。'))
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(undefined)

    if (companyId === undefined) {
      setFormError('請先選擇公司。')
      return
    }

    if (editingUser) {
      const parsed = updateUserFormSchema.safeParse(formValues)
      if (!parsed.success) {
        const errors = parsed.error.flatten().fieldErrors
        setFieldErrors({
          name: firstMessage(errors.name),
          email: firstMessage(errors.email),
          deptId: firstMessage(errors.deptId),
          role: firstMessage(errors.role),
        })
        return
      }

      updateUser.mutate({ id: editingUser.id, request: {
        name: parsed.data.name,
        email: parsed.data.email || null,
        deptId: parsed.data.deptId,
        role: parsed.data.role,
        isActive: parsed.data.isActive,
        // TODO: 通知功能不在本輪範圍；更新使用者時保留後端既有設定值。
        notifyEmailEnabled: parsed.data.notifyEmailEnabled,
      } }, {
        onSuccess: () => {
          setFormOpen(false)
          setSuccessMessage('使用者資料已更新。')
        },
        onError: (error) => {
          if (error instanceof ApiError) applyServerErrors(error)
          else setFormError(errorMessage(error, '無法更新使用者資料。'))
        },
      })
      return
    }

    const parsed = createUserFormSchema.safeParse(formValues)
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors
      setFieldErrors({
        empno: firstMessage(errors.empno),
        name: firstMessage(errors.name),
        email: firstMessage(errors.email),
        deptId: firstMessage(errors.deptId),
        role: firstMessage(errors.role),
        password: firstMessage(errors.password),
        passwordConfirmation: firstMessage(errors.passwordConfirmation),
      })
      return
    }

    createUser.mutate({
      empno: parsed.data.empno,
      name: parsed.data.name,
      email: parsed.data.email || null,
      companyId,
      deptId: parsed.data.deptId,
      role: parsed.data.role,
      password: parsed.data.password || null,
      passwordConfirmation: parsed.data.password ? parsed.data.passwordConfirmation : null,
    }, {
      onSuccess: () => {
        setFormOpen(false)
        setSuccessMessage('使用者已新增。')
      },
      onError: (error) => {
        if (error instanceof ApiError) applyServerErrors(error)
        else setFormError(errorMessage(error, '無法新增使用者。'))
      },
    })
  }

  function confirmDelete() {
    if (!deleteTarget) return

    setDeleteError(undefined)
    deleteUser.mutate(deleteTarget.id, {
      onSuccess: () => {
        setDeleteTarget(undefined)
        setSuccessMessage('使用者已停用。')
      },
      onError: (error) => setDeleteError(errorMessage(error, '無法停用使用者。')),
    })
  }

  function openResetDialog(user: UserResponse) {
    setResetTarget(user)
    setResetError(undefined)
    setTemporaryPassword(undefined)
    setCopyMessage(undefined)
  }

  function closeResetDialog() {
    if (resetPassword.isPending) return
    setResetTarget(undefined)
    setTemporaryPassword(undefined)
    setCopyMessage(undefined)
  }

  function confirmResetPassword() {
    if (!resetTarget) return

    setResetError(undefined)
    resetPassword.mutate(resetTarget.id, {
      onSuccess: (response) => setTemporaryPassword(response.temporaryPassword),
      onError: (error) => setResetError(errorMessage(error, '無法重設使用者密碼。')),
    })
  }

  async function copyTemporaryPassword() {
    if (temporaryPassword === undefined) return

    try {
      await navigator.clipboard.writeText(temporaryPassword)
      setCopyMessage('臨時密碼已複製。')
    } catch {
      setCopyMessage('無法自動複製，請手動選取臨時密碼。')
    }
  }

  function clearFilters() {
    setKeywordInput('')
    setKeyword('')
    setSelectedDeptId('')
    setIncludeInactive(false)
    setPage(1)
  }

  const columns: ReadonlyArray<TableColumn<UserResponse>> = [
    {
      key: 'empno',
      header: '員工編號',
      cellClassName: 'tabular',
      render: (user) => user.empno,
    },
    {
      key: 'name',
      header: '姓名',
      cellClassName: 'font-medium',
      render: (user) => user.name,
    },
    {
      key: 'department',
      header: '部門',
      render: (user) => departmentNames.get(user.deptId) ?? '－',
    },
    {
      key: 'email',
      header: '電子郵件',
      cellClassName: 'text-meta text-ink-muted',
      render: (user) => user.email ?? '－',
    },
    {
      key: 'role',
      header: '角色',
      render: (user) => (
        <Badge variant={ROLE_BADGE_VARIANT[user.role]}>
          {MANAGED_USER_ROLE_LABEL[user.role]}
        </Badge>
      ),
    },
    {
      key: 'status',
      header: '狀態',
      render: (user) => (
        <Badge variant={user.isActive ? 'success' : 'danger'}>
          {user.isActive ? '啟用' : '停用'}
        </Badge>
      ),
    },
    {
      key: 'lastLoginAt',
      header: '最後登入',
      cellClassName: 'text-meta text-ink-muted tabular whitespace-nowrap',
      render: (user) => formatDateTime(user.lastLoginAt),
    },
    {
      key: 'actions',
      header: '操作',
      headerClassName: 'w-72',
      render: (user) => {
        const canManage = isSystemAdmin || user.role !== MANAGED_USER_ROLE.SystemAdmin
        if (!canManage) return <span className="text-meta text-ink-muted">無可用操作</span>

        return (
          <div className="flex items-center gap-1">
            <Button size="sm" variant="ghost" onClick={() => openEditForm(user)}>編輯</Button>
            <Button size="sm" variant="ghost" onClick={() => openResetDialog(user)}>重設密碼</Button>
            {user.isActive && (
              <Button
                size="sm"
                variant="ghost"
                className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
                onClick={() => {
                  setDeleteError(undefined)
                  setDeleteTarget(user)
                }}
              >
                停用
              </Button>
            )}
          </div>
        )
      },
    },
  ]

  return (
    <section>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="mb-1 text-label font-medium text-primary">基礎設定</p>
          <h1 className="text-page-title text-ink">使用者維護</h1>
          <p className="mt-1 text-meta text-ink-muted">管理帳號、角色、所屬部門與登入狀態。</p>
        </div>
        <Button disabled={companyId === undefined || departments.isPending} onClick={openCreateForm}>
          新增使用者
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

      {(users.isError || departments.isError || (isSystemAdmin && companies.isError)) && (
        <Alert className="mb-4" variant="error" title="無法載入使用者維護資料">
          {errorMessage(
            users.error ?? departments.error ?? companies.error,
            '請稍後重新整理頁面。',
          )}
        </Alert>
      )}

      <form
        className="border border-line-strong bg-surface p-4"
        onSubmit={(event) => {
          event.preventDefault()
          setKeyword(keywordInput.trim())
          setPage(1)
        }}
      >
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {isSystemAdmin && (
            <FormField label="公司" htmlFor="user-company-filter">
              <Select
                id="user-company-filter"
                value={selectedCompanyId}
                disabled={companies.isPending || companies.isError}
                onChange={(event) => {
                  setSelectedCompanyId(event.target.value)
                  setSelectedDeptId('')
                  setPage(1)
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
          <FormField label="關鍵字" htmlFor="user-keyword" hint="搜尋員工編號、姓名或電子郵件。">
            <Input
              id="user-keyword"
              value={keywordInput}
              placeholder="輸入關鍵字"
              onChange={(event) => setKeywordInput(event.target.value)}
            />
          </FormField>
          <FormField label="部門" htmlFor="user-department-filter">
            <Select
              id="user-department-filter"
              value={selectedDeptId}
              disabled={departments.isPending || departments.isError}
              onChange={(event) => {
                setSelectedDeptId(event.target.value)
                setPage(1)
              }}
            >
              <option value="">全部部門</option>
              {departments.data?.items.map((department) => (
                <option key={department.id} value={department.id}>{department.name}</option>
              ))}
            </Select>
          </FormField>
          <div className="flex items-end gap-2">
            <label className="flex h-control flex-1 cursor-pointer items-center gap-2 text-label text-ink">
              <input
                type="checkbox"
                className="size-4 accent-primary"
                checked={includeInactive}
                onChange={(event) => {
                  setIncludeInactive(event.target.checked)
                  setPage(1)
                }}
              />
              包含停用帳號
            </label>
            <Button type="submit" variant="secondary">搜尋</Button>
          </div>
        </div>
      </form>

      <div className="border-x border-b border-line-strong bg-surface">
        <div className="flex h-row items-center justify-between border-b border-line px-4">
          <p className="text-meta text-ink-muted">
            共 <span className="tabular font-medium text-ink">{users.data?.totalCount ?? 0}</span> 位使用者
          </p>
          <Button variant="secondary" size="sm" loading={users.isFetching} loadingText="載入中" onClick={() => void users.refetch()}>
            重新整理
          </Button>
        </div>
        <Table
          className="border-0"
          columns={columns}
          data={users.data?.items ?? []}
          loading={users.isPending}
          skeletonRows={6}
          getRowKey={(user) => user.id}
          caption="使用者清單"
          emptyMessage={(
            <div className="py-5">
              <p className="font-medium text-ink">
                {hasFilters ? '沒有符合條件的使用者' : '尚未建立任何使用者'}
              </p>
              <p className="mt-1 text-meta">
                {hasFilters ? '請調整搜尋條件後再試一次。' : '建立第一位使用者以開始管理系統帳號。'}
              </p>
              {hasFilters
                ? <Button className="mt-4" variant="secondary" onClick={clearFilters}>清除篩選</Button>
                : <Button className="mt-4" variant="secondary" onClick={openCreateForm}>新增使用者</Button>}
            </div>
          )}
        />
        <Pagination
          className="border-t border-line px-4"
          page={users.data?.page ?? page}
          pageSize={users.data?.pageSize ?? PAGE_SIZE}
          totalCount={users.data?.totalCount ?? 0}
          onPageChange={setPage}
        />
      </div>

      <Modal
        open={formOpen}
        onClose={closeForm}
        size="lg"
        title={editingUser ? '編輯使用者' : '新增使用者'}
        description={editingUser ? `更新 ${editingUser.empno} 的帳號資料。` : '建立公司內可登入 ISO 文件系統的帳號。'}
        footer={(
          <>
            <Button variant="secondary" disabled={formPending} onClick={closeForm}>取消</Button>
            <Button type="submit" form="user-form" loading={formPending} loadingText="儲存中">儲存</Button>
          </>
        )}
      >
        {formError && <Alert className="mb-4" variant="error">{formError}</Alert>}
        <form id="user-form" className="grid gap-4 md:grid-cols-2" noValidate onSubmit={handleSubmit}>
          <FormField label="公司" className="md:col-span-2">
            <div className="flex h-control items-center border border-line bg-surface-header px-2 text-control text-ink-muted">
              {isSystemAdmin
                ? (selectedCompany ? `${selectedCompany.code} — ${selectedCompany.name}` : '請先選擇公司')
                : (currentUser.data?.companyName ?? '目前公司')}
            </div>
          </FormField>
          <FormField label="員工編號" htmlFor="user-empno" error={fieldErrors.empno} required>
            <Input
              id="user-empno"
              value={formValues.empno}
              readOnly={editingUser !== undefined}
              className={editingUser ? 'bg-surface-header text-ink-muted' : undefined}
              error={fieldErrors.empno !== undefined}
              aria-describedby={fieldErrors.empno ? 'user-empno-error' : undefined}
              onChange={(event) => updateField('empno', event.target.value)}
            />
          </FormField>
          <FormField label="姓名" htmlFor="user-name" error={fieldErrors.name} required>
            <Input
              id="user-name"
              value={formValues.name}
              error={fieldErrors.name !== undefined}
              aria-describedby={fieldErrors.name ? 'user-name-error' : undefined}
              onChange={(event) => updateField('name', event.target.value)}
            />
          </FormField>
          <FormField label="電子郵件" htmlFor="user-email" error={fieldErrors.email} hint="選填。">
            <Input
              id="user-email"
              type="email"
              value={formValues.email}
              error={fieldErrors.email !== undefined}
              aria-describedby={fieldErrors.email ? 'user-email-error' : 'user-email-hint'}
              onChange={(event) => updateField('email', event.target.value)}
            />
          </FormField>
          <FormField label="部門" htmlFor="user-dept" error={fieldErrors.deptId} required>
            <Select
              id="user-dept"
              value={formValues.deptId}
              error={fieldErrors.deptId !== undefined}
              aria-describedby={fieldErrors.deptId ? 'user-dept-error' : undefined}
              onChange={(event) => updateField('deptId', event.target.value)}
            >
              <option value="" disabled>請選擇部門</option>
              {departments.data?.items.map((department) => (
                <option key={department.id} value={department.id}>{department.name}</option>
              ))}
            </Select>
          </FormField>
          <FormField label="角色" htmlFor="user-role" error={fieldErrors.role} required>
            <Select
              id="user-role"
              value={formValues.role}
              error={fieldErrors.role !== undefined}
              aria-describedby={fieldErrors.role ? 'user-role-error' : undefined}
              onChange={(event) => {
                if (isManagedUserRole(event.target.value)) {
                  updateField('role', event.target.value)
                }
              }}
            >
              <option value={MANAGED_USER_ROLE.User}>{MANAGED_USER_ROLE_LABEL.USER}</option>
              <option value={MANAGED_USER_ROLE.CompanyAdmin}>{MANAGED_USER_ROLE_LABEL.COMPANY_ADMIN}</option>
              {isSystemAdmin && (
                <option value={MANAGED_USER_ROLE.SystemAdmin}>{MANAGED_USER_ROLE_LABEL.SYSTEM_ADMIN}</option>
              )}
            </Select>
          </FormField>
          {!editingUser && (
            <>
              <FormField label="密碼" htmlFor="user-password" error={fieldErrors.password} hint="選填；可使用短密碼，僅不可全部空白。">
                <Input
                  id="user-password"
                  type="password"
                  autoComplete="new-password"
                  value={formValues.password}
                  error={fieldErrors.password !== undefined}
                  aria-describedby={fieldErrors.password ? 'user-password-error' : 'user-password-hint'}
                  onChange={(event) => updateField('password', event.target.value)}
                />
              </FormField>
              <FormField label="確認密碼" htmlFor="user-password-confirmation" error={fieldErrors.passwordConfirmation}>
                <Input
                  id="user-password-confirmation"
                  type="password"
                  autoComplete="new-password"
                  value={formValues.passwordConfirmation}
                  error={fieldErrors.passwordConfirmation !== undefined}
                  aria-describedby={fieldErrors.passwordConfirmation ? 'user-password-confirmation-error' : undefined}
                  onChange={(event) => updateField('passwordConfirmation', event.target.value)}
                />
              </FormField>
            </>
          )}
          {editingUser && (
            <div className="md:col-span-2">
              <label className="flex cursor-pointer items-center gap-2 text-label text-ink">
                <input
                  type="checkbox"
                  className="size-4 accent-primary"
                  checked={formValues.isActive}
                  onChange={(event) => updateField('isActive', event.target.checked)}
                />
                啟用帳號
              </label>
            </div>
          )}
        </form>
      </Modal>

      <Modal
        open={deleteTarget !== undefined}
        onClose={() => {
          if (!deleteUser.isPending) setDeleteTarget(undefined)
        }}
        size="sm"
        title="停用使用者"
        description="停用後，使用者將無法登入；資料與歷史紀錄仍會保留。"
        footer={(
          <>
            <Button variant="secondary" disabled={deleteUser.isPending} onClick={() => setDeleteTarget(undefined)}>取消</Button>
            <Button variant="danger" loading={deleteUser.isPending} loadingText="停用中" onClick={confirmDelete}>停用使用者</Button>
          </>
        )}
      >
        {deleteError && <Alert className="mb-4" variant="error">{deleteError}</Alert>}
        <p className="text-cell text-ink">
          確定要停用「<strong className="font-semibold">{deleteTarget?.name}</strong>」嗎？
        </p>
      </Modal>

      <Modal
        open={resetTarget !== undefined}
        onClose={closeResetDialog}
        size="sm"
        title="重設使用者密碼"
        description={temporaryPassword
          ? '臨時密碼只會顯示這一次，關閉前請先複製並安全交付。'
          : `系統將為 ${resetTarget?.empno ?? ''} 產生一組臨時密碼。`}
        footer={temporaryPassword
          ? <Button onClick={closeResetDialog}>完成</Button>
          : (
              <>
                <Button variant="secondary" disabled={resetPassword.isPending} onClick={closeResetDialog}>取消</Button>
                <Button loading={resetPassword.isPending} loadingText="重設中" onClick={confirmResetPassword}>重設密碼</Button>
              </>
            )}
      >
        {resetError && <Alert className="mb-4" variant="error">{resetError}</Alert>}
        {temporaryPassword ? (
          <div className="space-y-3">
            <FormField label="臨時密碼" htmlFor="temporary-password">
              <div className="flex gap-2">
                <Input id="temporary-password" value={temporaryPassword} readOnly />
                <Button variant="secondary" onClick={() => void copyTemporaryPassword()}>複製</Button>
              </div>
            </FormField>
            {copyMessage && <p className="text-meta text-ink-muted" role="status">{copyMessage}</p>}
            <Alert variant="warning">使用者下次登入時必須變更臨時密碼。</Alert>
          </div>
        ) : (
          <p className="text-cell text-ink">重設後，原密碼會立即失效。</p>
        )}
      </Modal>
    </section>
  )
}

export default UsersPage
