import { httpClient } from '@/api/httpClient'
import type { PagedResult } from '@/types/pagination'

import type { CompanyResponse, ListCompaniesParams } from './types'

export function list(params: ListCompaniesParams): Promise<PagedResult<CompanyResponse>> {
  return httpClient.get<PagedResult<CompanyResponse>>('/companies', { params })
}
