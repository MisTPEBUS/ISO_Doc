import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '@/api/httpClient'

import { changePassword, getMe, login, logout } from './api'

export const authKeys = {
  all: ['auth'] as const,
  me: ['auth', 'me'] as const,
}

export function useCurrentUser() {
  return useQuery({
    queryKey: authKeys.me,
    queryFn: async () => {
      try {
        return await getMe()
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          return null
        }

        throw error
      }
    },
    retry: false,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: login,
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: authKeys.me })
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(authKeys.me, null)
    },
  })
}

export function useChangePassword() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: changePassword,
    onSuccess: () => {
      // 後端已重新簽發登入 cookie 並清除 mustChangePassword，重新取得使用者狀態
      void queryClient.invalidateQueries({ queryKey: authKeys.me })
    },
  })
}
