export interface AvailableDocumentVersion {
  versionId: string
  version: string
  effectiveDate: string | null
  pageCount: number | null
}

export interface AvailableDocumentResponse {
  documentId: string
  documentNo: string
  name: string
  companyName: string
  currentVersion: AvailableDocumentVersion
}
