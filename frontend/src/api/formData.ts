export interface VersionFormDataInput {
  version: string
  effectiveDate: string
  pageCount?: number
  memo?: string
  file: File
}

export function buildVersionFormData(input: VersionFormDataInput): FormData {
  const formData = new FormData()

  formData.append('version', input.version)
  formData.append('effectiveDate', input.effectiveDate)

  if (input.pageCount !== undefined) {
    formData.append('pageCount', String(input.pageCount))
  }

  if (input.memo !== undefined && input.memo.length > 0) {
    formData.append('memo', input.memo)
  }

  formData.append('file', input.file, input.file.name)
  return formData
}

export interface UploadDraftVersionFileFormDataInput {
  effectiveDate: string
  file: File
}

export function buildUploadDraftVersionFileFormData(
  input: UploadDraftVersionFileFormDataInput,
): FormData {
  const formData = new FormData()

  if (input.effectiveDate.length > 0) {
    formData.append('effectiveDate', input.effectiveDate)
  }
  formData.append('file', input.file, input.file.name)
  return formData
}

export interface AttachmentVersionFormDataInput {
  version: string
  effectiveDate: string
  file: File
}

export function buildAttachmentVersionFormData(
  input: AttachmentVersionFormDataInput,
): FormData {
  const formData = new FormData()

  formData.append('version', input.version)
  if (input.effectiveDate.length > 0) {
    formData.append('effectiveDate', input.effectiveDate)
  }
  formData.append('file', input.file, input.file.name)
  return formData
}

export interface AiImportCommitAttachmentInput {
  attachmentNo: string
  name: string
  version: string
  effectiveDate?: string
  file?: File
}

export interface AiImportCommitDocumentInput {
  documentNo: string
  name: string
  version: string
  effectiveDate: string
  pageCount?: number
  mainFile?: File
  attachments: AiImportCommitAttachmentInput[]
}

export interface AiImportCommitFormDataInput {
  companyId: string
  analysisId?: string
  documents: AiImportCommitDocumentInput[]
}

export function buildAiImportCommitFormData(input: AiImportCommitFormDataInput): FormData {
  const formData = new FormData()
  formData.append('companyId', input.companyId)
  if (input.analysisId !== undefined && input.analysisId.length > 0) {
    formData.append('analysisId', input.analysisId)
  }

  input.documents.forEach((document, documentIndex) => {
    const prefix = `documents[${documentIndex}]`
    formData.append(`${prefix}.documentNo`, document.documentNo)
    formData.append(`${prefix}.name`, document.name)
    formData.append(`${prefix}.version`, document.version)
    formData.append(`${prefix}.effectiveDate`, document.effectiveDate)
    if (document.pageCount !== undefined) {
      formData.append(`${prefix}.pageCount`, String(document.pageCount))
    }
    if (document.mainFile !== undefined) {
      formData.append(`${prefix}.mainFile`, document.mainFile, document.mainFile.name)
    }

    document.attachments.forEach((attachment, attachmentIndex) => {
      const attachmentPrefix = `${prefix}.attachments[${attachmentIndex}]`
      formData.append(`${attachmentPrefix}.attachmentNo`, attachment.attachmentNo)
      formData.append(`${attachmentPrefix}.name`, attachment.name)
      formData.append(`${attachmentPrefix}.version`, attachment.version)
      if (attachment.effectiveDate !== undefined && attachment.effectiveDate.length > 0) {
        formData.append(`${attachmentPrefix}.effectiveDate`, attachment.effectiveDate)
      }
      if (attachment.file !== undefined) {
        formData.append(`${attachmentPrefix}.file`, attachment.file, attachment.file.name)
      }
    })
  })

  return formData
}
