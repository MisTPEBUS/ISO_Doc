import { httpClient } from '@/api/httpClient'
import type { PagedResult } from '@/types/pagination'

import type {
  BatchCreateUsersRequest,
  BatchCreateUsersResponse,
  CreateUserRequest,
  ListUsersParams,
  ResetPasswordResponse,
  UpdateUserRequest,
  UserResponse,
} from './types'

export function list(params: ListUsersParams): Promise<PagedResult<UserResponse>> {
  return httpClient.get<PagedResult<UserResponse>>('/users', { params })
}

export function get(id: string): Promise<UserResponse> {
  return httpClient.get<UserResponse>(`/users/${id}`)
}

export function create(request: CreateUserRequest): Promise<UserResponse> {
  return httpClient.post<UserResponse, CreateUserRequest>('/users', request)
}

export function batchCreate(
  request: BatchCreateUsersRequest,
): Promise<BatchCreateUsersResponse> {
  return httpClient.post<BatchCreateUsersResponse, BatchCreateUsersRequest>(
    '/users/batch',
    request,
  )
}

export function update(id: string, request: UpdateUserRequest): Promise<UserResponse> {
  return httpClient.put<UserResponse, UpdateUserRequest>(`/users/${id}`, request)
}

export function remove(id: string): Promise<void> {
  return httpClient.delete<void>(`/users/${id}`)
}

export function resetPassword(id: string): Promise<ResetPasswordResponse> {
  return httpClient.post<ResetPasswordResponse>(`/users/${id}/reset-password`)
}
