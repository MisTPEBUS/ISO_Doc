import type { SortParams } from '@/types/sort'

export interface IsoCategoryResponse {
  id: string
  companyId: string
  name: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export const ISO_CATEGORY_SORT_FIELD = {
  Name: 'name',
  IsActive: 'isActive',
  UpdatedAt: 'updatedAt',
} as const

export type IsoCategorySortField =
  (typeof ISO_CATEGORY_SORT_FIELD)[keyof typeof ISO_CATEGORY_SORT_FIELD]

export interface ListIsoCategoriesParams extends SortParams<IsoCategorySortField> {
  companyId?: string
  includeInactive?: boolean
  page?: number
  pageSize?: number
}

export interface CreateIsoCategoryRequest {
  companyId: string
  name: string
}

export interface UpdateIsoCategoryRequest {
  name: string
  isActive: boolean
}
