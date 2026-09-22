import { useState } from 'react'

import { ApiError } from '@/api/httpClient'
import { Alert, Button, FormField, Select } from '@/components/common'
import { useCurrentUser } from '@/features/auth/queries'
import { USER_ROLE } from '@/features/auth/types'
import { useDownloadBackup } from '@/features/backup/queries'
import { useCompanies } from '@/features/companies/queries'

function backupErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return '目前無法下載備份，請稍後再試。'
  }

  if (error.status === 403) {
    return '你沒有下載這家公司備份的權限。'
  }

  if (error.status === 404) {
    return '找不到這家公司，請重新選擇後再試。'
  }

  return error.detail ?? '目前無法下載備份，請稍後再試。'
}

export function BackupPage() {
  const currentUser = useCurrentUser()
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin
  const [selectedCompanyId, setSelectedCompanyId] = useState(
    currentUser.data?.companyId ?? '',
  )
  const [downloadError, setDownloadError] = useState<string>()
  const [downloadedFileName, setDownloadedFileName] = useState<string>()
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin)
  const downloadBackup = useDownloadBackup()
  const companyId = isSystemAdmin
    ? (selectedCompanyId || undefined)
    : currentUser.data?.companyId
  const selectedCompany = isSystemAdmin
    ? companies.data?.items.find((company) => company.id === companyId)
    : currentUser.data === null || currentUser.data === undefined
      ? undefined
      : {
          id: currentUser.data.companyId,
          name: currentUser.data.companyName,
        }

  function handleDownload() {
    if (companyId === undefined || downloadBackup.isPending) return

    setDownloadError(undefined)
    setDownloadedFileName(undefined)
    downloadBackup.mutate(companyId, {
      onSuccess: (result) => setDownloadedFileName(result.fileName),
      onError: (error) => setDownloadError(backupErrorMessage(error)),
    })
  }

  return (
    <section>
      <div className="mb-4">
        <p className="mb-1 text-label font-medium text-primary">文件管理</p>
        <h1 className="text-page-title text-ink">ISO 文件備份</h1>
        <p className="mt-1 text-meta text-ink-muted">
          下載公司目前已發布的ISO管理程序與已上傳表單及附件。
        </p>
      </div>

      {companies.isError && isSystemAdmin && (
        <Alert className="mb-4" variant="error" title="無法載入公司">
          {companies.error instanceof ApiError
            ? (companies.error.detail ?? '請稍後重新整理頁面。')
            : '目前無法連線到系統，請稍後再試。'}
        </Alert>
      )}

      {downloadError && (
        <Alert className="mb-4" variant="error" title="無法下載備份">
          {downloadError}
        </Alert>
      )}

      {downloadedFileName && (
        <Alert
          className="mb-4"
          variant="success"
          title="備份下載已開始"
          dismissAfterMs={3000}
          onDismiss={() => setDownloadedFileName(undefined)}
        >
          {downloadedFileName}
        </Alert>
      )}

      <div className="max-w-2xl border border-line-strong bg-surface">
        <div className="border-b border-line px-4 py-3">
          <h2 className="text-section-label text-ink">公司文件 ZIP</h2>
          <p className="mt-1 text-meta text-ink-muted">
            備份由後端即時串流產生；檔案較多時需要較長處理時間。
          </p>
        </div>

        <div className="space-y-4 p-4">
          {isSystemAdmin ? (
            <FormField label="公司" htmlFor="backup-company" hint="選擇要下載備份的公司。">
              <Select
                id="backup-company"
                value={selectedCompanyId}
                disabled={companies.isPending || companies.isError || downloadBackup.isPending}
                onChange={(event) => {
                  setSelectedCompanyId(event.target.value)
                  setDownloadError(undefined)
                  setDownloadedFileName(undefined)
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
          ) : (
            <div>
              <p className="text-label text-ink-muted">備份公司</p>
              <p className="mt-1 text-cell font-medium text-ink">
                {selectedCompany?.name ?? '公司資料載入中'}
              </p>
            </div>
          )}

          <Alert variant="info" title="備份內容">
            僅包含每份文件目前已發布版本的ISO管理程序，以及已有檔案的表單及附件；不包含已作廢版本。
          </Alert>

          <Button
            loading={downloadBackup.isPending}
            loadingText="備份產生與下載中"
            disabled={companyId === undefined || companies.isError}
            onClick={handleDownload}
          >
            下載 ZIP 備份
          </Button>
        </div>
      </div>
    </section>
  )
}

export default BackupPage
