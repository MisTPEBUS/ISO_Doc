import { downloadFile, type FileDownloadResult } from '@/api/fileDownload'
import { httpClient } from '@/api/httpClient'
import type { PagedResult } from '@/types/pagination'

import type {
  AvailableDocumentResponse,
  ListAvailableDocumentsParams,
} from './types'

export function listAvailable(
  params: ListAvailableDocumentsParams,
): Promise<PagedResult<AvailableDocumentResponse>> {
  return httpClient.get<PagedResult<AvailableDocumentResponse>>(
    '/documents/available',
    { params },
  )
}

export function downloadDocument(
  documentId: string,
  versionId: string,
  fallbackFileName: string,
): Promise<FileDownloadResult> {
  return downloadFile(
    `/documents/${documentId}/versions/${versionId}/download`,
    fallbackFileName,
  )
}

export function downloadAttachment(
  attachmentId: string,
  versionId: string,
  fallbackFileName: string,
): Promise<FileDownloadResult> {
  return downloadFile(
    `/attachments/${attachmentId}/versions/${versionId}/download`,
    fallbackFileName,
  )
}
