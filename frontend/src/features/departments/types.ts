export interface DeptResponse {
  id: string
  companyId: string
  name: string
  seq: number | null
  createdAt: string
  updatedAt: string
}

export interface ListDeptsParams {
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
