# Frontend 目錄結構（參考範例）

```
src/
  api/
    axiosInstance.ts
    auth.ts
    antiforgery.ts
    documents.ts
    admin/
      depts.ts
      users.ts
      documents.ts
      attachments.ts
      permissions.ts
      backup.ts

  pages/
    LoginPage.tsx
    ChangePasswordPage.tsx
    HomePage.tsx
    DeptsPage.tsx
    UsersPage.tsx
    DocumentsAdminPage.tsx
    DocumentDetailPage.tsx
    PermissionsPage.tsx
    BackupPage.tsx

  hooks/
    useCurrentUser.ts
    useLogin.ts
    useLogout.ts
    useChangePassword.ts
    useAvailableDocuments.ts
    useDownloadDocument.ts
    useDownloadAttachment.ts
    admin/
      useDepts.ts
      useCreateDept.ts
      useUpdateDept.ts
      useDeleteDept.ts
      useUsers.ts
      useCreateUser.ts
      useUpdateUser.ts
      useDeleteUser.ts
      useResetPassword.ts
      useDocumentsAdmin.ts
      useDocumentDetail.ts
      useCreateDocument.ts
      useCreateVersion.ts
      useUploadVersionFile.ts
      useUploadAttachments.ts
      useUploadAttachmentFile.ts
      useDeleteAttachment.ts
      useDeptPermissions.ts
      useUpdateDeptPermissions.ts
      useBackupDownload.ts

  components/
    common/
    ProblemDetailsAlert.tsx
    DocumentCard.tsx
    VersionTimeline.tsx
    AttachmentBatchUploadForm.tsx
    DeptPermissionSelector.tsx

  types/
    common.ts
    problemDetails.ts
    auth.ts
    documents.ts
    admin/
      depts.ts
      users.ts
      documents.ts
      permissions.ts

  services/
    fileDownload.ts

  lib/
    queryClient.ts

  data/
    roleLabels.ts
    documentStatusLabels.ts

  utils/
    date.ts
    fileValidation.ts

  App.tsx
  main.tsx
```
