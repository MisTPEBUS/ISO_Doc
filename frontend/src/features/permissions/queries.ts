import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import * as permissionsApi from './api'
import type {
  PermissionMatrixParams,
  PermissionMatrixScopeParams,
  UpdateDocumentDeptPermissionsRequest,
  UpdateDocumentPermissionMatrixRequest,
} from './types'

export const permissionMatrixKeys = {
  all: ['permissions', 'matrix'] as const,
  list: (params: PermissionMatrixParams) =>
    ['permissions', 'matrix', params] as const,
  allItems: (params: PermissionMatrixScopeParams) =>
    ['permissions', 'matrix', 'all-items', params] as const,
}

export function usePermissionMatrix(
  params: PermissionMatrixParams,
  enabled = true,
) {
  return useQuery({
    queryKey: permissionMatrixKeys.list(params),
    queryFn: () => permissionsApi.getPermissionMatrix(params),
    enabled,
  })
}

export function useAllPermissionMatrixItems(
  params: PermissionMatrixScopeParams,
  enabled = true,
) {
  return useQuery({
    queryKey: permissionMatrixKeys.allItems(params),
    queryFn: () => permissionsApi.getAllPermissionMatrixItems(params),
    enabled,
  })
}

interface UpdateDeptPermissionsVariables {
  documentId: string
  request: UpdateDocumentDeptPermissionsRequest
}

export function useUpdateDeptPermissions() {
  return useMutation({
    mutationFn: ({ documentId, request }: UpdateDeptPermissionsVariables) =>
      permissionsApi.update(documentId, request),
  })
}

export function useUpdatePermissionMatrix() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: UpdateDocumentPermissionMatrixRequest) =>
      permissionsApi.updateMatrix(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: permissionMatrixKeys.all })
    },
  })
}
