export interface AiImportFileDescriptor {
  originalFileName: string
  relativePath: string
  size: number
  role: string
  documentNo: string
  attachmentNo: string
  displayName: string
  extension: string
  parseStatus: string
  checksum: string
}

export interface AnalyzeImportRequest {
  companyId: string
  files: AiImportFileDescriptor[]
}

export interface ExistingDocumentState {
  documentId: string | null
  latestVersion: string | null
  latestVersionStatus: string | null
  latestVersionHasFile: boolean
}

export interface ExistingAttachmentState {
  attachmentId: string | null
  latestVersion: string | null
}

export interface AnalyzedAttachment {
  attachmentNo: string
  name: string
  effectiveDate: string | null
  suggestedVersion: string | null
  predictedAction: string
  relativePath: string
  existing: ExistingAttachmentState
}

export interface AnalyzedDocument {
  documentNo: string
  name: string
  effectiveDate: string | null
  suggestedVersion: string | null
  predictedAction: string
  confidence: string
  existing: ExistingDocumentState
  mainFile: { relativePath: string } | null
  attachments: AnalyzedAttachment[]
}

export interface UnresolvedImportFile {
  relativePath: string
  reason: string
}

export interface AnalyzeImportResponse {
  analysisId: string
  documents: AnalyzedDocument[]
  unresolved: UnresolvedImportFile[]
}

export interface CommitImportDocumentSuccess {
  index: number
  documentNo: string
  documentId: string
  action: string
  documentVersionId: string | null
  version: string | null
  effectiveDate: string | null
  status: string | null
}

export interface CommitImportDocumentFailure {
  index: number
  documentNo: string | null
  errors: Record<string, string[]>
}

export interface CommitImportAttachmentSuccess {
  index: number
  documentIndex: number
  attachmentNo: string
  attachmentId: string
  action: string
  attachmentVersionId: string | null
  version: string | null
}

export interface CommitImportAttachmentSkipped {
  index: number
  documentIndex: number
  attachmentNo: string | null
  reason: string
}

export interface CommitImportAttachmentFailure {
  index: number
  documentIndex: number
  attachmentNo: string | null
  errors: Record<string, string[]>
}

export interface CommitImportResponse {
  documents: {
    total: number
    successCount: number
    failureCount: number
    succeeded: CommitImportDocumentSuccess[]
    failed: CommitImportDocumentFailure[]
  }
  attachments: {
    total: number
    successCount: number
    skippedCount: number
    failureCount: number
    succeeded: CommitImportAttachmentSuccess[]
    skipped: CommitImportAttachmentSkipped[]
    failed: CommitImportAttachmentFailure[]
  }
}
