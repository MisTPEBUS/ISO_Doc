import { Workbook, type CellValue } from 'exceljs'

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

function cellText(value: CellValue): string {
  if (value === null || value === undefined) return ''
  if (typeof value === 'string') return value.trim()
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  if (value instanceof Date) return value.toISOString().slice(0, 10)
  if ('richText' in value) return value.richText.map((part) => part.text).join('').trim()
  if ('hyperlink' in value) return value.text.trim()
  if ('formula' in value || 'sharedFormula' in value) return cellText(value.result ?? null)
  return ''
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

/**
 * 僅支援 .xlsx（Office Open XML，Excel 2007 以後另存新檔的格式）。
 * exceljs 無法讀取舊版二進位 .xls（Excel 97-2003）格式，選到 .xls 時需明確告知使用者，
 * 而不是讓 exceljs 拋出難以理解的解析錯誤。
 */
export function isSupportedExcelFileName(fileName: string): boolean {
  return /\.xlsx$/i.test(fileName)
}

export function isLegacyXlsFileName(fileName: string): boolean {
  return /\.xls$/i.test(fileName) && !/\.xlsx$/i.test(fileName)
}

/** 解析上傳的 Excel 檔案，僅讀取第一個工作表，並自動尋找表頭列（最多掃描前 30 列）。 */
export async function parseDocumentsWorkbook(file: File): Promise<ParsedWorkbook> {
  if (isLegacyXlsFileName(file.name)) {
    throw new ExcelParseError(
      '偵測到舊版 .xls 格式，此頁面僅支援 .xlsx（Excel 2007 以後的格式）。' +
        '請在 Excel 開啟後另存新檔為 .xlsx，再重新匯入。',
    )
  }
  if (!isSupportedExcelFileName(file.name)) {
    throw new ExcelParseError('只接受 .xlsx 檔案。')
  }

  const workbook = new Workbook()
  const buffer = await file.arrayBuffer()
  await workbook.xlsx.load(buffer)

  const worksheet = workbook.worksheets[0]
  if (!worksheet) {
    throw new ExcelParseError('Excel 內沒有工作表。')
  }

  const rows: string[][] = []
  worksheet.eachRow({ includeEmpty: true }, (row) => {
    const cells: string[] = []
    row.eachCell({ includeEmpty: true }, (cell, colNumber) => {
      cells[colNumber - 1] = cellText(cell.value)
    })
    rows.push(cells)
  })

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
    worksheetName: worksheet.name,
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
