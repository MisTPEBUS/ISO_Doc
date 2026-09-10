import { z } from 'zod'

export const deptFormSchema = z.object({
  name: z.string().trim().min(1, '請輸入部門名稱').max(100, '部門名稱不可超過 100 個字元'),
  seq: z.string().trim().refine(
    (value) => value.length === 0 || /^-?\d+$/.test(value),
    '排序必須是整數',
  ).transform((value) => value.length === 0 ? null : Number(value)),
})

export interface DeptFormValues {
  name: string
  seq: string
}
