import { useQuery } from '@tanstack/react-query'

import * as companiesApi from './api'
import type { ListCompaniesParams } from './types'

export const companyKeys = {
  all: ['companies'] as const,
  list: (params: ListCompaniesParams) => ['companies', 'list', params] as const,
}

export function useCompanies(params: ListCompaniesParams, enabled = true) {
  return useQuery({
    queryKey: companyKeys.list(params),
    queryFn: () => companiesApi.list(params),
    enabled,
  })
}
