import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { Button, PageLoading } from '@/components/common'
import { useCurrentUser } from '@/features/auth/queries'
import type { UserRole } from '@/features/auth/types'

interface RoleGuardProps {
  allow: ReadonlyArray<UserRole>
  children: ReactNode
}

export function RoleGuard({ allow, children }: RoleGuardProps) {
  const location = useLocation()
  const currentUser = useCurrentUser()

  if (currentUser.isPending) {
    return <PageLoading />
  }

  if (currentUser.isError) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-canvas px-4">
        <section className="w-full max-w-md border border-line-strong bg-surface p-6 text-center">
          <h1 className="text-page-title text-ink">目前無法確認管理權限</h1>
          <p className="mt-2 text-meta text-ink-muted">請確認後端服務已啟動，再重新嘗試。</p>
          <Button className="mt-5" onClick={() => void currentUser.refetch()}>重新嘗試</Button>
        </section>
      </main>
    )
  }

  if (currentUser.data === null) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (!allow.includes(currentUser.data.role)) {
    return <Navigate to="/" replace />
  }

  return children
}
