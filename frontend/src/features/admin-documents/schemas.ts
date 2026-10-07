import { z } from 'zod'

import { createVersionFormSchema, type VersionFormValues } from './versionSchemas'

const documentNameSchema = z.string()
  .trim()
  .min(1, '請輸入文件名稱')
  .max(255, '文件名稱不可超過 255 個字元')

export const createAdminDocumentFormSchema = z.object({
  documentNo: z.string()
    .trim()
    .min(1, '請輸入文件編號')
    .max(50, '文件編號不可超過 50 個字元')
    .regex(
      /^[A-Za-z0-9](?:[A-Za-z0-9-]{0,48}[A-Za-z0-9])?$/,
      '文件編號只能包含英文字母、數字與連字號，且開頭與結尾必須是字母或數字',
    ),
  name: documentNameSchema,
  isoCategoryId: z.string().trim(),
  deptId: z.string().trim(),
})

// 單筆新增ISO管理程序：文件欄位 + 第一個版本欄位一起驗證，規則分別沿用兩者既有 schema。
export const createAdminDocumentWithVersionFormSchema = createAdminDocumentFormSchema.extend(
  createVersionFormSchema.shape,
)

export const updateAdminDocumentFormSchema = z.object({
  name: documentNameSchema,
  isoCategoryId: z.string().trim(),
  deptId: z.string().trim(),
})

export interface AdminDocumentFormValues {
  documentNo: string
  name: string
  isoCategoryId: string
  deptId: string
}

export type AdminDocumentWithVersionFormValues = AdminDocumentFormValues & VersionFormValues
