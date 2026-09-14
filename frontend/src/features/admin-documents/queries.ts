import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  buildAttachmentVersionFormData,
  buildVersionFormData,
  type AttachmentVersionFormDataInput,
  type VersionFormDataInput,
} from '@/api/formData'

import * as adminDocumentsApi from './api'
import * as attachmentsApi from './attachmentsApi'
import type {
  CreateAttachmentRequest,
  ListAdminDocumentsParams,
  UpdateAdminDocumentRequest,
} from './types'
import * as versionsApi from './versionsApi'

export const adminDocumentKeys = {
  all: ['admin-documents'] as const,
  lists: () => ['admin-documents', 'list'] as const,
  list: (params: ListAdminDocumentsParams) => ['admin-documents', 'list', params] as const,
  details: () => ['admin-documents', 'detail'] as const,
  detail: (id: string) => ['admin-documents', 'detail', id] as const,
  attachments: (documentId: string) =>
    ['admin-documents', 'detail', documentId, 'attachments'] as const,
  attachmentDetail: (documentId: string, attachmentId: string) =>
    ['admin-documents', 'detail', documentId, 'attachments', attachmentId] as const,
}

export function useAdminDocuments(params: ListAdminDocumentsParams) {
  return useQuery({
    queryKey: adminDocumentKeys.list(params),
    queryFn: () => adminDocumentsApi.list(params),
  })
}

export function useAdminDocument(id: string | undefined) {
  return useQuery({
    queryKey: adminDocumentKeys.detail(id ?? ''),
    queryFn: () => adminDocumentsApi.get(id ?? ''),
    enabled: id !== undefined,
  })
}

export function useCreateAdminDocument() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminDocumentsApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: adminDocumentKeys.lists() })
    },
  })
}

interface UpdateAdminDocumentVariables {
  id: string
  request: UpdateAdminDocumentRequest
}

export function useUpdateAdminDocument() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: UpdateAdminDocumentVariables) =>
      adminDocumentsApi.update(id, request),
    onSuccess: async (document) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: adminDocumentKeys.lists() }),
        queryClient.invalidateQueries({ queryKey: adminDocumentKeys.detail(document.id) }),
      ])
    },
  })
}

export function useBulkImportAdminDocuments() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminDocumentsApi.bulkImport,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: adminDocumentKeys.lists() })
    },
  })
}

export function useDeleteAdminDocument() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminDocumentsApi.remove,
    onSuccess: async (_data, id) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: adminDocumentKeys.lists() }),
        queryClient.invalidateQueries({ queryKey: adminDocumentKeys.detail(id) }),
      ])
    },
  })
}

interface CreateVersionVariables {
  documentId: string
  input: VersionFormDataInput
}

export function useCreateVersion() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ documentId, input }: CreateVersionVariables) =>
      versionsApi.createVersion(documentId, buildVersionFormData(input)),
    onSuccess: async (_version, variables) => {
      await queryClient.invalidateQueries({
        queryKey: adminDocumentKeys.detail(variables.documentId),
      })
    },
  })
}

export function useAttachments(documentId: string | undefined) {
  return useQuery({
    queryKey: adminDocumentKeys.attachments(documentId ?? ''),
    queryFn: () => attachmentsApi.listAttachments(documentId ?? ''),
    enabled: documentId !== undefined,
  })
}

export function useAttachmentDetail(
  documentId: string | undefined,
  attachmentId: string | undefined,
) {
  return useQuery({
    queryKey: adminDocumentKeys.attachmentDetail(documentId ?? '', attachmentId ?? ''),
    queryFn: () => attachmentsApi.getAttachment(documentId ?? '', attachmentId ?? ''),
    enabled: documentId !== undefined && attachmentId !== undefined,
  })
}

interface CreateAttachmentVariables {
  documentId: string
  request: CreateAttachmentRequest
}

export function useCreateAttachment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ documentId, request }: CreateAttachmentVariables) =>
      attachmentsApi.createAttachment(documentId, request),
    onSuccess: async (_attachment, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: adminDocumentKeys.attachments(variables.documentId),
        }),
        queryClient.invalidateQueries({
          queryKey: adminDocumentKeys.detail(variables.documentId),
        }),
      ])
    },
  })
}

interface DeleteAttachmentVariables {
  documentId: string
  attachmentId: string
}

export function useDeleteAttachment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ documentId, attachmentId }: DeleteAttachmentVariables) =>
      attachmentsApi.deleteAttachment(documentId, attachmentId),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: adminDocumentKeys.attachments(variables.documentId),
        }),
        queryClient.invalidateQueries({
          queryKey: adminDocumentKeys.detail(variables.documentId),
        }),
      ])
    },
  })
}

interface CreateAttachmentVersionVariables {
  documentId: string
  attachmentId: string
  input: AttachmentVersionFormDataInput
}

export function useCreateAttachmentVersion() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ attachmentId, input }: CreateAttachmentVersionVariables) =>
      attachmentsApi.createAttachmentVersion(
        attachmentId,
        buildAttachmentVersionFormData(input),
      ),
    onSuccess: async (_version, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: adminDocumentKeys.attachmentDetail(
            variables.documentId,
            variables.attachmentId,
          ),
        }),
        queryClient.invalidateQueries({
          queryKey: adminDocumentKeys.detail(variables.documentId),
        }),
      ])
    },
  })
}
