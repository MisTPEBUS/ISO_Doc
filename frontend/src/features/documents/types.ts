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

export interface ListAvailableDocumentsParams {
  page: number
  pageSize: number
  keyword?: string
  isoCategoryId?: string
}
