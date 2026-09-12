export const USER_ROLE = {
  User: 'USER',
  CompanyAdmin: 'COMPANY_ADMIN',
  SystemAdmin: 'SYSTEM_ADMIN',
} as const

export type UserRole = (typeof USER_ROLE)[keyof typeof USER_ROLE]

export const USER_ROLE_LABEL: Record<UserRole, string> = {
  [USER_ROLE.User]: '使用者',
  [USER_ROLE.CompanyAdmin]: '文件管理員',
  [USER_ROLE.SystemAdmin]: '系統管理員',
}

export interface LoginRequest {
  empno: string
  password: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
  newPasswordConfirmation: string
}

export interface LoginResponse {
  userId: string
  name: string
  role: UserRole
  companyId: string
  companyName: string
  deptName: string
}

export interface MeResponse {
  userId: string
  empno: string
  name: string
  role: UserRole
  companyId: string
  companyName: string
  deptId: string
  deptName: string
  mustChangePassword: boolean
}
