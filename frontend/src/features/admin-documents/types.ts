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

export interface BulkImportDocumentItem {
  documentNo: string
  name: string
  pageCount: number | null
  effectiveDate: string | null
  version: string
}

export interface BulkImportDocumentsRequest {
  companyId: string
  items: BulkImportDocumentItem[]
}

export interface BulkImportedDocument {
  documentId: string
  documentVersionId: string
  documentNo: string
  name: string
  pageCount: number
  effectiveDate: string | null
  version: string
  status: string
}

export interface BulkImportDocumentSuccess {
  index: number
  document: BulkImportedDocument
}

export interface BulkImportDocumentFailure {
  index: number
  originalData: BulkImportDocumentItem
  errors: Record<string, string[]>
}

export interface BulkImportDocumentsResponse {
  total: number
  successCount: number
  failureCount: number
  succeeded: BulkImportDocumentSuccess[]
  failed: BulkImportDocumentFailure[]
}
