import { httpClient } from '@/api/httpClient'

import type {
  DocumentDeptPermissionsResponse,
  PermissionMatrixParams,
  PermissionMatrixResponse,
  UpdateDocumentDeptPermissionsRequest,
  UpdateDocumentPermissionMatrixRequest,
  UpdateDocumentPermissionMatrixResponse,
} from './types'

function permissionPath(documentId: string): string {
  return `/documents/${documentId}/dept-permissions`
}

export function getPermissionMatrix(
  params: PermissionMatrixParams,
): Promise<PermissionMatrixResponse> {
  return httpClient.get<PermissionMatrixResponse>('/documents/permission-matrix', {
    params,
  })
}

export function update(
  documentId: string,
  request: UpdateDocumentDeptPermissionsRequest,
): Promise<DocumentDeptPermissionsResponse> {
  return httpClient.put<
    DocumentDeptPermissionsResponse,
    UpdateDocumentDeptPermissionsRequest
  >(permissionPath(documentId), request)
}

export function updateMatrix(
  request: UpdateDocumentPermissionMatrixRequest,
): Promise<UpdateDocumentPermissionMatrixResponse> {
  return httpClient.put<
    UpdateDocumentPermissionMatrixResponse,
    UpdateDocumentPermissionMatrixRequest
  >('/documents/permission-matrix', request)
}
