# React 元件

這個目錄是靜態原型對應的 Vite + React + TypeScript 元件版本。

## 已產生

- `components/layout/app-header.tsx`
- `components/layout/app-sidebar.tsx`
- `components/ui/button.tsx`
- `components/ui/pagination-bar.tsx`
- `features/documents/components/status-indicator.tsx`
- `features/documents/components/download-action.tsx`
- `features/documents/components/document-filter-bar.tsx`
- `features/documents/components/document-table.tsx`
- `features/documents/types.ts`
- `features/auth/types.ts`
- `pages/home-page.tsx`
- `pages/admin-documents-page.tsx`

## 注意

目前 `HomePage` 先用前端陣列做查詢，以利原型驗證。

正式串 API 時建議改成：

```text
DocumentQuery
  -> TanStack Query
  -> GET /api/documents
  -> Paged<IsoDocument>
```

並把 `keyword` 的 300ms debounce 放在 query key 更新前。
