import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import * as usersApi from './api'
import type { ListUsersParams, UpdateUserRequest } from './types'

export const userKeys = {
  all: ['users'] as const,
  lists: () => ['users', 'list'] as const,
  list: (params: ListUsersParams) => ['users', 'list', params] as const,
  details: () => ['users', 'detail'] as const,
  detail: (id: string) => ['users', 'detail', id] as const,
}

export function useUsers(params: ListUsersParams) {
  return useQuery({
    queryKey: userKeys.list(params),
    queryFn: () => usersApi.list(params),
  })
}

export function useUser(id: string | undefined) {
  return useQuery({
    queryKey: userKeys.detail(id ?? ''),
    queryFn: () => usersApi.get(id ?? ''),
    enabled: id !== undefined,
  })
}

export function useCreateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: usersApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: userKeys.lists() })
    },
  })
}

interface UpdateUserVariables {
  id: string
  request: UpdateUserRequest
}

export function useUpdateUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, request }: UpdateUserVariables) => usersApi.update(id, request),
    onSuccess: async (user) => {
      queryClient.setQueryData(userKeys.detail(user.id), user)
      await queryClient.invalidateQueries({ queryKey: userKeys.lists() })
    },
  })
}

export function useDeleteUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: usersApi.remove,
    onSuccess: async (_data, id) => {
      queryClient.removeQueries({ queryKey: userKeys.detail(id) })
      await queryClient.invalidateQueries({ queryKey: userKeys.lists() })
    },
  })
}

export function useResetPassword() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: usersApi.resetPassword,
    onSuccess: async (_data, id) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: userKeys.lists() }),
        queryClient.invalidateQueries({ queryKey: userKeys.detail(id) }),
      ])
    },
  })
}
