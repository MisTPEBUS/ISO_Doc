import { Workbook, type CellValue, type Row } from 'exceljs'

import type { CreateUserRequest, ManagedUserRole } from './types'

export const MAX_BATCH_IMPORT_ROWS = 200

type RawColumnKey = 'empno' | 'name' | 'email' | 'companyName' | 'deptName' | 'password'

const HEADER_ALIASES: Record<string, RawColumnKey> = {
  empno: 'empno',
  帳號: 'empno',
  員編: 'empno',
  name: 'name',
  姓名: 'name',
  email: 'email',
  電子郵件: 'email',
  信箱: 'email',
  companyid: 'companyName',
  公司: 'companyName',
  deptid: 'deptName',
  部門: 'deptName',
  password: 'password',
  密碼: 'password',
}

export interface ParsedUserRow {
  rowNumber: number
  empno: string
  name: string
  email: string
  companyName: string
  deptName: string
  password: string
}

function cellText(value: CellValue): string {
  if (value === null || value === undefined) return ''
  if (typeof value === 'string') return value.trim()
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  if (value instanceof Date) return value.toISOString()
  if ('richText' in value) return value.richText.map((part) => part.text).join('').trim()
  if ('hyperlink' in value) return value.text.trim()
  if ('formula' in value || 'sharedFormula' in value) return cellText(value.result ?? null)
  return ''
}

function getCellText(row: Row, colNumber: number | undefined): string {
  if (colNumber === undefined) return ''
  return cellText(row.getCell(colNumber).value)
}

/** 解析上傳的 Excel 檔案；只讀取第一個工作表，第一列為標題列。 */
export async function parseUsersWorkbook(file: File): Promise<ParsedUserRow[]> {
  const workbook = new Workbook()
  const buffer = await file.arrayBuffer()
  await workbook.xlsx.load(buffer)

  const worksheet = workbook.worksheets[0]
  if (!worksheet) return []

  const columnIndexes = new Map<RawColumnKey, number>()
  worksheet.getRow(1).eachCell({ includeEmpty: false }, (cell, colNumber) => {
    const key = HEADER_ALIASES[cellText(cell.value).toLowerCase()]
    if (key && !columnIndexes.has(key)) {
      columnIndexes.set(key, colNumber)
    }
  })

  const rows: ParsedUserRow[] = []
  worksheet.eachRow((row, sheetRowNumber) => {
    if (sheetRowNumber === 1) return

    const empno = getCellText(row, columnIndexes.get('empno'))
    const name = getCellText(row, columnIndexes.get('name'))
    const email = getCellText(row, columnIndexes.get('email'))
    const companyName = getCellText(row, columnIndexes.get('companyName'))
    const deptName = getCellText(row, columnIndexes.get('deptName'))
    const password = getCellText(row, columnIndexes.get('password'))

    if (!empno && !name && !email && !companyName && !deptName && !password) return

    rows.push({
      rowNumber: rows.length + 1,
      empno,
      name,
      email,
      companyName,
      deptName,
      password,
    })
  })

  return rows
}

export interface ResolvedBatchRow {
  parsed: ParsedUserRow
  deptId: string | null
  validationError: string | null
}

/**
 * 依目前公司底下的部門清單，把 Excel 內的部門名稱解析成 deptId；
 * 找不到對應部門，或缺少必填欄位時回傳 validationError，該筆不會送出到後端。
 */
export function resolveDeptIds(
  rows: readonly ParsedUserRow[],
  departments: ReadonlyArray<{ id: string; name: string }>,
): ResolvedBatchRow[] {
  const departmentIdByName = new Map(
    departments.map((department) => [department.name.trim(), department.id]),
  )

  return rows.map((parsed) => {
    if (!parsed.empno) {
      return { parsed, deptId: null, validationError: '缺少帳號。' }
    }
    if (!parsed.name) {
      return { parsed, deptId: null, validationError: '缺少姓名。' }
    }
    if (!parsed.deptName) {
      return { parsed, deptId: null, validationError: '缺少部門。' }
    }

    const deptId = departmentIdByName.get(parsed.deptName) ?? null
    if (deptId === null) {
      return { parsed, deptId: null, validationError: `找不到部門「${parsed.deptName}」。` }
    }

    return { parsed, deptId, validationError: null }
  })
}

export function toCreateUserRequest(
  row: ParsedUserRow,
  companyId: string,
  deptId: string,
  role: ManagedUserRole,
): CreateUserRequest {
  const password = row.password.length > 0 ? row.password : null
  return {
    empno: row.empno,
    name: row.name,
    email: row.email.length > 0 ? row.email : null,
    companyId,
    deptId,
    role,
    password,
    passwordConfirmation: password,
  }
}
