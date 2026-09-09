export type RoleCode = "USER" | "DEPT_ADMIN" | "SYSTEM_ADMIN";
export type PermissionScope = "ALC" | "COMPANY" | "GLOBAL";

export interface CurrentUser {
  id: string;
  name: string;
  companyId: string;
  companyName: string;
  departmentId: string;
  departmentName: string;
  role: RoleCode;
  scope: PermissionScope;
}
