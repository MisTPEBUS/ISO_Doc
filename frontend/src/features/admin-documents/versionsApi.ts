import { httpClient } from '@/api/httpClient'

import type { CreateDocumentVersionResponse } from './types'

export function createVersion(
  documentId: string,
  formData: FormData,
): Promise<CreateDocumentVersionResponse> {
  return httpClient.post<CreateDocumentVersionResponse, FormData>(
    `/documents/${documentId}/versions`,
    formData,
  )
}

export function uploadDraftFile(
  documentId: string,
  versionId: string,
  formData: FormData,
): Promise<CreateDocumentVersionResponse> {
  return httpClient.put<CreateDocumentVersionResponse, FormData>(
    `/documents/${documentId}/versions/${versionId}/file`,
    formData,
  )
}
