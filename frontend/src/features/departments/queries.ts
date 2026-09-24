import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import * as departmentsApi from './api'
import type { CreateDeptRequest, ListDeptsParams, UpdateDeptRequest } from './types'

export const deptKeys = {
  all: ['departments'] as const,
  lists: () => ['departments', 'list'] as const,
  list: (params: ListDeptsParams) => ['departments', 'list', params] as const,
  details: () => ['departments', 'detail'] as const,
  detail: (id: string) => ['departments', 'detail', id] as const,
}

export function useDepts(params: ListDeptsParams, enabled = true) {
  return useQuery({
    queryKey: deptKeys.list(params),
    queryFn: () => departmentsApi.list(params),
    enabled,
  })
}

/** Select options must include every company department, not only the first page. */
export function useCompanyDeptOptions(companyId: string | undefined) {
  return useQuery({
    queryKey: [...deptKeys.lists(), 'options', companyId],
    enabled: companyId !== undefined,
    queryFn: async () => {
      const first = await departmentsApi.list({ companyId, page: 1, pageSize: 100 })
      const items = [...first.items]
      const totalPages = Math.ceil(first.totalCount / first.pageSize)
      for (let page = 2; page <= totalPages; page += 1) {
        const result = await departmentsApi.list({ companyId, page, pageSize: first.pageSize })
        items.push(...result.items)
      }
      return items
    },
  })
}

export function useDept(id: string | undefined) {
  return useQuery({
    queryKey: deptKeys.detail(id ?? ''),
    queryFn: () => departmentsApi.get(id ?? ''),
    enabled: id !== undefined,
  })
}

export function useCreateDept() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: departmentsApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: deptKeys.lists() })
    },
  })
}

interface UpdateDeptVariables {
  id: string
  request: UpdateDeptRequest
}

export function useUpdateDept() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: UpdateDeptVariables) => departmentsApi.update(id, request),
    onSuccess: async (department) => {
      queryClient.setQueryData(deptKeys.detail(department.id), department)
      await queryClient.invalidateQueries({ queryKey: deptKeys.lists() })
    },
  })
}

export function useDeleteDept() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: departmentsApi.remove,
    onSuccess: async (_data, id) => {
      queryClient.removeQueries({ queryKey: deptKeys.detail(id) })
      await queryClient.invalidateQueries({ queryKey: deptKeys.lists() })
    },
  })
}

export type { CreateDeptRequest }
