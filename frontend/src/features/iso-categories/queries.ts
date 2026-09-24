import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import * as isoCategoriesApi from './api'
import type { CreateIsoCategoryRequest, ListIsoCategoriesParams, UpdateIsoCategoryRequest } from './types'

export const isoCategoryKeys = {
  all: ['iso-categories'] as const,
  lists: () => ['iso-categories', 'list'] as const,
  list: (params: ListIsoCategoriesParams) => ['iso-categories', 'list', params] as const,
  details: () => ['iso-categories', 'detail'] as const,
  detail: (id: string) => ['iso-categories', 'detail', id] as const,
}

export function useIsoCategories(params: ListIsoCategoriesParams, enabled = true) {
  return useQuery({
    queryKey: isoCategoryKeys.list(params),
    queryFn: () => isoCategoriesApi.list(params),
    enabled,
  })
}

/** The import selector must not silently omit categories beyond the first page. */
export function useCompanyIsoCategoryOptions(companyId: string | undefined) {
  return useQuery({
    queryKey: [...isoCategoryKeys.lists(), 'options', companyId],
    enabled: companyId !== undefined,
    queryFn: async () => {
      const first = await isoCategoriesApi.list({ companyId, includeInactive: true, page: 1, pageSize: 100 })
      const items = [...first.items]
      const totalPages = Math.ceil(first.totalCount / first.pageSize)
      for (let page = 2; page <= totalPages; page += 1) {
        const result = await isoCategoriesApi.list({
          companyId, includeInactive: true, page, pageSize: first.pageSize,
        })
        items.push(...result.items)
      }
      return items
    },
  })
}

export function useIsoCategory(id: string | undefined) {
  return useQuery({
    queryKey: isoCategoryKeys.detail(id ?? ''),
    queryFn: () => isoCategoriesApi.get(id ?? ''),
    enabled: id !== undefined,
  })
}

export function useCreateIsoCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: isoCategoriesApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: isoCategoryKeys.lists() })
    },
  })
}

interface UpdateIsoCategoryVariables {
  id: string
  request: UpdateIsoCategoryRequest
}

export function useUpdateIsoCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: UpdateIsoCategoryVariables) => isoCategoriesApi.update(id, request),
    onSuccess: async (category) => {
      queryClient.setQueryData(isoCategoryKeys.detail(category.id), category)
      await queryClient.invalidateQueries({ queryKey: isoCategoryKeys.lists() })
    },
  })
}

export function useDeleteIsoCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: isoCategoriesApi.remove,
    onSuccess: async (_data, id) => {
      queryClient.removeQueries({ queryKey: isoCategoryKeys.detail(id) })
      await queryClient.invalidateQueries({ queryKey: isoCategoryKeys.lists() })
    },
  })
}

export type { CreateIsoCategoryRequest }
