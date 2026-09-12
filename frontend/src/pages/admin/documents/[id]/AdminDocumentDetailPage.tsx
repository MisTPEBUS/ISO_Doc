import { useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import { ApiError } from '@/api/httpClient'
import {
  Alert,
  Badge,
  Button,
  FormField,
  Input,
  Modal,
  Select,
  Spinner,
  Textarea,
} from '@/components/common'
import {
  useAdminDocument,
  useCreateVersion,
} from '@/features/admin-documents/queries'
import {
  createVersionFormSchema,
  todayUtc,
  type VersionFormValues,
} from '@/features/admin-documents/versionSchemas'
import {
  DOCUMENT_VERSION_STATUS,
  VERSION_CHANGE_TYPE,
  type DocumentVersionStatus,
} from '@/features/admin-documents/types'
import { useCurrentUser } from '@/features/auth/queries'
import { USER_ROLE } from '@/features/auth/types'
import { useCompanies } from '@/features/companies/queries'

const VERSION_STATUS: Record<
  DocumentVersionStatus,
  { label: string; dotClassName: string; textClassName: string }
> = {
  [DOCUMENT_VERSION_STATUS.Published]: {
    label: '已發布',
    dotClassName: 'bg-state-active',
    textClassName: 'text-state-active',
  },
  [DOCUMENT_VERSION_STATUS.Obsolete]: {
    label: '已作廢',
    dotClassName: 'bg-state-obsolete',
    textClassName: 'text-state-obsolete',
  },
}

type VersionFieldErrors = Partial<Record<keyof VersionFormValues, string>>

function emptyVersionForm(): VersionFormValues {
  return {
    changeType: VERSION_CHANGE_TYPE.Minor,
    effectiveDate: todayUtc(),
    pageCount: '',
    memo: '',
    file: null,
  }
}

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0]
}

function apiFieldMessage(
  errors: Record<string, string[]>,
  field: keyof VersionFormValues,
): string | undefined {
  const entry = Object.entries(errors).find(
    ([key]) => key.toLowerCase() === field.toLowerCase(),
  )
  return firstMessage(entry?.[1])
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'Asia/Taipei',
  }).format(new Date(value))
}

function versionErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return '目前無法連線到系統，請稍後再試。'
  }

  if (error.status === 404) {
    return '找不到這份文件，請返回清單確認文件是否仍存在。'
  }

  if (error.status === 409) {
    const message = `${error.title} ${error.detail ?? ''}`.toLowerCase()
    if (message.includes('停用') || message.includes('inactive')) {
      return '文件已停用，無法新增版本。'
    }
    return '其他管理員可能同時發布版本，請重新整理文件後再試。'
  }

  return error.detail ?? '無法新增文件版本，請稍後再試。'
}

export function AdminDocumentDetailPage() {
  const navigate = useNavigate()
  const { id } = useParams<{ id: string }>()
  const currentUser = useCurrentUser()
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin
  const document = useAdminDocument(id)
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin)
  const createVersion = useCreateVersion()
  const [versionModalOpen, setVersionModalOpen] = useState(false)
  const [versionFormKey, setVersionFormKey] = useState(0)
  const [versionValues, setVersionValues] = useState<VersionFormValues>(emptyVersionForm)
  const [versionFieldErrors, setVersionFieldErrors] = useState<VersionFieldErrors>({})
  const [versionFormError, setVersionFormError] = useState<string>()
  const [successMessage, setSuccessMessage] = useState<string>()

  function openVersionModal() {
    setVersionValues(emptyVersionForm())
    setVersionFieldErrors({})
    setVersionFormError(undefined)
    setSuccessMessage(undefined)
    setVersionFormKey((current) => current + 1)
    setVersionModalOpen(true)
  }

  function closeVersionModal() {
    if (!createVersion.isPending) setVersionModalOpen(false)
  }

  function updateVersionField(
    field: Exclude<keyof VersionFormValues, 'file'>,
    value: string,
  ) {
    setVersionValues((current) => ({ ...current, [field]: value }))
    setVersionFieldErrors((current) => ({ ...current, [field]: undefined }))
    setVersionFormError(undefined)
  }

  function handleVersionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setVersionFormError(undefined)

    const parsed = createVersionFormSchema.safeParse(versionValues)
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors
      setVersionFieldErrors({
        changeType: firstMessage(errors.changeType),
        effectiveDate: firstMessage(errors.effectiveDate),
        pageCount: firstMessage(errors.pageCount),
        memo: firstMessage(errors.memo),
        file: firstMessage(errors.file),
      })
      return
    }

    if (id === undefined) {
      setVersionFormError('找不到這份文件，請返回清單後重試。')
      return
    }

    createVersion.mutate({ documentId: id, input: parsed.data }, {
      onSuccess: (version) => {
        setVersionModalOpen(false)
        setSuccessMessage(`版本 ${version.version} 已發布，文件詳情已更新。`)
      },
      onError: (error) => {
        if (error instanceof ApiError && error.status === 400) {
          const errors = error.fieldErrors()
          setVersionFieldErrors({
            changeType: apiFieldMessage(errors, 'changeType'),
            effectiveDate: apiFieldMessage(errors, 'effectiveDate'),
            pageCount: apiFieldMessage(errors, 'pageCount'),
            memo: apiFieldMessage(errors, 'memo'),
            file: apiFieldMessage(errors, 'file'),
          })
          setVersionFormError(
            Object.keys(errors).length === 0
              ? (error.detail ?? '欄位或 PDF 檔案內容不正確，請檢查後重試。')
              : undefined,
          )
          return
        }

        setVersionFormError(versionErrorMessage(error))
      },
    })
  }

  if (document.isPending) {
    return (
      <section className="flex min-h-64 items-center justify-center border border-line-strong bg-surface">
        <div className="flex items-center gap-2 text-meta text-ink-muted" role="status">
          <Spinner decorative />
          文件載入中
        </div>
      </section>
    )
  }

  if (document.isError || document.data === undefined) {
    const message = document.error instanceof ApiError
      ? (document.error.detail ?? '找不到指定的文件。')
      : '目前無法載入文件詳情，請稍後再試。'

    return (
      <section>
        <Alert variant="error" title="無法載入文件詳情">{message}</Alert>
        <Button className="mt-4" variant="secondary" onClick={() => navigate('/admin/documents')}>
          返回文件清單
        </Button>
      </section>
    )
  }

  const detail = document.data
  const companyName = isSystemAdmin
    ? companies.data?.items.find((company) => company.id === detail.companyId)?.name
    : currentUser.data?.companyName

  return (
    <section>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <button
            type="button"
            className="mb-2 rounded-xs text-meta text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            onClick={() => navigate('/admin/documents')}
          >
            返回文件清單
          </button>
          <p className="mb-1 font-mono text-code text-primary">{detail.documentNo}</p>
          <h1 className="text-page-title text-ink">{detail.name}</h1>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant={detail.isActive ? 'success' : 'danger'}>
            {detail.isActive ? '啟用' : '停用'}
          </Badge>
          {detail.isActive && <Button onClick={openVersionModal}>新增版本</Button>}
        </div>
      </div>

      {successMessage && (
        <Alert
          className="mb-4"
          variant="success"
          title="版本建立完成"
          dismissAfterMs={3000}
          onDismiss={() => setSuccessMessage(undefined)}
        >
          {successMessage}
        </Alert>
      )}

      <div className="grid border border-line-strong bg-surface md:grid-cols-2 xl:grid-cols-4">
        <div className="border-b border-line p-4 md:border-r xl:border-b-0">
          <p className="text-label text-ink-muted">公司</p>
          <p className="mt-1 text-cell text-ink">{companyName ?? detail.companyId}</p>
        </div>
        <div className="border-b border-line p-4 xl:border-r xl:border-b-0">
          <p className="text-label text-ink-muted">文件編號</p>
          <p className="mt-1 font-mono text-code text-ink">{detail.documentNo}</p>
        </div>
        <div className="border-b border-line p-4 md:border-r md:border-b-0">
          <p className="text-label text-ink-muted">建立時間</p>
          <p className="mt-1 text-meta text-ink tabular">{formatDateTime(detail.createdAt)}</p>
        </div>
        <div className="p-4">
          <p className="text-label text-ink-muted">最後更新</p>
          <p className="mt-1 text-meta text-ink tabular">{formatDateTime(detail.updatedAt)}</p>
        </div>
      </div>

      <section className="mt-4 border border-line-strong bg-surface" aria-labelledby="version-history-title">
        <div className="border-b border-line px-4 py-3">
          <h2 id="version-history-title" className="text-section-label text-ink">版本歷程</h2>
          <p className="mt-1 text-meta text-ink-muted">依版本由新至舊顯示發布與作廢紀錄。</p>
        </div>

        {detail.versions.length === 0 ? (
          <div className="p-12 text-center">
            <p className="text-cell font-medium text-ink">尚未建立任何版本</p>
            <p className="mt-1 text-meta text-ink-muted">
              {detail.isActive ? '點選「新增版本」上傳第一份 PDF。' : '文件已停用，無法新增版本。'}
            </p>
          </div>
        ) : (
          <ol className="divide-y divide-line">
            {detail.versions.map((version) => {
              const status = VERSION_STATUS[version.status]
              return (
                <li key={version.version} className="grid gap-3 px-4 py-3 md:grid-cols-[8rem_1fr_1fr] md:items-center">
                  <div className="flex items-center gap-2">
                    <span className={`size-1.5 shrink-0 rounded-full ${status.dotClassName}`} aria-hidden="true" />
                    <span className={`text-label font-medium ${status.textClassName}`}>{status.label}</span>
                  </div>
                  <div>
                    <p className="text-label text-ink-muted">版本</p>
                    <p className="font-mono text-revision text-ink">{version.version}</p>
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <p className="text-label text-ink-muted">生效日期</p>
                      <p className="text-meta text-ink tabular">{version.effectiveDate ?? '－'}</p>
                    </div>
                    <div>
                      <p className="text-label text-ink-muted">失效日期</p>
                      <p className="text-meta text-ink tabular">{version.expiredDate ?? '－'}</p>
                    </div>
                  </div>
                </li>
              )
            })}
          </ol>
        )}
      </section>

      <Modal
        open={versionModalOpen}
        onClose={closeVersionModal}
        title="新增文件版本"
        description={`${detail.documentNo}｜${detail.name}`}
        size="md"
        closeOnBackdrop={!createVersion.isPending}
        closeOnEscape={!createVersion.isPending}
        footer={(
          <>
            <Button variant="secondary" disabled={createVersion.isPending} onClick={closeVersionModal}>
              取消
            </Button>
            <Button
              type="submit"
              form="create-version-form"
              loading={createVersion.isPending}
              loadingText="上傳中"
            >
              上傳並發布
            </Button>
          </>
        )}
      >
        <form key={versionFormKey} id="create-version-form" className="space-y-4" onSubmit={handleVersionSubmit}>
          {versionFormError && (
            <Alert variant="error" title="無法新增版本">{versionFormError}</Alert>
          )}

          <Alert variant="info" title="發布方式">
            新版本上傳後會立即發布；目前的已發布版本會自動轉為已作廢。
          </Alert>

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="變更類型" htmlFor="version-change-type" error={versionFieldErrors.changeType} required>
              <Select
                id="version-change-type"
                value={versionValues.changeType}
                error={versionFieldErrors.changeType !== undefined}
                aria-describedby={versionFieldErrors.changeType ? 'version-change-type-error' : undefined}
                onChange={(event) => updateVersionField('changeType', event.target.value)}
              >
                <option value={VERSION_CHANGE_TYPE.Minor}>次要變更（MINOR）</option>
                <option value={VERSION_CHANGE_TYPE.Major}>主要變更（MAJOR）</option>
              </Select>
            </FormField>

            <FormField label="生效日期" htmlFor="version-effective-date" error={versionFieldErrors.effectiveDate} required>
              <Input
                id="version-effective-date"
                type="date"
                min={todayUtc()}
                value={versionValues.effectiveDate}
                error={versionFieldErrors.effectiveDate !== undefined}
                aria-describedby={versionFieldErrors.effectiveDate ? 'version-effective-date-error' : undefined}
                onChange={(event) => updateVersionField('effectiveDate', event.target.value)}
              />
            </FormField>
          </div>

          <FormField label="頁數" htmlFor="version-page-count" error={versionFieldErrors.pageCount} hint="選填；請輸入大於 0 的整數。">
            <Input
              id="version-page-count"
              type="number"
              min="1"
              step="1"
              inputMode="numeric"
              value={versionValues.pageCount}
              error={versionFieldErrors.pageCount !== undefined}
              aria-describedby={versionFieldErrors.pageCount ? 'version-page-count-error' : 'version-page-count-hint'}
              onChange={(event) => updateVersionField('pageCount', event.target.value)}
            />
          </FormField>

          <FormField label="備註" htmlFor="version-memo" error={versionFieldErrors.memo} hint="選填。">
            <Textarea
              id="version-memo"
              value={versionValues.memo}
              error={versionFieldErrors.memo !== undefined}
              aria-describedby={versionFieldErrors.memo ? 'version-memo-error' : 'version-memo-hint'}
              onChange={(event) => updateVersionField('memo', event.target.value)}
            />
          </FormField>

          <FormField label="PDF 檔案" htmlFor="version-file" error={versionFieldErrors.file} hint="僅接受副檔名為 .pdf 的檔案，檔名最多 255 個字元。" required>
            <Input
              id="version-file"
              type="file"
              accept=".pdf,application/pdf"
              error={versionFieldErrors.file !== undefined}
              aria-describedby={versionFieldErrors.file ? 'version-file-error' : 'version-file-hint'}
              onChange={(event) => {
                setVersionValues((current) => ({
                  ...current,
                  file: event.target.files?.[0] ?? null,
                }))
                setVersionFieldErrors((current) => ({ ...current, file: undefined }))
                setVersionFormError(undefined)
              }}
            />
          </FormField>
        </form>
      </Modal>
    </section>
  )
}

export default AdminDocumentDetailPage
