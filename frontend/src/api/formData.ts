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
