import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'

import { RoleGuard } from './app/guards/RoleGuard'
import { PageLoading } from './components/common'
import { USER_ROLE } from './features/auth/types'
import { AdminLayout } from './layouts/AdminLayout'
import { AdminDocumentsPage } from './pages/admin/documents/AdminDocumentsPage'
import { DepartmentsPage } from './pages/admin/departments/DepartmentsPage'
import { UsersPage } from './pages/admin/users/UsersPage'
import { HomePage } from './pages/home/HomePage'
import { LoginPage } from './pages/login/LoginPage'

const ComponentPreview = import.meta.env.DEV
  ? lazy(() => import('./pages/_ComponentPreview'))
  : null

const ADMIN_ROLES = [USER_ROLE.CompanyAdmin, USER_ROLE.SystemAdmin] as const

function App() {
  return (
    <BrowserRouter basename="/ISO">
      <Suspense fallback={<PageLoading />}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/admin"
            element={(
              <RoleGuard allow={ADMIN_ROLES}>
                <AdminLayout />
              </RoleGuard>
            )}
          >
            <Route index element={<Navigate to="documents" replace />} />
            <Route path="documents" element={<AdminDocumentsPage />} />
            <Route path="departments" element={<DepartmentsPage />} />
            <Route path="users" element={<UsersPage />} />
          </Route>
          {ComponentPreview && (
            <Route path="/_component-preview" element={<ComponentPreview />} />
          )}
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}

export default App
