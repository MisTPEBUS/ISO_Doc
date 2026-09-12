import { httpClient } from '@/api/httpClient'
import type { PagedResult } from '@/types/pagination'

import type {
  AdminDocument,
  CreateAdminDocumentRequest,
  DocumentDetail,
  ListAdminDocumentsParams,
  UpdateAdminDocumentRequest,
} from './types'

export function list(
  params: ListAdminDocumentsParams,
): Promise<PagedResult<AdminDocument>> {
  return httpClient.get<PagedResult<AdminDocument>>('/documents', { params })
}

export function get(id: string): Promise<DocumentDetail> {
  return httpClient.get<DocumentDetail>(`/documents/${id}`)
}

export function create(
  request: CreateAdminDocumentRequest,
): Promise<AdminDocument> {
  return httpClient.post<AdminDocument, CreateAdminDocumentRequest>('/documents', request)
}

export function update(
  id: string,
  request: UpdateAdminDocumentRequest,
): Promise<AdminDocument> {
  return httpClient.put<AdminDocument, UpdateAdminDocumentRequest>(`/documents/${id}`, request)
}

export function remove(id: string): Promise<void> {
  return httpClient.delete<void>(`/documents/${id}`)
}
