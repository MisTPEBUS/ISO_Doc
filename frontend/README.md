# ISO 文件管理系統 Frontend

## 目的

提供公司內部使用者查詢 ISO 文件、檢視主文件、下載附件，以及管理人員維護 ISO 文件相關資料。

## 前端技術

- Vite
- React
- TypeScript
- React Router
- TanStack Query
- TanStack Table
- Tailwind CSS
- shadcn/ui
- Lucide React

## 系統介面

### 一般使用者

- 首頁
- 修改密碼

### 管理介面

- 部門維護
- 使用者維護
- ISO 文件維護
- 權限維護
- ISO 文件備份
- 關於

## Layout

### Public / User Layout

- 不使用 Sidebar。
- 主要用途為 ISO 文件查詢與下載。

### Admin Layout

- 使用 Sidebar。
- Sidebar 顯示管理功能入口。

## 文件關係

一份 ISO 主文件可具有 0 到多個附件。

```text
Document
└── Attachments[]
```

主文件本身也是可檢視的檔案，不只是附件的分類節點。
