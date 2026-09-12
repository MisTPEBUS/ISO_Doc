import { z } from 'zod'

import { VERSION_CHANGE_TYPE, type VersionChangeType } from './types'

export interface VersionFormValues {
  changeType: VersionChangeType
  effectiveDate: string
  pageCount: string
  memo: string
  file: File | null
}

export function todayUtc(): string {
  return new Date().toISOString().slice(0, 10)
}

const pdfFileSchema = z.custom<File>(
  (value) => typeof File !== 'undefined' && value instanceof File,
  { message: '請選擇 PDF 檔案' },
).refine(
  (file) => file.name.toLowerCase().endsWith('.pdf'),
  '檔案格式必須是 PDF',
).refine(
  (file) => file.name.length <= 255,
  '檔名不可超過 255 個字元',
)

export const createVersionFormSchema = z.object({
  changeType: z.enum([VERSION_CHANGE_TYPE.Major, VERSION_CHANGE_TYPE.Minor], {
    error: '請選擇變更類型',
  }),
  effectiveDate: z.string()
    .min(1, '請選擇生效日期')
    .regex(/^\d{4}-\d{2}-\d{2}$/, '生效日期格式不正確')
    .refine((value) => value >= todayUtc(), '生效日期不可早於今天'),
  pageCount: z.string()
    .trim()
    .refine(
      (value) => value.length === 0 || /^\d+$/.test(value),
      '頁數必須是正整數',
    )
    .refine(
      (value) => value.length === 0 || Number(value) > 0,
      '頁數必須大於 0',
    )
    .transform((value) => value.length === 0 ? undefined : Number(value)),
  memo: z.string()
    .trim()
    .transform((value) => value.length === 0 ? undefined : value),
  file: pdfFileSchema,
})
