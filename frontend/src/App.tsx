import { lazy, Suspense } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import { LoginGuard } from "./app/guards/LoginGuard";
import { RoleGuard } from "./app/guards/RoleGuard";
import { PageLoading } from "./components/common";
import { USER_ROLE } from "./features/auth/types";
import { AdminLayout } from "./layouts/AdminLayout";
import { AdminDocumentsPage } from "./pages/admin/documents/AdminDocumentsPage";
import { AdminDocumentDetailPage } from "./pages/admin/documents/[id]/AdminDocumentDetailPage";
import { AdminDocumentImportPage } from "./pages/admin/documents/import/AdminDocumentImportPage";
import { AdminAttachmentImportPage } from "./pages/admin/documents/attachments/AdminAttachmentImportPage";
import { BackupPage } from "./pages/admin/backup/BackupPage";
import { DepartmentsPage } from "./pages/admin/departments/DepartmentsPage";
import { IsoCategoriesPage } from "./pages/admin/iso-categories/IsoCategoriesPage";
import { PermissionsPage } from "./pages/admin/permissions/PermissionsPage";
import { UsersPage } from "./pages/admin/users/UsersPage";
import { ChangePasswordPage } from "./pages/change-password/ChangePasswordPage";
import { HomePage } from "./pages/home/HomePage";
import { LoginPage } from "./pages/login/LoginPage";
import { Login1Page } from "./pages/login_1/Login1Page";

const ComponentPreview = import.meta.env.DEV
  ? lazy(() => import("./pages/_ComponentPreview"))
  : null;
const UiKitPage = import.meta.env.DEV
  ? lazy(() => import("./pages/ui-kit/UiKitPage"))
  : null;

const ADMIN_ROLES = [USER_ROLE.CompanyAdmin, USER_ROLE.SystemAdmin] as const;

function App() {
  return (
    <BrowserRouter basename="/ISO">
      <Suspense fallback={<PageLoading />}>
        <Routes>
          <Route
            path="/"
            element={
              <LoginGuard>
                <HomePage />
              </LoginGuard>
            }
          />
          <Route path="/login_1" element={<LoginPage />} />
          <Route path="/login" element={<Login1Page />} />

          <Route
            path="/change-password"
            element={
              <LoginGuard>
                <ChangePasswordPage />
              </LoginGuard>
            }
          />
          <Route
            path="/admin"
            element={
              <RoleGuard allow={ADMIN_ROLES}>
                <AdminLayout />
              </RoleGuard>
            }
          >
            <Route index element={<Navigate to="documents" replace />} />
            <Route path="documents" element={<AdminDocumentsPage />} />
            <Route
              path="documents/import"
              element={<AdminDocumentImportPage />}
            />
            <Route
              path="documents/attachments/import"
              element={<AdminAttachmentImportPage />}
            />
            <Route path="documents/:id" element={<AdminDocumentDetailPage />} />
            <Route path="backup" element={<BackupPage />} />
            <Route path="departments" element={<DepartmentsPage />} />
            <Route path="iso-categories" element={<IsoCategoriesPage />} />
            <Route path="permissions" element={<PermissionsPage />} />
            <Route path="users" element={<UsersPage />} />
          </Route>
          {ComponentPreview && (
            <Route path="/_component-preview" element={<ComponentPreview />} />
          )}
          {UiKitPage && (
            <Route
              path="/ui-kit"
              element={
                <LoginGuard>
                  <UiKitPage />
                </LoginGuard>
              }
            />
          )}
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}

export default App;
