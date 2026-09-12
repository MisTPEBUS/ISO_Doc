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
