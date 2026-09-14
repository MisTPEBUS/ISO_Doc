import { z } from 'zod'

import { todayUtc } from './versionSchemas'

export interface AttachmentFormValues {
  attachmentNo: string
  name: string
}

export const createAttachmentFormSchema = z.object({
  attachmentNo: z.string()
    .trim()
    .min(1, '請輸入附件編號')
    .max(50, '附件編號不可超過 50 個字元')
    .regex(
      /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,48}[A-Za-z0-9])?$/,
      '附件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字',
    ),
  name: z.string()
    .trim()
    .min(1, '請輸入附件名稱')
    .max(255, '附件名稱不可超過 255 個字元'),
})

export interface AttachmentVersionFormValues {
  changeType: 'MAJOR' | 'MINOR'
  effectiveDate: string
  file: File | null
}

export const ALLOWED_ATTACHMENT_EXTENSIONS = [
  '.jpg', '.jpeg', '.png', '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.odt', '.ods',
]

const attachmentFileSchema = z.custom<File>(
  (value) => typeof File !== 'undefined' && value instanceof File,
  { message: '請選擇附件檔案' },
).refine(
  (file) => ALLOWED_ATTACHMENT_EXTENSIONS.some((extension) =>
    file.name.toLowerCase().endsWith(extension)),
  `不允許的附件檔案類型，僅支援 ${ALLOWED_ATTACHMENT_EXTENSIONS.join('、')}`,
).refine(
  (file) => file.name.length <= 255,
  '檔名不可超過 255 個字元',
)

export const createAttachmentVersionFormSchema = z.object({
  changeType: z.enum(['MAJOR', 'MINOR']),
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
