export const DOCUMENT_VERSION_STATUS = {
  Published: 'PUBLISHED',
  Obsolete: 'OBSOLETE',
} as const

export type DocumentVersionStatus =
  (typeof DOCUMENT_VERSION_STATUS)[keyof typeof DOCUMENT_VERSION_STATUS]

export const VERSION_CHANGE_TYPE = {
  Major: 'MAJOR',
  Minor: 'MINOR',
} as const

export type VersionChangeType =
  (typeof VERSION_CHANGE_TYPE)[keyof typeof VERSION_CHANGE_TYPE]

export interface AdminDocument {
  id: string
  companyId: string
  documentNo: string
  name: string
  isActive: boolean
  createdBy: string
  createdAt: string
  updatedAt: string
}

export interface DocumentVersionSummary {
  version: string
  status: DocumentVersionStatus
  effectiveDate: string | null
  expiredDate: string | null
}

export interface DocumentDetail extends AdminDocument {
  versions: DocumentVersionSummary[]
}

export interface CreateDocumentVersionResponse {
  versionId: string
  version: string
  status: DocumentVersionStatus
}

export interface ListAdminDocumentsParams {
  companyId?: string
  keyword?: string
  page?: number
  pageSize?: number
}

export interface CreateAdminDocumentRequest {
  companyId: string
  documentNo: string
  name: string
}

export interface UpdateAdminDocumentRequest {
  name: string
}
