import {
  FileSpreadsheet,
  Plus,
  Trash2,
  Upload,
  X,
} from 'lucide-react'
import {
  useRef,
  useState,
  type ChangeEvent,
  type DragEvent,
  type KeyboardEvent,
} from 'react'

import { Alert, Badge, Button, FormField, Input, Select, Table, type TableColumn } from '../common'
import {
  buildRowsFromMapping,
  ExcelParseError,
  MAPPING_TARGETS,
  parseDocumentsWorkbook,
  type ColumnMapping,
  type DraftDocumentRow,
  type MappingTargetKey,
} from '../../features/admin-documents/bulkImportExcel'

type RowSource = 'manual' | 'excel'

interface EditableDocumentRow extends DraftDocumentRow {
  id: string
  source: RowSource
}

interface ExcelPreviewState {
  fileName: string
  worksheetName: string
  headerRowIndex: number
  sourceHeaders: string[]
  sourceRows: string[][]
  mapping: ColumnMapping
}

function createRowId(): string {
  return typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `preview-row-${Date.now()}-${Math.random().toString(16).slice(2)}`
}

function createRow(source: RowSource, values: DraftDocumentRow): EditableDocumentRow {
  return { id: createRowId(), source, ...values }
}

function createBlankRow(): EditableDocumentRow {
  return createRow('manual', {
    documentNo: '',
    name: '',
    pageCount: '',
    effectiveDate: '',
    version: '',
  })
}

function createSampleRows(): EditableDocumentRow[] {
  return [
    createRow('manual', {
      documentNo: 'HR-I-01',
      name: '人力資源管理程序',
      pageCount: '12',
      effectiveDate: '2026-09-01',
      version: '1.0',
    }),
    createRow('manual', {
      documentNo: 'HR-I-02',
      name: '教育訓練管理程序',
      pageCount: '8',
      effectiveDate: '2026-09-01',
      version: '2.1',
    }),
  ]
}

export function IsoDocumentInlineEntryPreview() {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [rows, setRows] = useState<EditableDocumentRow[]>(createSampleRows)
  const [excelState, setExcelState] = useState<ExcelPreviewState>()
  const [isDragging, setIsDragging] = useState(false)
  const [isParsing, setIsParsing] = useState(false)
  const [message, setMessage] = useState('可直接編輯範例資料、增加一筆，或匯入 Excel。')
  const [error, setError] = useState<string>()

  const manualCount = rows.filter((row) => row.source === 'manual').length
  const excelCount = rows.filter((row) => row.source === 'excel').length
  const mappedCount = excelState
    ? MAPPING_TARGETS.filter((target) => excelState.mapping[target.key] !== null).length
    : 0

  function applyMapping(mapping: ColumnMapping, sourceRows: readonly string[][]) {
    const mappedRows = buildRowsFromMapping(sourceRows, mapping)
    setRows((current) => [
      ...current.filter((row) => row.source !== 'excel'),
      ...mappedRows.map((row) => createRow('excel', row)),
    ])
  }

  async function handleFile(file: File) {
    if (!/\.xlsx$/i.test(file.name)) {
      setError('目前範例只支援 .xlsx 檔案。')
      return
    }

    setError(undefined)
    setIsParsing(true)
    setMessage('正在解析 Excel…')
    try {
      const parsed = await parseDocumentsWorkbook(file)
      const nextExcelState: ExcelPreviewState = {
        fileName: file.name,
        worksheetName: parsed.worksheetName,
        headerRowIndex: parsed.headerRowIndex,
        sourceHeaders: parsed.sourceHeaders,
        sourceRows: parsed.sourceRows,
        mapping: parsed.mapping,
      }
      setExcelState(nextExcelState)
      applyMapping(parsed.mapping, parsed.sourceRows)

      const nextMappedCount = MAPPING_TARGETS.filter(
        (target) => parsed.mapping[target.key] !== null,
      ).length
      setMessage(
        `已讀取「${parsed.worksheetName}」，自動對應 ${nextMappedCount} / ${MAPPING_TARGETS.length} 個欄位。`,
      )
    } catch (parseError) {
      setError(
        parseError instanceof ExcelParseError
          ? parseError.message
          : 'Excel 解析失敗，請確認檔案內容與格式。',
      )
      setMessage('Excel 尚未匯入。')
    } finally {
      setIsParsing(false)
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
    setError(undefined)
    setMessage('Excel 資料已清除，手動輸入資料仍保留。')
  }

  function updateRow(id: string, patch: Partial<DraftDocumentRow>) {
    setRows((current) => current.map((row) => (row.id === id ? { ...row, ...patch } : row)))
  }

  const columns: ReadonlyArray<TableColumn<EditableDocumentRow>> = [
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
      headerClassName: 'min-w-40',
      render: (row) => (
        <Input
          className="font-mono"
          value={row.documentNo}
          placeholder="例如 HR-I-01"
          onChange={(event) => updateRow(row.id, { documentNo: event.target.value })}
        />
      ),
    },
    {
      key: 'name',
      header: '文件名稱',
      headerClassName: 'min-w-64',
      render: (row) => (
        <Input
          value={row.name}
          placeholder="請輸入文件名稱"
          onChange={(event) => updateRow(row.id, { name: event.target.value })}
        />
      ),
    },
    {
      key: 'pageCount',
      header: '頁數',
      headerClassName: 'w-24',
      render: (row) => (
        <Input
          className="tabular"
          type="number"
          min={0}
          value={row.pageCount}
          onChange={(event) => updateRow(row.id, { pageCount: event.target.value })}
        />
      ),
    },
    {
      key: 'effectiveDate',
      header: '生效日期',
      headerClassName: 'min-w-40',
      render: (row) => (
        <Input
          className="tabular"
          type="date"
          value={row.effectiveDate}
          onChange={(event) => updateRow(row.id, { effectiveDate: event.target.value })}
        />
      ),
    },
    {
      key: 'version',
      header: '版次',
      headerClassName: 'w-28',
      render: (row) => (
        <Input
          className="font-mono font-semibold"
          value={row.version}
          placeholder="例如 1.0"
          onChange={(event) => updateRow(row.id, { version: event.target.value })}
        />
      ),
    },
    {
      key: 'actions',
      header: '操作',
      headerClassName: 'w-24',
      render: (row) => (
        <Button
          variant="ghost"
          size="sm"
          className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
          aria-label={`刪除 ${row.documentNo || '未命名文件'}`}
          onClick={() => setRows((current) => current.filter((item) => item.id !== row.id))}
        >
          <Trash2 className="size-4" aria-hidden="true" />
          刪除
        </Button>
      ),
    },
  ]

  const jsonPreview = rows.map((row) => ({
    documentNo: row.documentNo,
    name: row.name,
    pageCount: row.pageCount,
    effectiveDate: row.effectiveDate,
    version: row.version,
  }))

  return (
    <div className="space-y-4">
      <input
        ref={fileInputRef}
        type="file"
        accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        hidden
        onChange={handleFileInputChange}
      />

      <section className="border border-line-strong bg-surface">
        <div className="flex h-row items-center border-b border-line bg-surface-header px-4">
          <div>
            <h3 className="text-section-label text-ink">Excel 匯入</h3>
            <p className="text-meta text-ink-muted">自動尋找前 30 列中的表頭</p>
          </div>
        </div>
        <div className="space-y-3 p-4">
          <div
            role="button"
            tabIndex={0}
            aria-label="拖曳或點擊選擇 Excel 檔案"
            className={`grid min-h-32 cursor-pointer place-items-center rounded-sm border-2 border-dashed px-6 py-8 text-center transition-colors ${
              isDragging
                ? 'border-primary bg-primary-subtle'
                : 'border-line-strong bg-canvas hover:border-primary hover:bg-primary-subtle'
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
              <span className="mx-auto mb-3 grid size-10 place-items-center rounded-md bg-primary-subtle text-primary">
                <FileSpreadsheet className="size-5" aria-hidden="true" />
              </span>
              <p className="font-semibold text-ink">拖曳 Excel 到這裡，或點擊選擇檔案</p>
              <p className="mt-1 text-meta text-ink-muted">支援 .xlsx，解析後可重新指定欄位對應。</p>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-meta text-ink-muted">
              {excelState
                ? `${excelState.fileName} · ${excelState.worksheetName}`
                : '尚未選擇 Excel'}
            </p>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="secondary"
                size="sm"
                loading={isParsing}
                loadingText="解析中"
                onClick={() => fileInputRef.current?.click()}
              >
                <Upload className="size-4" aria-hidden="true" />
                選擇 Excel
              </Button>
              {excelState && (
                <Button variant="ghost" size="sm" onClick={clearExcel}>
                  <X className="size-4" aria-hidden="true" />
                  清除 Excel
                </Button>
              )}
            </div>
          </div>

          {error ? (
            <Alert variant="error" title="Excel 無法解析">
              {error}
            </Alert>
          ) : (
            <Alert variant={excelState ? 'success' : 'info'}>{message}</Alert>
          )}
        </div>
      </section>

      {excelState && (
        <section className="border border-line-strong bg-surface">
          <div className="flex min-h-row flex-wrap items-center justify-between gap-2 border-b border-line bg-surface-header px-4 py-2">
            <h3 className="text-section-label text-ink">Excel 欄位對應</h3>
            <div className="flex flex-wrap gap-3 text-meta text-ink-muted">
              <span>
                表頭第 <strong className="tabular text-ink">{excelState.headerRowIndex + 1}</strong> 列
              </span>
              <span>
                已對應 <strong className="tabular text-ink">{mappedCount} / {MAPPING_TARGETS.length}</strong>
              </span>
            </div>
          </div>
          <div className="grid gap-4 p-4 sm:grid-cols-2 lg:grid-cols-3">
            {MAPPING_TARGETS.map((target) => {
              const columnIndex = excelState.mapping[target.key]
              return (
                <FormField
                  key={target.key}
                  label={target.label}
                  htmlFor={`preview-mapping-${target.key}`}
                  hint={columnIndex === null ? '請指定 Excel 欄位' : '已對應，表格資料同步更新'}
                >
                  <Select
                    id={`preview-mapping-${target.key}`}
                    value={columnIndex === null ? '' : String(columnIndex)}
                    onChange={(event) => handleMappingChange(target.key, event.target.value)}
                  >
                    <option value="">— 未對應 —</option>
                    {excelState.sourceHeaders.map((header, index) => (
                      <option key={`${index}-${header}`} value={index}>
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
        <div className="flex min-h-row flex-wrap items-center justify-between gap-3 border-b border-line bg-surface-header px-4 py-2">
          <div className="flex flex-wrap items-center gap-3">
            <h3 className="text-section-label text-ink">文件資料</h3>
            <span className="text-meta text-ink-muted">
              共 <strong className="tabular text-ink">{rows.length}</strong> 筆
            </span>
            <span className="text-meta text-ink-muted">
              手動 <strong className="tabular text-ink">{manualCount}</strong> 筆
            </span>
            <span className="text-meta text-ink-muted">
              Excel <strong className="tabular text-ink">{excelCount}</strong> 筆
            </span>
          </div>
          <Button size="sm" onClick={() => setRows((current) => [...current, createBlankRow()])}>
            <Plus className="size-4" aria-hidden="true" />
            新增一筆
          </Button>
        </div>
        <Table
          className="border-0"
          columns={columns}
          data={rows}
          getRowKey={(row) => row.id}
          caption="ISO 文件登錄資料預覽"
          emptyMessage="尚無文件資料，請新增一筆或匯入 Excel。"
        />
        <details className="border-t border-line px-4 py-3">
          <summary className="cursor-pointer text-label font-medium text-primary">
            查看送出資料 JSON
          </summary>
          <pre className="mt-3 max-h-64 overflow-auto rounded-sm bg-shell-900 p-4 text-fine text-on-shell">
            {JSON.stringify(jsonPreview, null, 2)}
          </pre>
        </details>
      </section>
    </div>
  )
}
