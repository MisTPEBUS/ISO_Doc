import { httpClient } from '@/api/httpClient'
import type { PagedResult } from '@/types/pagination'

import type {
  CreateDeptRequest,
  DeptResponse,
  ListDeptsParams,
  UpdateDeptRequest,
} from './types'

export function list(params: ListDeptsParams): Promise<PagedResult<DeptResponse>> {
  return httpClient.get<PagedResult<DeptResponse>>('/depts', { params })
}

export function get(id: string): Promise<DeptResponse> {
  return httpClient.get<DeptResponse>(`/depts/${id}`)
}

export function create(request: CreateDeptRequest): Promise<DeptResponse> {
  return httpClient.post<DeptResponse, CreateDeptRequest>('/depts', request)
}

export function update(id: string, request: UpdateDeptRequest): Promise<DeptResponse> {
  return httpClient.put<DeptResponse, UpdateDeptRequest>(`/depts/${id}`, request)
}

export function remove(id: string): Promise<void> {
  return httpClient.delete<void>(`/depts/${id}`)
}
