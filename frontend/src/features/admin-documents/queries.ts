import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { buildVersionFormData, type VersionFormDataInput } from '@/api/formData'

import * as adminDocumentsApi from './api'
import type { ListAdminDocumentsParams, UpdateAdminDocumentRequest } from './types'
import * as versionsApi from './versionsApi'

export const adminDocumentKeys = {
  all: ['admin-documents'] as const,
  lists: () => ['admin-documents', 'list'] as const,
  list: (params: ListAdminDocumentsParams) => ['admin-documents', 'list', params] as const,
  details: () => ['admin-documents', 'detail'] as const,
  detail: (id: string) => ['admin-documents', 'detail', id] as const,
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
