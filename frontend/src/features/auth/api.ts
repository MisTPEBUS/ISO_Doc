import { httpClient } from '@/api/httpClient'

import type {
  ChangePasswordRequest,
  LoginRequest,
  LoginResponse,
  MeResponse,
} from './types'

export function login(request: LoginRequest): Promise<LoginResponse> {
  return httpClient.post<LoginResponse, LoginRequest>('/auth/login', request)
}

export function getMe(): Promise<MeResponse> {
  return httpClient.get<MeResponse>('/auth/me')
}

export function logout(): Promise<void> {
  return httpClient.post<void>('/auth/logout')
}

export function changePassword(request: ChangePasswordRequest): Promise<void> {
  return httpClient.post<void, ChangePasswordRequest>(
    '/auth/change-password',
    request,
  )
}
