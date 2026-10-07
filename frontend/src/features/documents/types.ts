import type { SortParams } from '@/types/sort'

export interface AvailableDocumentVersion {
  versionId: string
  version: string
  effectiveDate: string | null
  pageCount: number | null
  hasFile: boolean
}

export interface AvailableAttachmentVersion {
  versionId: string
  version: string
  hasFile: boolean
}

export interface AvailableDocumentAttachment {
  attachmentId: string
  attachmentNo: string | null
  name: string
  currentVersion: AvailableAttachmentVersion | null
}

export interface AvailableDocumentResponse {
  documentId: string
  documentNo: string
  name: string
  companyName: string
  deptName: string | null
  isoCategoryId: string | null
  isoCategoryName: string | null
  currentVersion: AvailableDocumentVersion
  attachments: AvailableDocumentAttachment[]
}

export const AVAILABLE_DOCUMENT_SORT_FIELD = {
  DocumentNo: 'documentNo',
  Name: 'name',
  IsoCategoryName: 'isoCategoryName',
  DeptName: 'deptName',
  CompanyName: 'companyName',
  Version: 'version',
  EffectiveDate: 'effectiveDate',
} as const

export type AvailableDocumentSortField =
  (typeof AVAILABLE_DOCUMENT_SORT_FIELD)[keyof typeof AVAILABLE_DOCUMENT_SORT_FIELD]

export interface ListAvailableDocumentsParams extends SortParams<AvailableDocumentSortField> {
  page: number
  pageSize: number
  keyword?: string
  isoCategoryId?: string
}
