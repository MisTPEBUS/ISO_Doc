import { useRef, useState, type ChangeEvent, type DragEvent, type KeyboardEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'

import { ApiError } from '@/api/httpClient'
import {
  Alert,
  Badge,
  Button,
  FormField,
  Input,
  Select,
  Table,
  type TableColumn,
} from '@/components/common'
import {
  blankDraftRow,
  buildRowsFromMapping,
  ExcelParseError,
  MAPPING_TARGETS,
  MAX_BULK_IMPORT_ROWS,
  parseDocumentsWorkbook,
  type ColumnMapping,
  type MappingTargetKey,
} from '@/features/admin-documents/bulkImportExcel'
import { resolveImportDept } from '@/features/admin-documents/bulkImportDepartment'
import { useBulkImportAdminDocuments } from '@/features/admin-documents/queries'
import type { BulkImportDocumentItem } from '@/features/admin-documents/types'
import { useCurrentUser } from '@/features/auth/queries'
import { USER_ROLE } from '@/features/auth/types'
import { useCompanies } from '@/features/companies/queries'
import { useCompanyDeptOptions } from '@/features/departments/queries'

type RowSource = 'manual' | 'excel'
type RowStatus = 'editing' | 'success' | 'failed'

interface ImportRow {
  deptId: string
  departmentCleared: boolean
  id: string
  source: RowSource
  documentNo: string
  name: string
  effectiveDate: string
  version: string
  status: RowStatus
  errorMessage: string | null
}

interface ExcelState {
  fileName: string
  worksheetName: string
  sourceHeaders: string[]
  sourceRows: string[][]
  mapping: ColumnMapping
}

interface ImportSummary {
  total: number
  successCount: number
  failureCount: number
}

function createId(): string {
  return typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `row-${Date.now()}-${Math.random().toString(16).slice(2)}`
}

function makeRow(source: RowSource, draft = blankDraftRow()): ImportRow {
  return { id: createId(), source, ...draft, departmentCleared: false, status: 'editing', errorMessage: null }
}

function formatServerErrors(errors: Record<string, string[]>): string {
  return Object.values(errors).flat().join('；')
}

function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.detail ?? fallback
  }
  return '目前無法連線到系統，請稍後再試。'
}

export function AdminDocumentImportPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const navigationState = location.state as { companyId?: string } | null

  const currentUser = useCurrentUser()
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin
  const [selectedCompanyId, setSelectedCompanyId] = useState(
    navigationState?.companyId ?? currentUser.data?.companyId ?? '',
  )
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin)
  const companyId = isSystemAdmin
    ? selectedCompanyId || undefined
    : currentUser.data?.companyId
  const depts = useCompanyDeptOptions(companyId)
  const deptNameById = new Map((depts.data ?? []).map((dept) => [dept.id.toLowerCase(), dept.name]))
  function resolveDept(row: ImportRow) {
    if (row.departmentCleared) return { id: null, unknownExplicit: false }
    return resolveImportDept(row.deptId, row.documentNo, depts.data ?? [])
  }

  const fileInputRef = useRef<HTMLInputElement>(null)
  const [rows, setRows] = useState<ImportRow[]>([])
  const [excelState, setExcelState] = useState<ExcelState>()
  const [parsing, setParsing] = useState(false)
  const [isDragging, setIsDragging] = useState(false)
  const [fileError, setFileError] = useState<string>()
  const [submitError, setSubmitError] = useState<string>()
  const [summary, setSummary] = useState<ImportSummary>()
  const [submitted, setSubmitted] = useState(false)

  const bulkImport = useBulkImportAdminDocuments()

  const manualCount = rows.filter((row) => row.source === 'manual').length
  const excelCount = rows.filter((row) => row.source === 'excel').length

  function applyMapping(mapping: ColumnMapping, sourceRows: readonly string[][]) {
    const draftRows = buildRowsFromMapping(sourceRows, mapping)
    setRows((current) => [
      ...current.filter((row) => row.source !== 'excel'),
      ...draftRows.map((draft) => makeRow('excel', draft)),
    ])
  }

  async function handleFile(file: File) {
    setFileError(undefined)
    setSummary(undefined)
    setSubmitted(false)
    setParsing(true)
    try {
      const parsed = await parseDocumentsWorkbook(file)
      if (parsed.sourceRows.length > MAX_BULK_IMPORT_ROWS) {
        setFileError(
          `一次最多可匯入 ${MAX_BULK_IMPORT_ROWS} 筆，這個檔案有 ${parsed.sourceRows.length} 筆，請拆分後再匯入。`,
        )
        return
      }

      setExcelState({
        fileName: file.name,
        worksheetName: parsed.worksheetName,
        sourceHeaders: parsed.sourceHeaders,
        sourceRows: parsed.sourceRows,
        mapping: parsed.mapping,
      })
      applyMapping(parsed.mapping, parsed.sourceRows)
    } catch (error) {
      setFileError(
        error instanceof ExcelParseError
          ? error.message
          : '無法解析這個檔案，請確認格式為 .xlsx 或 .xls。',
      )
    } finally {
      setParsing(false)
    }
  }

  function handleFileInputChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (file) void handleFile(file)
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault()
    setIsDragging(false)
    const file = event.dataTransfer.files[0]
    if (file) void handleFile(file)
  }

  function handleDropZoneKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      fileInputRef.current?.click()
    }
  }

  function handleMappingChange(key: MappingTargetKey, value: string) {
    if (!excelState) return

    const mapping: ColumnMapping = {
      ...excelState.mapping,
      [key]: value === '' ? null : Number(value),
    }
    setExcelState({ ...excelState, mapping })
    applyMapping(mapping, excelState.sourceRows)
  }

  function clearExcel() {
    setExcelState(undefined)
    setRows((current) => current.filter((row) => row.source !== 'excel'))
    setFileError(undefined)
  }

  function addManualRow() {
    setRows((current) => [...current, makeRow('manual')])
  }

  function removeRow(id: string) {
    setRows((current) => current.filter((row) => row.id !== id))
  }

  function updateRow(id: string, patch: Partial<Omit<ImportRow, 'id' | 'source' | 'status' | 'errorMessage'>>) {
    setRows((current) => current.map((row) => (row.id === id ? { ...row, ...patch } : row)))
  }

  function resetAll() {
    setRows([])
    setExcelState(undefined)
    setSubmitted(false)
    setSummary(undefined)
    setFileError(undefined)
    setSubmitError(undefined)
  }

  function handleSubmit() {
    if (companyId === undefined) {
      setSubmitError('請先選擇公司。')
      return
    }
    if (rows.length === 0) return

    setSubmitError(undefined)
    if (rows.some((row) => resolveDept(row).unknownExplicit)) {
      setSubmitError('部分發行部門不屬於所選公司，請在表格重新選擇或清空後再送出。')
      return
    }
    const items: BulkImportDocumentItem[] = rows.map((row) => ({
      deptId: resolveDept(row).id,
      documentNo: row.documentNo.trim(),
      name: row.name.trim(),
      effectiveDate: row.effectiveDate.trim() === '' ? null : row.effectiveDate.trim(),
      version: row.version.trim(),
    }))

    bulkImport.mutate(
      { companyId, items },
      {
        onSuccess: (response) => {
          const successByIndex = new Map(response.succeeded.map((item) => [item.index, item]))
          const failedByIndex = new Map(response.failed.map((item) => [item.index, item]))

          setRows((current) =>
            current.map((row, arrayIndex) => {
              const position = arrayIndex + 1
              if (successByIndex.has(position)) {
                return { ...row, status: 'success', errorMessage: null }
              }
              const failure = failedByIndex.get(position)
              return {
                ...row,
                status: 'failed',
                errorMessage: failure ? formatServerErrors(failure.errors) : '未知錯誤。',
              }
            }),
          )
          setSummary({
            total: response.total,
            successCount: response.successCount,
            failureCount: response.failureCount,
          })
          setSubmitted(true)
        },
        onError: (error) => setSubmitError(describeError(error, '無法匯入文件資料。')),
      },
    )
  }

  const columns: ReadonlyArray<TableColumn<ImportRow>> = [
    {
      key: 'source',
      header: '來源',
      headerClassName: 'w-20',
      render: (row) => (
        <Badge variant={row.source === 'excel' ? 'info' : 'neutral'}>
          {row.source === 'excel' ? 'Excel' : '手動'}
        </Badge>
      ),
    },
    {
      key: 'documentNo',
      header: '文件編號',
      headerClassName: 'w-40',
      render: (row) =>
        submitted ? (
          <span className="tabular">{row.documentNo || '－'}</span>
        ) : (
          <Input
            value={row.documentNo}
            disabled={bulkImport.isPending}
            onChange={(event) => updateRow(row.id, { documentNo: event.target.value })}
          />
        ),
    },
    {
      key: 'name',
      header: '文件名稱',
      headerClassName: 'min-w-56',
      render: (row) =>
        submitted ? (
          row.name || '－'
        ) : (
          <Input
            value={row.name}
            disabled={bulkImport.isPending}
            onChange={(event) => updateRow(row.id, { name: event.target.value })}
          />
        ),
    },
    {
      key: 'deptId',
      header: '發行部門',
      headerClassName: 'min-w-48',
      render: (row) => {
        const resolved = resolveDept(row)
        const selectedId = resolved.id?.toLowerCase() ?? ''
        return submitted ? (
          deptNameById.get(selectedId) ?? row.deptId
        ) : (
          <div>
            <Select
              aria-label={`發行部門 ${row.documentNo || '未編號文件'}`}
              value={resolved.unknownExplicit ? row.deptId : selectedId}
              disabled={companyId === undefined || depts.isPending || depts.isError || bulkImport.isPending}
              error={resolved.unknownExplicit && depts.isSuccess}
              onChange={(event) => updateRow(row.id, {
                deptId: event.target.value,
                departmentCleared: event.target.value === '',
              })}
            >
              <option value="">不指定發行部門</option>
              {resolved.unknownExplicit && <option value={row.deptId}>待確認：{row.deptId}</option>}
              {depts.data?.map((dept) => <option key={dept.id} value={dept.id.toLowerCase()}>{dept.name}</option>)}
            </Select>
            {resolved.unknownExplicit && depts.isSuccess && <p className="mt-1 text-meta text-state-danger">請選擇此公司的部門或清空。</p>}
            {resolved.id && !row.deptId.trim() && <p className="mt-1 text-fine text-ink-muted">依文件編號推定：{resolved.inferredName}</p>}
          </div>
        )
      },
    },
    {
      key: 'effectiveDate',
      header: '生效日期',
      headerClassName: 'w-40',
      render: (row) =>
        submitted ? (
          <span className="tabular">{row.effectiveDate || '－'}</span>
        ) : (
          <Input
            type="date"
            value={row.effectiveDate}
            disabled={bulkImport.isPending}
            onChange={(event) => updateRow(row.id, { effectiveDate: event.target.value })}
          />
        ),
    },
    {
      key: 'version',
      header: '版本',
      headerClassName: 'w-28',
      render: (row) =>
        submitted ? (
          <span className="tabular">{row.version || '－'}</span>
        ) : (
          <Input
            placeholder="例如 1.0"
            value={row.version}
            disabled={bulkImport.isPending}
            onChange={(event) => updateRow(row.id, { version: event.target.value })}
          />
        ),
    },
    {
      key: 'status',
      header: submitted ? '狀態' : '操作',
      headerClassName: 'w-44',
      render: (row) => {
        if (!submitted) {
          return (
            <Button
              variant="ghost"
              size="sm"
              className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
              disabled={bulkImport.isPending}
              onClick={() => removeRow(row.id)}
            >
              移除
            </Button>
          )
        }
        if (row.status === 'success') {
          return <Badge variant="success">成功（待補檔）</Badge>
        }
        if (row.status === 'failed') {
          return (
            <div className="space-y-0.5">
              <Badge variant="danger">失敗</Badge>
              <p className="text-meta text-state-danger">{row.errorMessage}</p>
            </div>
          )
        }
        return <Badge variant="neutral">待送出</Badge>
      },
    },
  ]

  return (
    <section>
      <input
        ref={fileInputRef}
        type="file"
        accept=".xlsx,.xls"
        hidden
        onChange={handleFileInputChange}
      />

      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="mb-1 text-label font-medium text-primary">文件管理</p>
          <h1 className="text-page-title text-ink">批次匯入 ISO 文件</h1>
          <p className="mt-1 text-meta text-ink-muted">
            可直接在表格新增一筆資料，也可以拖曳或選擇 Excel 檔案匯入。一次最多{' '}
            {MAX_BULK_IMPORT_ROWS} 筆。
          </p>
          <p className="mt-1 text-meta text-ink-muted">未填發行部門時，依文件編號 GM／GA／OP／MT／FN／IT／HR 推定；仍可逐筆修改。</p>
        </div>
        <Button variant="secondary" onClick={() => navigate('/admin/documents')}>
          返回文件列表
        </Button>
      </div>

      {isSystemAdmin && (
        <div className="mb-4 border border-line-strong bg-surface p-4">
          <FormField
            className="w-full md:w-72"
            label="公司"
            htmlFor="import-company"
            hint={rows.length > 0 ? '清除全部資料後才能變更公司。' : undefined}
          >
            <Select
              id="import-company"
              value={selectedCompanyId}
              disabled={companies.isPending || companies.isError || rows.length > 0}
              onChange={(event) => setSelectedCompanyId(event.target.value)}
            >
              <option value="">請選擇公司</option>
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
        <Alert className="mb-4" variant="error" title="無法載入公司清單">
          請重新整理頁面後再試一次。
        </Alert>
      )}
      {fileError && (
        <Alert className="mb-4" variant="error" title="無法匯入">
          {fileError}
        </Alert>
      )}
      {depts.isError && (
        <Alert className="mb-4" variant="error" title="無法載入發行部門">
          請重新載入部門清單後再送出。
          <Button variant="secondary" size="sm" onClick={() => void depts.refetch()}>重新載入</Button>
        </Alert>
      )}
      {submitError && (
        <Alert className="mb-4" variant="error">
          {submitError}
        </Alert>
      )}
      {summary && (
        <Alert
          className="mb-4"
          variant={summary.failureCount === 0 ? 'success' : 'warning'}
          title="匯入結果"
        >
          共 {summary.total} 筆，成功 {summary.successCount} 筆，失敗 {summary.failureCount} 筆。
        </Alert>
      )}

      {rows.length === 0 && (
        <section className="mb-4 border border-line-strong bg-surface">
          <div className="flex h-row items-center border-b border-line bg-surface-header px-4">
            <h2 className="text-section-label text-ink">Excel 匯入</h2>
          </div>
          <div className="p-4">
            <div
              role="button"
              tabIndex={0}
              aria-label="拖曳或點擊選擇 Excel 檔案"
              className={`grid min-h-36 cursor-pointer place-items-center border-2 border-dashed px-6 py-8 text-center transition-colors ${
                isDragging ? 'border-primary bg-primary-subtle' : 'border-line-strong bg-canvas'
              }`}
              onClick={() => fileInputRef.current?.click()}
              onKeyDown={handleDropZoneKeyDown}
              onDragEnter={(event) => {
                event.preventDefault()
                setIsDragging(true)
              }}
              onDragOver={(event) => {
                event.preventDefault()
                setIsDragging(true)
              }}
              onDragLeave={(event) => {
                event.preventDefault()
                setIsDragging(false)
              }}
              onDrop={handleDrop}
            >
              <div>
                <p className="font-semibold text-ink">拖曳 Excel 到這裡，或點擊選擇檔案</p>
                <p className="mt-1.5 text-meta text-ink-muted">
                  支援 .xlsx / .xls，會自動尋找表頭。
                </p>
                <p className="mt-1 text-fine text-ink-muted">
                  生效日期支援西元 2025-04-11、2025/04/11，以及民國 114/04/11。
                </p>
              </div>
            </div>
            {parsing && <p className="mt-3 text-meta text-ink-muted">正在解析 Excel…</p>}
            <p className="mt-3 text-meta text-ink-muted">
              或直接在下方「文件資料」表格按「＋新增一筆」輸入資料。
            </p>
          </div>
        </section>
      )}

      {excelState && (
        <section className="mb-4 border border-line-strong bg-surface">
          <div className="flex h-row flex-wrap items-center justify-between gap-2 border-b border-line bg-surface-header px-4">
            <h2 className="text-section-label text-ink">Excel 欄位對應</h2>
            <div className="flex items-center gap-3 text-meta text-ink-muted">
              <span>
                {excelState.fileName} · {excelState.worksheetName}
              </span>
              <Button variant="ghost" size="sm" onClick={clearExcel}>
                清除 Excel
              </Button>
            </div>
          </div>
          <div className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-2 lg:grid-cols-3">
            {MAPPING_TARGETS.map((target) => {
              const columnIndex = excelState.mapping[target.key]
              return (
                <FormField
                  key={target.key}
                  label={target.label}
                  htmlFor={`mapping-${target.key}`}
                  hint={target.key === 'deptId' ? '可用部門名稱或 UUID；未對應時依文件編號推定。' : columnIndex === null ? '請手動指定對應欄位' : '已對應'}
                >
                  <Select
                    id={`mapping-${target.key}`}
                    value={columnIndex === null ? '' : String(columnIndex)}
                    onChange={(event) => handleMappingChange(target.key, event.target.value)}
                  >
                    <option value="">— 未對應 —</option>
                    {excelState.sourceHeaders.map((header, index) => (
                      <option key={index} value={index}>
                        {index + 1}. {header}
                      </option>
                    ))}
                  </Select>
                </FormField>
              )
            })}
          </div>
        </section>
      )}

      <section className="border border-line-strong bg-surface">
        <div className="flex h-row flex-wrap items-center justify-between gap-2 border-b border-line px-4">
          <div className="flex items-center gap-2">
            <h2 className="text-section-label text-ink">文件資料</h2>
            <div className="flex items-center gap-3 text-meta text-ink-muted">
              <span>
                共 <strong className="tabular text-ink">{rows.length}</strong> 筆
              </span>
              <span>
                手動 <strong className="tabular text-ink">{manualCount}</strong> 筆
              </span>
              <span>
                Excel <strong className="tabular text-ink">{excelCount}</strong> 筆
              </span>
            </div>
          </div>
          {!submitted && (
            <div className="flex items-center gap-2">
              <Button variant="secondary" size="sm" onClick={addManualRow}>
                ＋ 新增一筆
              </Button>
              <Button
                variant="secondary"
                size="sm"
                loading={parsing}
                loadingText="解析中"
                onClick={() => fileInputRef.current?.click()}
              >
                匯入 Excel
              </Button>
              {rows.length > 0 && (
                <Button variant="ghost" size="sm" onClick={resetAll}>
                  清除全部
                </Button>
              )}
            </div>
          )}
        </div>
        <Table
          className="border-0"
          columns={columns}
          data={rows}
          getRowKey={(row) => row.id}
          caption="批次匯入文件預覽"
          emptyMessage="按「＋新增一筆」直接輸入，或匯入 Excel 檔案。"
        />
        <div className="flex items-center justify-end gap-2 border-t border-line px-4 py-3">
          {submitted ? (
            <>
              <Button variant="secondary" onClick={resetAll}>
                再匯入一批
              </Button>
              <Button onClick={() => navigate('/admin/documents')}>回到文件列表</Button>
            </>
          ) : (
            <Button
              loading={bulkImport.isPending}
              loadingText="送出中"
              disabled={companyId === undefined || rows.length === 0 || depts.isPending || depts.isError}
              onClick={handleSubmit}
            >
              送出並建立文件
            </Button>
          )}
        </div>
      </section>
    </section>
  )
}

export default AdminDocumentImportPage
