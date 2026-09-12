import { z } from "zod";

import { MANAGED_USER_ROLE, type ManagedUserRole } from "./types";

const emailSchema = z
  .string()
  .trim()
  .max(255, "電子郵件不可超過 255 個字元")
  .refine(
    (value) => value.length === 0 || z.email().safeParse(value).success,
    "電子郵件格式不正確",
  );

const roleSchema = z.enum([
  MANAGED_USER_ROLE.User,
  MANAGED_USER_ROLE.CompanyAdmin,
  MANAGED_USER_ROLE.SystemAdmin,
]);

export const createUserFormSchema = z
  .object({
    empno: z
      .string()
      .trim()
      .min(1, "請輸入帳號")
      .max(30, "帳號不可超過 30 個字元"),
    name: z
      .string()
      .trim()
      .min(1, "請輸入使用者姓名")
      .max(100, "使用者姓名不可超過 100 個字元"),
    email: emailSchema,
    deptId: z.string().min(1, "請選擇部門"),
    role: roleSchema,
    password: z
      .string()
      .refine(
        (value) => value.length === 0 || value.trim().length > 0,
        "密碼不可只有空白",
      ),
    passwordConfirmation: z.string(),
  })
  .superRefine((values, context) => {
    if (values.passwordConfirmation !== values.password) {
      context.addIssue({
        code: "custom",
        path: ["passwordConfirmation"],
        message: "確認密碼與密碼不符",
      });
    }
  });

export const updateUserFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "請輸入使用者姓名")
    .max(100, "使用者姓名不可超過 100 個字元"),
  email: emailSchema,
  deptId: z.string().min(1, "請選擇部門"),
  role: roleSchema,
  isActive: z.boolean(),
  notifyEmailEnabled: z.boolean(),
});

export interface UserFormValues {
  empno: string;
  name: string;
  email: string;
  deptId: string;
  role: ManagedUserRole;
  password: string;
  passwordConfirmation: string;
  isActive: boolean;
  notifyEmailEnabled: boolean;
}
