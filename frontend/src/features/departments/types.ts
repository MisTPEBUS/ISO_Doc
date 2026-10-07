import type { SortParams } from '@/types/sort'

export interface DeptResponse {
  id: string
  companyId: string
  name: string
  seq: number | null
  createdAt: string
  updatedAt: string
}

export const DEPT_SORT_FIELD = {
  Name: 'name',
  Seq: 'seq',
  UpdatedAt: 'updatedAt',
} as const

export type DeptSortField = (typeof DEPT_SORT_FIELD)[keyof typeof DEPT_SORT_FIELD]

export interface ListDeptsParams extends SortParams<DeptSortField> {
  companyId?: string
  page?: number
  pageSize?: number
}

export interface CreateDeptRequest {
  companyId: string
  name: string
  seq?: number | null
}

export interface UpdateDeptRequest {
  name: string
  seq?: number | null
}
