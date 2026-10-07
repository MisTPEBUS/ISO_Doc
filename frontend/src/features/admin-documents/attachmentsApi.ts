import { httpClient } from '@/api/httpClient'

import type {
  Attachment,
  AttachmentDetail,
  CreateAttachmentRequest,
  CreateAttachmentVersionResponse,
  UpdateAttachmentRequest,
} from './types'

export function listAttachments(documentId: string): Promise<Attachment[]> {
  return httpClient.get<Attachment[]>(`/documents/${documentId}/attachments`)
}

export function getAttachment(
  documentId: string,
  attachmentId: string,
): Promise<AttachmentDetail> {
  return httpClient.get<AttachmentDetail>(
    `/documents/${documentId}/attachments/${attachmentId}`,
  )
}

export function createAttachment(
  documentId: string,
  request: CreateAttachmentRequest,
): Promise<Attachment> {
  return httpClient.post<Attachment, CreateAttachmentRequest>(
    `/documents/${documentId}/attachments`,
    request,
  )
}

export function updateAttachment(
  documentId: string,
  attachmentId: string,
  request: UpdateAttachmentRequest,
): Promise<Attachment> {
  return httpClient.put<Attachment, UpdateAttachmentRequest>(
    `/documents/${documentId}/attachments/${attachmentId}`,
    request,
  )
}

export function deleteAttachment(
  documentId: string,
  attachmentId: string,
): Promise<void> {
  return httpClient.delete<void>(
    `/documents/${documentId}/attachments/${attachmentId}`,
  )
}

export function createAttachmentVersion(
  attachmentId: string,
  formData: FormData,
): Promise<CreateAttachmentVersionResponse> {
  return httpClient.post<CreateAttachmentVersionResponse, FormData>(
    `/attachments/${attachmentId}/versions`,
    formData,
  )
}
