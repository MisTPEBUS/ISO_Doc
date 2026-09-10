import { httpClient } from '@/api/httpClient'

import type { LoginRequest, LoginResponse, MeResponse } from './types'

export function login(request: LoginRequest): Promise<LoginResponse> {
  return httpClient.post<LoginResponse, LoginRequest>('/auth/login', request)
}

export function getMe(): Promise<MeResponse> {
  return httpClient.get<MeResponse>('/auth/me')
}

export function logout(): Promise<void> {
  return httpClient.post<void>('/auth/logout')
}
