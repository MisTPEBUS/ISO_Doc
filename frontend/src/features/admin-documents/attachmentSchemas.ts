import { z } from 'zod'

import { todayUtc } from './versionSchemas'

export interface AttachmentFormValues {
  attachmentNo: string
  name: string
}

export const createAttachmentFormSchema = z.object({
  // 表單及附件編號可留空：部分掃描進來的檔案本來就沒有編號規則，留空一律視為新增表單及附件
  // （比照後端 CreateAttachmentRequestValidator / CommitImportAttachmentItemValidator，前後端規則需一致）。
  attachmentNo: z.string()
    .trim()
    .max(100, '表單及附件編號不可超過 100 個字元')
    .refine(
      (value) => value.length === 0 || /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,98}[A-Za-z0-9])?$/.test(value),
      '表單及附件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字',
    ),
  name: z.string()
    .trim()
    .min(1, '請輸入表單及附件名稱')
    .max(255, '表單及附件名稱不可超過 255 個字元'),
})

export interface AttachmentVersionFormValues {
  version: string
  effectiveDate: string
  file: File | null
}

export const ALLOWED_ATTACHMENT_EXTENSIONS = [
  '.jpg', '.jpeg', '.png', '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.odt', '.ods',
]

const attachmentFileSchema = z.custom<File>(
  (value) => typeof File !== 'undefined' && value instanceof File,
  { message: '請選擇表單及附件檔案' },
).refine(
  (file) => ALLOWED_ATTACHMENT_EXTENSIONS.some((extension) =>
    file.name.toLowerCase().endsWith(extension)),
  `不允許的表單及附件檔案類型，僅支援 ${ALLOWED_ATTACHMENT_EXTENSIONS.join('、')}`,
).refine(
  (file) => file.name.length <= 255,
  '檔名不可超過 255 個字元',
)

export const createAttachmentVersionFormSchema = z.object({
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
    .refine(
      (value) => value.length === 0 || /^\d{4}-\d{2}-\d{2}$/.test(value),
      '生效日期格式不正確',
    )
    .refine(
      (value) => value.length === 0 || value >= todayUtc(),
      '生效日期不可早於今天',
    ),
  file: attachmentFileSchema,
})
