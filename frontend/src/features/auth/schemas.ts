import { z } from "zod";

export const loginSchema = z.object({
  empno: z.string().trim().min(1, "請輸入帳號"),
  password: z.string().min(1, "請輸入密碼"),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

// 檢查比照後端（backend-api.md）：三欄必填、新密碼不可全為空白、
// 兩次新密碼需一致、新密碼不可等於目前密碼。後端未限制長度／字元類別，前端也不加嚴。
export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "請輸入目前密碼"),
    newPassword: z
      .string()
      .refine((value) => value.trim().length > 0, "請輸入新密碼"),
    newPasswordConfirmation: z.string().min(1, "請再次輸入新密碼"),
  })
  .refine((values) => values.newPassword === values.newPasswordConfirmation, {
    path: ["newPasswordConfirmation"],
    message: "兩次輸入的新密碼不一致",
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    path: ["newPassword"],
    message: "新密碼不可與目前密碼相同",
  });

export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;
