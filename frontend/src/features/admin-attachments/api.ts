import { httpClient } from '@/api/httpClient'

import type { AnalyzeImportRequest, AnalyzeImportResponse, CommitImportResponse } from './types'

export function analyze(request: AnalyzeImportRequest): Promise<AnalyzeImportResponse> {
  return httpClient.post<AnalyzeImportResponse, AnalyzeImportRequest>(
    '/documents/ai-import/analyze',
    request,
  )
}

export function commit(formData: FormData): Promise<CommitImportResponse> {
  return httpClient.post<CommitImportResponse, FormData>('/documents/ai-import/commit', formData)
}
