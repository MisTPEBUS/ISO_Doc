export interface DocumentDeptPermissionsResponse {
  deptIds: string[]
}

export interface UpdateDocumentDeptPermissionsRequest {
  deptIds: string[]
}

export interface PermissionMatrixDepartment {
  id: string
  name: string
  seq: number | null
}

export interface PermissionMatrixFileStatus {
  code: string
  label: string
  hasError: boolean
}

export interface PermissionMatrixEffectiveStatus {
  code: string
  label: string
}

export interface PermissionMatrixDocumentStatus {
  mainDocument: PermissionMatrixFileStatus
  attachment: PermissionMatrixFileStatus
  effective: PermissionMatrixEffectiveStatus
}

export interface PermissionMatrixItem {
  documentId: string
  documentCode: string
  documentName: string
  version: string | null
  companyId: string
  status: PermissionMatrixDocumentStatus
  departmentIds: string[]
}

export interface PermissionMatrixPagination {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface PermissionMatrixResponse {
  departments: PermissionMatrixDepartment[]
  items: PermissionMatrixItem[]
  pagination: PermissionMatrixPagination
}

export interface PermissionMatrixScopeParams {
  companyCode: string
}

export interface PermissionMatrixParams {
  companyCode: string
  page: number
  pageSize: number
}

export interface AllPermissionMatrixItems {
  departments: PermissionMatrixDepartment[]
  items: PermissionMatrixItem[]
}

export interface UpdateDocumentPermissionMatrixItemRequest {
  documentId: string
  departmentIds: string[]
}

export interface UpdateDocumentPermissionMatrixRequest {
  items: UpdateDocumentPermissionMatrixItemRequest[]
}

export interface DocumentPermissionMatrixUpdateResult {
  documentId: string
  documentCode: string
  departmentIds: string[]
  addedDepartmentIds: string[]
  removedDepartmentIds: string[]
}

export interface PermissionMatrixUpdatedBy {
  id: string
  name: string
}

export interface UpdateDocumentPermissionMatrixResponse {
  items: DocumentPermissionMatrixUpdateResult[]
  updatedBy: PermissionMatrixUpdatedBy
  updatedAt: string
}
