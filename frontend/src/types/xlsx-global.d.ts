/**
 * SheetJS (xlsx) 是透過 index.html 的 CDN <script> 標籤載入的全域變數，
 * 刻意不裝 npm 套件（見 index.html 註解），所以這裡手動宣告用到的最小 API 介面。
 */
export {}

interface XlsxSheetToJsonOptions {
  header?: 1
  defval?: unknown
  raw?: boolean
  blankrows?: boolean
}

interface XlsxWorkbook {
  SheetNames: string[]
  Sheets: Record<string, unknown>
}

interface XlsxReadOptions {
  type: 'array' | 'binary' | 'buffer'
  cellDates?: boolean
  cellText?: boolean
}

interface XlsxStatic {
  read(data: ArrayBuffer, opts: XlsxReadOptions): XlsxWorkbook
  utils: {
    sheet_to_json(worksheet: unknown, opts: XlsxSheetToJsonOptions): unknown[][]
  }
}

declare global {
  interface Window {
    XLSX?: XlsxStatic
  }
}
