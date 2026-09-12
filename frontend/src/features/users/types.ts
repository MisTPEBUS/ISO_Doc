export const MANAGED_USER_ROLE = {
  User: 'USER',
  CompanyAdmin: 'COMPANY_ADMIN',
  SystemAdmin: 'SYSTEM_ADMIN',
} as const

export type ManagedUserRole = (typeof MANAGED_USER_ROLE)[keyof typeof MANAGED_USER_ROLE]

export function isManagedUserRole(value: string): value is ManagedUserRole {
  return Object.values(MANAGED_USER_ROLE).some((role) => role === value)
}

export const MANAGED_USER_ROLE_LABEL: Record<ManagedUserRole, string> = {
  [MANAGED_USER_ROLE.User]: '使用者',
  [MANAGED_USER_ROLE.CompanyAdmin]: '文件管理員',
  [MANAGED_USER_ROLE.SystemAdmin]: '系統管理員',
}

export interface UserResponse {
  id: string
  companyId: string
  deptId: string
  empno: string
  name: string
  email: string | null
  role: ManagedUserRole
  isActive: boolean
  mustChangePassword: boolean
  notifyEmailEnabled: boolean
  lastLoginAt: string | null
  createdAt: string
  updatedAt: string
}

export interface ListUsersParams {
  companyId?: string
  deptId?: string
  keyword?: string
  includeInactive?: boolean
  page?: number
  pageSize?: number
}

export interface CreateUserRequest {
  empno: string
  name: string
  email: string | null
  companyId: string
  deptId: string
  role: ManagedUserRole
  password: string | null
  passwordConfirmation: string | null
}

export interface UpdateUserRequest {
  name: string
  email: string | null
  deptId: string
  role: ManagedUserRole
  isActive: boolean
  notifyEmailEnabled: boolean
}

export interface ResetPasswordResponse {
  temporaryPassword: string
}
