import { z } from 'zod'

export const loginSchema = z.object({
  empno: z.string().trim().min(1, '請輸入員工編號'),
  password: z.string().min(1, '請輸入密碼'),
})

export type LoginFormValues = z.infer<typeof loginSchema>
