/**
 * SheetJS (window.XLSX) 由 index.html 的 CDN <script> 載入，用來解析批次匯入的
 * Excel 檔案（.xlsx / .xls）。型別宣告見 src/types/xlsx-global.d.ts。
 */

export const MAX_BULK_IMPORT_ROWS = 200

export type MappingTargetKey =
  | 'documentNo'
  | 'name'
  | 'pageCount'
  | 'effectiveDate'
  | 'version'

export interface MappingTarget {
  key: MappingTargetKey
  label: string
  aliases: string[]
}

export const MAPPING_TARGETS: readonly MappingTarget[] = [
  {
    key: 'documentNo',
    label: '文件編號',
    aliases: ['文件編號', '文件代號', '文號', '編號', 'documentno', 'code'],
  },
  {
    key: 'name',
    label: '文件名稱',
    aliases: ['文件名稱', '文件名', '名稱', 'documentname', 'title', 'name'],
  },
  {
    key: 'pageCount',
    label: '頁數',
    aliases: ['頁數', '頁碼', 'pages', 'pagecount'],
  },
  {
    key: 'effectiveDate',
    label: '生效日期',
    aliases: ['生效日期', '生效日', 'effectivedate'],
  },
  {
    key: 'version',
    label: '版本',
    aliases: ['版本', '版次', '版本號', 'version', 'revision'],
  },
]

export type ColumnMapping = Record<MappingTargetKey, number | null>

export interface ParsedWorkbook {
  worksheetName: string
  headerRowIndex: number
  sourceHeaders: string[]
  sourceRows: string[][]
  mapping: ColumnMapping
}

function cellText(value: unknown): string {
  if (value === null || value === undefined) return ''
  return String(value).trim()
}

function normalize(value: string): string {
  return value
    .replace(/\s+/g, '')
    .replace(/[：:()（）【】[\]\-_]/g, '')
    .toLowerCase()
}

function scoreHeaderRow(row: string[]): number {
  const cells = row.map(normalize)
  let score = 0

  for (const target of MAPPING_TARGETS) {
    const hit = cells.some((cell) =>
      target.aliases.some((alias) => {
        const normalizedAlias = normalize(alias)
        return cell === normalizedAlias || cell.includes(normalizedAlias) || normalizedAlias.includes(cell)
      }),
    )
    if (hit) score += 1
  }

  return score
}

function detectHeaderRowIndex(rows: string[][]): number {
  let bestIndex = -1
  let bestScore = 0
  const limit = Math.min(rows.length, 30)

  for (let index = 0; index < limit; index += 1) {
    const score = scoreHeaderRow(rows[index] ?? [])
    if (score > bestScore) {
      bestIndex = index
      bestScore = score
    }
  }

  return bestScore >= 2 ? bestIndex : -1
}

export function autoMapColumns(headers: string[]): ColumnMapping {
  const mapping = {} as ColumnMapping

  for (const target of MAPPING_TARGETS) {
    const aliases = target.aliases.map(normalize)
    let index = headers.findIndex((header) => aliases.includes(normalize(header)))

    if (index === -1) {
      index = headers.findIndex((header) => {
        const normalizedHeader = normalize(header)
        return aliases.some((alias) => normalizedHeader.includes(alias) || alias.includes(normalizedHeader))
      })
    }

    mapping[target.key] = index >= 0 ? index : null
  }

  return mapping
}

export class ExcelParseError extends Error {}

export function isSupportedExcelFileName(fileName: string): boolean {
  return /\.(xlsx|xls)$/i.test(fileName)
}

/** 解析上傳的 Excel 檔案（.xlsx / .xls），僅讀取第一個工作表，並自動尋找表頭列（最多掃描前 30 列）。 */
export async function parseDocumentsWorkbook(file: File): Promise<ParsedWorkbook> {
  if (!isSupportedExcelFileName(file.name)) {
    throw new ExcelParseError('只接受 .xlsx 或 .xls 檔案。')
  }

  const xlsx = window.XLSX
  if (!xlsx) {
    throw new ExcelParseError('Excel 解析套件載入失敗，請重新整理頁面後再試一次。')
  }

  const buffer = await file.arrayBuffer()
  const workbook = xlsx.read(buffer, { type: 'array', cellDates: false, cellText: true })

  const sheetName = workbook.SheetNames[0]
  if (!sheetName) {
    throw new ExcelParseError('Excel 內沒有工作表。')
  }

  const worksheet = workbook.Sheets[sheetName]
  const rawRows = xlsx.utils.sheet_to_json(worksheet, {
    header: 1,
    defval: '',
    raw: false,
    blankrows: false,
  })
  const rows: string[][] = rawRows.map((row) => (Array.isArray(row) ? row.map(cellText) : []))

  const headerRowIndex = detectHeaderRowIndex(rows)
  if (headerRowIndex < 0) {
    throw new ExcelParseError('找不到可辨識的表頭列，請確認欄位名稱或改用手動輸入。')
  }

  const sourceHeaders = (rows[headerRowIndex] ?? []).map(
    (cell, index) => cell.trim() || `未命名欄位 ${index + 1}`,
  )
  const sourceRows = rows
    .slice(headerRowIndex + 1)
    .filter((row) => row.some((cell) => cell.trim() !== ''))

  return {
    worksheetName: sheetName,
    headerRowIndex,
    sourceHeaders,
    sourceRows,
    mapping: autoMapColumns(sourceHeaders),
  }
}

export interface DraftDocumentRow {
  documentNo: string
  name: string
  pageCount: string
  effectiveDate: string
  version: string
}

export function blankDraftRow(): DraftDocumentRow {
  return { documentNo: '', name: '', pageCount: '', effectiveDate: '', version: '' }
}

export function buildRowsFromMapping(
  sourceRows: readonly string[][],
  mapping: ColumnMapping,
): DraftDocumentRow[] {
  return sourceRows
    .map((sourceRow) => {
      const row = blankDraftRow()
      for (const target of MAPPING_TARGETS) {
        const columnIndex = mapping[target.key]
        if (columnIndex !== null) {
          row[target.key] = (sourceRow[columnIndex] ?? '').trim()
        }
      }
      return row
    })
    .filter((row) => Object.values(row).some((value) => value !== ''))
}
