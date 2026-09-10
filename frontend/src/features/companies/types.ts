export interface CompanyResponse {
  id: string
  code: string
  name: string
}

export interface ListCompaniesParams {
  keyword?: string
  page?: number
  pageSize?: number
}
