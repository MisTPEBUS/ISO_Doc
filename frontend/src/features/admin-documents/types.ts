import type { SortParams } from '@/types/sort'

export const DOCUMENT_VERSION_STATUS = {
  Draft: 'DRAFT',
  Published: 'PUBLISHED',
  Obsolete: 'OBSOLETE',
} as const

export type DocumentVersionStatus =
  (typeof DOCUMENT_VERSION_STATUS)[keyof typeof DOCUMENT_VERSION_STATUS]

export interface AdminDocument {
  id: string
  companyId: string
  documentNo: string
  name: string
  isActive: boolean
  isoCategoryId: string | null
  deptId: string | null
  deptName: string | null
  createdBy: string
  createdAt: string
  updatedAt: string
}

export interface DocumentVersionSummary {
  versionId: string
  version: string
  status: DocumentVersionStatus
  publishDate: string | null
  effectiveDate: string | null
  expiredDate: string | null
  pageCount: number | null
  hasFile: boolean
}

export interface DocumentDetail extends AdminDocument {
  currentVersion: DocumentVersionSummary | null
  versions: DocumentVersionSummary[]
  attachments: DocumentAttachmentSummary[]
}

export interface CreateDocumentVersionResponse {
  versionId: string
  version: string
  status: DocumentVersionStatus
}

export interface CreateDocumentWithVersionResponse {
  document: AdminDocument
  version: CreateDocumentVersionResponse
}

export const ADMIN_DOCUMENT_SORT_FIELD = {
  IsActive: 'isActive',
  DocumentNo: 'documentNo',
  Name: 'name',
  IsoCategoryName: 'isoCategoryName',
  DeptName: 'deptName',
  UpdatedAt: 'updatedAt',
} as const

export type AdminDocumentSortField =
  (typeof ADMIN_DOCUMENT_SORT_FIELD)[keyof typeof ADMIN_DOCUMENT_SORT_FIELD]

export interface ListAdminDocumentsParams extends SortParams<AdminDocumentSortField> {
  companyId?: string
  keyword?: string
  page?: number
  pageSize?: number
}

export interface CreateAdminDocumentRequest {
  companyId: string
  documentNo: string
  name: string
  isoCategoryId?: string | null
  deptId?: string | null
}

export interface UpdateAdminDocumentRequest {
  name: string
  isoCategoryId?: string | null
  deptId?: string | null
}

export interface BulkImportDocumentItem {
  isoCategoryId?: string | null
  deptId?: string | null
  documentNo: string
  name: string
  pageCount?: number | null
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
  isoCategoryId: string | null
  pageCount: number | null
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

export interface Attachment {
  attachmentId: string
  attachmentNo: string
  name: string
  isActive: boolean
}

export interface AttachmentVersionSummary {
  versionId: string
  version: string
  status: DocumentVersionStatus
  publishDate: string | null
  effectiveDate: string | null
  expiredDate: string | null
  hasFile: boolean
}

export interface DocumentAttachmentSummary extends Attachment {
  currentVersion: AttachmentVersionSummary | null
}

export interface AttachmentDetail extends Attachment {
  versions: AttachmentVersionSummary[]
}

export interface CreateAttachmentRequest {
  attachmentNo: string
  name: string
}

export interface UpdateAttachmentRequest {
  name: string
}

export interface CreateAttachmentVersionResponse {
  versionId: string
  version: string
  status: DocumentVersionStatus
}
