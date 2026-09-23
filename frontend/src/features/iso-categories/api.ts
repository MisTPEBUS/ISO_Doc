import { httpClient } from '@/api/httpClient'
import type { PagedResult } from '@/types/pagination'

import type {
  CreateIsoCategoryRequest,
  IsoCategoryResponse,
  ListIsoCategoriesParams,
  UpdateIsoCategoryRequest,
} from './types'

export function list(params: ListIsoCategoriesParams): Promise<PagedResult<IsoCategoryResponse>> {
  return httpClient.get<PagedResult<IsoCategoryResponse>>('/iso-categories', { params })
}

export function get(id: string): Promise<IsoCategoryResponse> {
  return httpClient.get<IsoCategoryResponse>(`/iso-categories/${id}`)
}

export function create(request: CreateIsoCategoryRequest): Promise<IsoCategoryResponse> {
  return httpClient.post<IsoCategoryResponse, CreateIsoCategoryRequest>('/iso-categories', request)
}

export function update(id: string, request: UpdateIsoCategoryRequest): Promise<IsoCategoryResponse> {
  return httpClient.put<IsoCategoryResponse, UpdateIsoCategoryRequest>(`/iso-categories/${id}`, request)
}

export function remove(id: string): Promise<void> {
  return httpClient.delete<void>(`/iso-categories/${id}`)
}
