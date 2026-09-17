import {
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react'

import { Pagination } from '@/components/common'

import type {
  PermissionMatrixDepartment,
  PermissionMatrixEffectiveStatus,
  PermissionMatrixFileStatus,
  PermissionMatrixItem,
} from '../types'

interface PermissionMatrixTableProps {
  departments: ReadonlyArray<PermissionMatrixDepartment>
  items: ReadonlyArray<PermissionMatrixItem>
  allItems: ReadonlyArray<PermissionMatrixItem>
  selectionOverrides: Readonly<Record<string, ReadonlySet<string>>>
  loading?: boolean
  allItemsLoading?: boolean
  page: number
  pageSize: number
  totalCount: number
  onToggle: (document: PermissionMatrixItem, departmentId: string) => void
  onToggleAll: (document: PermissionMatrixItem) => void
  onToggleDepartmentAll: (departmentId: string) => void
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

type StatusTone = 'active' | 'review' | 'expiring' | 'obsolete' | 'danger'

interface StatusToneClasses {
  dot: string
  text: string
}

const STATUS_TONE_CLASSES: Record<StatusTone, StatusToneClasses> = {
  active: { dot: 'bg-state-active', text: 'text-state-active' },
  review: { dot: 'bg-state-review', text: 'text-state-review' },
  expiring: { dot: 'bg-state-expiring', text: 'text-state-expiring' },
  obsolete: { dot: 'bg-state-obsolete', text: 'text-state-obsolete' },
  danger: { dot: 'bg-state-danger', text: 'text-state-danger' },
}

function fileStatusTone(status: PermissionMatrixFileStatus): StatusTone {
  if (status.hasError) return 'danger'
  if (status.code === 'NORMAL') return 'active'
  return 'obsolete'
}

function effectiveStatusTone(
  status: PermissionMatrixEffectiveStatus,
): StatusTone {
  if (status.code === 'EFFECTIVE') return 'active'
  if (status.code === 'SCHEDULED') return 'expiring'
  if (status.code === 'DRAFT') return 'review'
  return 'obsolete'
}

interface StatusTextProps {
  label: string
  tone: StatusTone
  icon?: LucideIcon
  iconClassName?: string
}

function StatusText({ label, tone, icon: Icon, iconClassName }: StatusTextProps) {
  const toneClasses = STATUS_TONE_CLASSES[tone]

  return (
    <span className={`inline-flex items-center gap-1.5 text-label ${toneClasses.text}`}>
      {Icon === undefined ? (
        <span className={`size-1.5 shrink-0 rounded-full ${toneClasses.dot}`} aria-hidden="true" />
      ) : (
        <Icon className={`size-4 shrink-0 ${iconClassName ?? ''}`} strokeWidth={1.75} aria-hidden="true" />
      )}
      {label}
    </span>
  )
}

function MatrixLoading() {
  return (
    <div className="overflow-x-auto bg-surface" aria-busy="true">
      <span className="sr-only" role="status">ISO 文件權限矩陣載入中</span>
      <table className="w-max min-w-full border-collapse" aria-hidden="true">
        <thead className="bg-surface-header">
          <tr>
            {['w-44', 'w-16', 'w-52', 'w-32', 'w-32', 'w-32', 'w-32'].map((width, index) => (
              <th key={index} className="h-table-header border-b border-line-strong px-4">
                <span className={`block h-3 animate-pulse rounded-xs bg-line-strong ${width}`} />
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {Array.from({ length: 6 }, (_, rowIndex) => (
            <tr key={rowIndex}>
              {Array.from({ length: 7 }, (_, columnIndex) => (
                <td key={columnIndex} className="h-row px-4">
                  <span className={`block h-3 animate-pulse rounded-xs bg-surface-header ${columnIndex < 2 ? 'w-32' : 'mx-auto w-4'}`} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function PermissionMatrixTable({
  departments,
  items,
  allItems,
  selectionOverrides,
  loading = false,
  allItemsLoading = false,
  page,
  pageSize,
  totalCount,
  onToggle,
  onToggleAll,
  onToggleDepartmentAll,
  onPageChange,
  onPageSizeChange,
}: PermissionMatrixTableProps) {
  return (
    <div className="flex min-h-0 flex-col bg-surface">
      {loading ? (
        <MatrixLoading />
      ) : items.length === 0 ? (
        <div className="p-12 text-center">
          <p className="text-cell font-medium text-ink">這家公司還沒有 ISO 文件</p>
          <p className="mt-1 text-meta text-ink-muted">建立文件後即可在這裡設定部門檢視權限。</p>
        </div>
      ) : (
        <div
          className="max-h-[calc(100dvh-19rem)] min-h-0 overflow-auto"
          tabIndex={0}
          aria-label="ISO 文件權限矩陣，可水平及垂直捲動"
        >
          <table className="w-max min-w-full border-collapse text-left text-cell">
            <caption className="sr-only">ISO 文件與部門檢視權限</caption>
            <thead className="bg-surface-header text-ink-muted">
              <tr>
                <th
                  scope="col"
                  className="sticky top-0 left-0 z-30 h-16 w-52 min-w-52 max-w-52 border-r border-b border-line-strong bg-surface-header px-4 text-table-header shadow-sticky-y"
                >
                  ISO 主文
                </th>
                <th
                  scope="col"
                  className="sticky top-0 left-52 z-30 h-16 w-20 min-w-20 max-w-20 border-r border-b border-line-strong bg-surface-header px-4 text-center text-table-header whitespace-nowrap shadow-sticky-x"
                >
                  全選
                </th>
                <th scope="col" className="sticky top-0 z-20 h-16 min-w-48 border-b border-line-strong bg-surface-header px-4 text-table-header shadow-sticky-y">
                  狀態
                </th>
                {departments.map((department) => {
                  const selectedDocumentCount = allItems.reduce((count, document) => {
                    const selectedDepartmentIds = selectionOverrides[document.documentId]
                      ?? new Set(document.departmentIds)
                    return count + Number(selectedDepartmentIds.has(department.id))
                  }, 0)
                  const allDocumentsSelected = allItems.length > 0
                    && selectedDocumentCount === allItems.length
                  const someDocumentsSelected = selectedDocumentCount > 0
                    && !allDocumentsSelected
                  const checkboxDisabled = allItemsLoading || allItems.length === 0

                  return (
                    <th
                      key={department.id}
                      scope="col"
                      className="sticky top-0 z-20 h-16 min-w-32 border-b border-line-strong bg-surface-header px-4 text-center text-table-header whitespace-nowrap shadow-sticky-y"
                    >
                      <div className="flex flex-col items-center justify-center gap-1">
                        <label
                          className={`inline-flex size-6 items-center justify-center rounded-sm focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-primary ${checkboxDisabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer hover:bg-primary-subtle'}`}
                          title={`全選或取消全選${department.name}可查看的所有文件`}
                        >
                          <input
                            ref={(input) => {
                              if (input !== null) input.indeterminate = someDocumentsSelected
                            }}
                            type="checkbox"
                            className="size-4 cursor-inherit rounded-xs border-line-strong accent-primary"
                            checked={allDocumentsSelected}
                            disabled={checkboxDisabled}
                            onChange={() => onToggleDepartmentAll(department.id)}
                            aria-label={`${allDocumentsSelected ? '取消' : ''}全選${department.name}可查看的所有文件`}
                          />
                        </label>
                        <span>{department.name}</span>
                      </div>
                    </th>
                  )
                })}
              </tr>
            </thead>
            <tbody className="divide-y divide-line text-ink">
              {items.map((document) => {
                const selectedDepartmentIds = selectionOverrides[document.documentId]
                  ?? new Set(document.departmentIds)
                const selectedCount = departments.reduce(
                  (count, department) => count + Number(selectedDepartmentIds.has(department.id)),
                  0,
                )
                const allDepartmentsSelected = departments.length > 0
                  && selectedCount === departments.length
                const someDepartmentsSelected = selectedCount > 0
                  && !allDepartmentsSelected

                return (
                  <tr key={document.documentId} className="group even:bg-surface-zebra hover:bg-surface-hover">
                    <th
                      scope="row"
                      className="sticky left-0 z-10 w-52 min-w-52 max-w-52 border-r border-line bg-surface px-4 py-2 text-left group-even:bg-surface-zebra group-hover:bg-surface-hover"
                    >
                      <span className="block font-mono text-code text-primary">{document.documentCode}</span>
                      <span className="mt-0.5 block max-w-48 truncate text-meta font-normal text-ink-muted" title={document.documentName}>
                        {document.documentName}
                        {document.version === null ? '' : ` · V${document.version}`}
                      </span>
                    </th>
                    <td className="sticky left-52 z-10 w-20 min-w-20 max-w-20 border-r border-line bg-surface px-4 py-2 text-center align-middle shadow-sticky-x group-even:bg-surface-zebra group-hover:bg-surface-hover">
                      <label className="inline-flex size-9 cursor-pointer items-center justify-center rounded-sm hover:bg-primary-subtle focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-primary">
                        <input
                          ref={(input) => {
                            if (input !== null) input.indeterminate = someDepartmentsSelected
                          }}
                          type="checkbox"
                          className="size-4 cursor-pointer rounded-xs border-line-strong accent-primary"
                          checked={allDepartmentsSelected}
                          disabled={departments.length === 0}
                          onChange={() => onToggleAll(document)}
                          aria-label={`${allDepartmentsSelected ? '取消全選' : '全選'} ${document.documentCode} 的所有部門`}
                        />
                      </label>
                    </td>
                    <td className="px-4 py-2 align-middle">
                      <div className="flex min-w-44 flex-col items-start gap-0.5">
                        {document.status.mainDocument.code !== 'NORMAL' && (
                          <StatusText
                            label={document.status.mainDocument.label}
                            tone={fileStatusTone(document.status.mainDocument)}
                            icon={document.status.mainDocument.code === 'MISSING'
                              ? TriangleAlert
                              : undefined}
                            iconClassName="text-state-danger"
                          />
                        )}
                        {document.status.attachment.code !== 'NORMAL' && (
                          <StatusText
                            label={document.status.attachment.label}
                            tone={fileStatusTone(document.status.attachment)}
                            icon={document.status.attachment.code === 'NONE'
                              ? TriangleAlert
                              : undefined}
                            iconClassName="text-state-danger"
                          />
                        )}
                        {document.status.effective.code !== 'DRAFT' && (
                          <StatusText
                            label={document.status.effective.label}
                            tone={effectiveStatusTone(document.status.effective)}
                          />
                        )}
                      </div>
                    </td>
                    {departments.map((department) => {
                      const checked = selectedDepartmentIds.has(department.id)

                      return (
                        <td key={department.id} className="px-4 py-2 text-center align-middle">
                          <label className="inline-flex size-9 cursor-pointer items-center justify-center rounded-sm hover:bg-primary-subtle focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-primary">
                            <input
                              type="checkbox"
                              className="size-4 cursor-pointer rounded-xs border-line-strong accent-primary"
                              checked={checked}
                              onChange={() => onToggle(document, department.id)}
                              aria-label={`${checked ? '取消' : '授予'}${department.name}查看 ${document.documentCode} 的權限`}
                            />
                          </label>
                        </td>
                      )
                    })}
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      <Pagination
        className="shrink-0 border-t border-line bg-surface px-4"
        page={page}
        pageSize={pageSize}
        pageSizeOptions={[10, 20, 50, 100]}
        totalCount={totalCount}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
      />
    </div>
  )
}
