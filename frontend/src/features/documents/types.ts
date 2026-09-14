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
  attachmentNo: string
  name: string
  currentVersion: AvailableAttachmentVersion | null
}

export interface AvailableDocumentResponse {
  documentId: string
  documentNo: string
  name: string
  companyName: string
  currentVersion: AvailableDocumentVersion
  attachments: AvailableDocumentAttachment[]
}

export interface ListAvailableDocumentsParams {
  page: number
  pageSize: number
  keyword?: string
}
