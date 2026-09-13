import { z } from 'zod'

export interface VersionFormValues {
  version: string
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
  version: z.string()
    .trim()
    .min(1, '請輸入版本號')
    .max(20, '版本號不可超過 20 個字元')
    .regex(/^([1-9]\d*)(?:\.(0|[1-9]\d*))?$/, '版本格式須為正整數或主版號.次版號，例如 1、1.0、2.1')
    .refine(
      (value) => value.split('.').every((part) => Number(part) <= 2_147_483_647),
      '主版號與次版號不可超過 2147483647',
    )
    .transform((value) => value.includes('.') ? value : `${value}.0`),
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
