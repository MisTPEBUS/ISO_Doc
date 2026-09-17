import { httpClient } from '@/api/httpClient'

import type {
  AllPermissionMatrixItems,
  DocumentDeptPermissionsResponse,
  PermissionMatrixParams,
  PermissionMatrixResponse,
  PermissionMatrixScopeParams,
  UpdateDocumentDeptPermissionsRequest,
  UpdateDocumentPermissionMatrixRequest,
  UpdateDocumentPermissionMatrixResponse,
} from './types'

const MAX_PERMISSION_MATRIX_PAGE_SIZE = 100

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

export async function getAllPermissionMatrixItems(
  params: PermissionMatrixScopeParams,
): Promise<AllPermissionMatrixItems> {
  const firstPage = await getPermissionMatrix({
    ...params,
    page: 1,
    pageSize: MAX_PERMISSION_MATRIX_PAGE_SIZE,
  })

  if (firstPage.pagination.totalPages <= 1) {
    return {
      departments: firstPage.departments,
      items: firstPage.items,
    }
  }

  const remainingPages = await Promise.all(
    Array.from(
      { length: firstPage.pagination.totalPages - 1 },
      (_, index) => getPermissionMatrix({
        ...params,
        page: index + 2,
        pageSize: MAX_PERMISSION_MATRIX_PAGE_SIZE,
      }),
    ),
  )

  return {
    departments: firstPage.departments,
    items: [
      ...firstPage.items,
      ...remainingPages.flatMap((response) => response.items),
    ],
  }
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
