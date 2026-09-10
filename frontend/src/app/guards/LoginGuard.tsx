import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'

import { Button, PageLoading } from '@/components/common'
import { useCurrentUser } from '@/features/auth/queries'

interface LoginGuardProps {
  children: ReactNode
}

export function LoginGuard({ children }: LoginGuardProps) {
  const location = useLocation()
  const currentUser = useCurrentUser()

  if (currentUser.isPending) {
    return <PageLoading />
  }

  if (currentUser.isError) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-canvas px-4">
        <section className="w-full max-w-md border border-line-strong bg-surface p-6 text-center">
          <h1 className="text-page-title text-ink">目前無法連線到系統</h1>
          <p className="mt-2 text-meta text-ink-muted">請確認後端服務已啟動，再重新嘗試。</p>
          <div className="mt-5 flex justify-center gap-2">
            <Button variant="secondary" onClick={() => window.location.assign(`${import.meta.env.BASE_URL}login`)}>
              前往登入
            </Button>
            <Button onClick={() => void currentUser.refetch()}>重新嘗試</Button>
          </div>
        </section>
      </main>
    )
  }

  if (currentUser.data === null) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}
