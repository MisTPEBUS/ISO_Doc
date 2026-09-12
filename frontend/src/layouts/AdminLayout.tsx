import { useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'

import { ApiError } from '@/api/httpClient'
import { adminNavGroups } from '@/app/router/adminNav'
import { Alert, AppHeader, SidebarNav } from '@/components/common'
import { useCurrentUser, useLogout } from '@/features/auth/queries'
import { USER_ROLE_LABEL } from '@/features/auth/types'

export function AdminLayout() {
  const location = useLocation()
  const navigate = useNavigate()
  const currentUser = useCurrentUser()
  const user = currentUser.data
  const logoutMutation = useLogout()
  const [logoutError, setLogoutError] = useState<string>()
  const activeHref = `${import.meta.env.BASE_URL.replace(/\/$/, '')}${location.pathname}`

  function handleLogout() {
    if (logoutMutation.isPending) return

    setLogoutError(undefined)
    logoutMutation.mutate(undefined, {
      onSuccess: () => navigate('/login', { replace: true }),
      onError: (error) => {
        const message = error instanceof ApiError
          ? (error.detail ?? '登出失敗，請稍後再試。')
          : '目前無法連線到系統，請稍後再試。'
        setLogoutError(message)
      },
    })
  }

  return (
    <div className="min-h-screen bg-canvas">
      <AppHeader
        companyName={user?.companyName}
        departmentName={user?.deptName}
        userName={user?.name ?? '使用者'}
        roleLabel={user ? USER_ROLE_LABEL[user.role] : undefined}
        modeLink={{ label: '前台查閱', href: import.meta.env.BASE_URL }}
        changePasswordHref={`${import.meta.env.BASE_URL}change-password`}
        logoutLabel={logoutMutation.isPending ? '登出中' : '登出'}
        onLogout={handleLogout}
      />
      <div className="flex min-h-[calc(100vh-var(--spacing-header))]">
        <SidebarNav groups={adminNavGroups} activeHref={activeHref} />
        <main className="min-w-0 flex-1 px-4 py-5 lg:px-6">
          {logoutError && (
            <Alert
              className="mb-4"
              variant="error"
              dismissAfterMs={3000}
              onDismiss={() => setLogoutError(undefined)}
            >
              {logoutError}
            </Alert>
          )}
          <Outlet />
        </main>
      </div>
    </div>
  )
}
