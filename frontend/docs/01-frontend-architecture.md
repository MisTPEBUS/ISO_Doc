# Frontend Architecture

## 目錄建議

```text
src/
├─ app/
│  ├─ router/
│  └─ providers/
├─ layouts/
│  ├─ MainLayout.tsx
│  └─ AdminLayout.tsx
├─ pages/
│  ├─ home/
│  ├─ change-password/
│  └─ admin/
├─ features/
│  ├─ documents/
│  ├─ departments/
│  ├─ users/
│  ├─ permissions/
│  └─ backup/
├─ components/
│  ├─ ui/
│  └─ shared/
├─ services/
│  ├─ httpClient.ts
│  └─ api/
├─ hooks/
├─ types/
├─ utils/
└─ main.tsx
```

## 分層原則

### pages

負責頁面組合，不承擔大量商業邏輯。

### features

依功能切分：

- documents
- departments
- users
- permissions
- backup

每個 feature 可包含：

```text
feature/
├─ api/
├─ components/
├─ hooks/
├─ schemas/
├─ types/
└─ utils/
```

### services

共用 HTTP Client、錯誤處理與 API 基礎設定。

### components/ui

由 shadcn/ui 管理的基礎 UI 元件。

## State

### Server State

使用 TanStack Query。

### UI State

使用 React state。

除非出現跨多頁且複雜的 Client State，否則先不要額外導入 Zustand。

## Table

ISO 文件清單使用 TanStack Table。

需要支援：

- Server-side pagination
- 動態查詢
- Row expand
- ISO管理程序與表單及附件階層
