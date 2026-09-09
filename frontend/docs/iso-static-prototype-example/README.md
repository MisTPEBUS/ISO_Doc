# ISO 靜態 HTML 原型

## 內容

- `index.html`
  - 前台 ISO 文件查閱
  - 不使用 Sidebar
  - 關鍵字、類別、公司、狀態動態篩選
  - 文件表格、下載按鈕、分頁元件

- `admin.html`
  - 管理介面
  - 使用 Sidebar
  - 部門維護
  - 使用者維護
  - ISO 文件維護
  - 權限維護
  - ISO 文件備份
  - 關於

- `assets/components.js`
  - `iso-topbar`
  - `admin-sidebar`
  - `document-filter-bar`
  - `document-table`
  - `pagination-bar`

- `assets/app.js`
  - 查詢條件
  - 300ms debounce
  - 靜態下載行為
  - 管理介面操作 placeholder

- `assets/styles.css`
  - Design.codex.md token
  - 40 / 36 / 32 / 28px 垂直節拍
  - 狀態圓點 + 文字
  - RWD

## 執行

直接雙擊 `index.html` 即可。

若瀏覽器限制 ES Module 的 `file://` 載入，請在資料夾內執行：

```bash
python -m http.server 8080
```

然後開啟：

```text
http://localhost:8080
```

## 搬到 Vite + React 時的元件對應

```text
src/
├─ components/
│  ├─ layout/
│  │  ├─ AppHeader.tsx
│  │  └─ AdminSidebar.tsx
│  └─ ui/
│     └─ PaginationBar.tsx
└─ features/
   └─ documents/
      └─ components/
         ├─ DocumentFilterBar.tsx
         ├─ DocumentTable.tsx
         ├─ StatusIndicator.tsx
         └─ DownloadAction.tsx
```

靜態原型使用 Web Components；`react/` 另附可搬進 Vite + React + TypeScript 的實際 TSX 元件。
