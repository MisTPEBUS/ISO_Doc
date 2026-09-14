import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'

import * as documentsApi from './api'
import type { ListAvailableDocumentsParams } from './types'

export const availableDocumentKeys = {
  all: ['documents', 'available'] as const,
  list: (params: ListAvailableDocumentsParams) =>
    ['documents', 'available', params] as const,
}

export function useAvailableDocuments(params: ListAvailableDocumentsParams) {
  return useQuery({
    queryKey: availableDocumentKeys.list(params),
    queryFn: () => documentsApi.listAvailable(params),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

export interface DownloadDocumentVariables {
  documentId: string
  versionId: string
  fallbackFileName: string
}

export function useDownloadDocument() {
  return useMutation({
    mutationFn: (variables: DownloadDocumentVariables) =>
      documentsApi.downloadDocument(
        variables.documentId,
        variables.versionId,
        variables.fallbackFileName,
      ),
  })
}

export interface DownloadAttachmentVariables {
  attachmentId: string
  versionId: string
  fallbackFileName: string
}

export function useDownloadAttachment() {
  return useMutation({
    mutationFn: (variables: DownloadAttachmentVariables) =>
      documentsApi.downloadAttachment(
        variables.attachmentId,
        variables.versionId,
        variables.fallbackFileName,
      ),
  })
}
