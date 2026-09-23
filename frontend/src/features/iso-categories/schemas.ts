import { z } from 'zod'

export const isoCategoryFormSchema = z.object({
  name: z.string().trim().min(1, '請輸入分類名稱').max(100, '分類名稱不可超過 100 個字元'),
  isActive: z.boolean(),
})

export interface IsoCategoryFormValues {
  name: string
  isActive: boolean
}
