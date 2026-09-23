export interface IsoCategoryResponse {
  id: string
  companyId: string
  name: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListIsoCategoriesParams {
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
