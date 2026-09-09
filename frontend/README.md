# ISO 文件管理系統 — Frontend

公司內部 ISO 文件管理系統的前端。一般使用者用來查詢 ISO 文件、檢視主文件、下載附件；管理人員用來維護 ISO 文件與相關主檔資料。

後端為 `../backend`（ASP.NET Core API），完整資料 schema 與 API contract 以根目錄 `SPEC.md` 為準。

## 技術棧

| 分類 | 選型 |
| --- | --- |
| 建置工具 | Vite |
| 框架 | React + TypeScript（SPA） |
| 路由 | React Router |
| Server State | TanStack Query |
| 表格 | TanStack Table（排序 / 分頁 / 展開列） |
| UI 元件 | shadcn/ui |
| 樣式 | Tailwind CSS |
| Icon | Lucide React |

> 目前 `package.json` 僅安裝 React 本體，其餘套件於對應功能開發時再逐步導入。

## 環境需求

- Node.js 20 以上（Vite 8 需求）
- npm（專案以 `package-lock.json` 鎖定版本）

## 開發指令

```bash
npm install      # 安裝相依套件
npm run dev      # 啟動開發伺服器（Vite HMR）
npm run build    # 型別檢查 + 打包（tsc -b && vite build）
npm run preview  # 本地預覽打包結果
npm run lint     # ESLint 檢查
```

開發時後端 API 預設跑在 `http://localhost:5170`（見 `backend/Properties/launchSettings.json`）。前端對後端的呼叫一律經過 `src/shared/api/httpClient.ts`，不在 component 內直接寫裸 `fetch`。

## 系統介面

### 一般使用者（Public / User Layout）

- 首頁 — ISO 文件查詢與下載
- 修改密碼

不使用 Sidebar。

### 管理介面（Admin Layout）

- 部門維護
- 使用者維護
- ISO 文件維護
- 權限維護
- ISO 文件備份
- 關於

使用 Sidebar，Sidebar 顯示各管理功能入口。

## 文件資料關係

一份 ISO 主文件可具有 0 到多個附件；主文件本身也是可檢視的檔案，不只是附件的分類節點。

```text
Document
└── Attachments[]
```

## 開發規範

動到 `frontend/` 下任何檔案前，先讀 `AGENTS.md`，重點：

- Server state 一律走 TanStack Query（`useQuery` / `useMutation`），`queryKey` 慣例 `[featureName, resource, ...params]`。
- 禁止 `any`（含隱式）；API response 都要對應明確 `interface`，欄位命名與型別與 `SPEC.md` §5 DTO 一致，不自行改名改型別。
- 列舉值用 `as const` + union type，不用裸 `string`。
- 權限不在前端推導，以後端回應（401 / 403）為準；隱藏按鈕只是 UX，不是安全機制。頁面層級限制用 `RoleGuard` 包裹。
- Tailwind class 直接寫在 JSX；重複 3 次以上才抽共用元件。
- 未定義的 API 端點、未確定的需求不得自行想像實作，先回報。

### 規格文件閱讀順序

1. 本 README
2. `docs/01-frontend-architecture.md`
3. `docs/02-routes-and-layout.md`
4. `docs/03-home.md`
5. `docs/04-change-password.md`
6. `docs/05-admin-layout.md`
7. 各管理功能規格（`docs/[未完善]-07`～`10`）
8. `docs/[未完善]-api-contract.md`

檔名前綴 `[未完善]` 表示規格仍可能調整：可建 UI 骨架與型別，但不得自行發明 API 欄位或權限矩陣，需要 placeholder 時明確標 `TODO`。

## ESLint

預設為非型別感知規則。若要啟用型別感知的嚴格檢查，將 `eslint.config.js` 中 `tseslint.configs.recommended` 換成 `recommendedTypeChecked`（或 `strictTypeChecked`），並補上 `parserOptions.project`：

```js
languageOptions: {
  parserOptions: {
    project: ['./tsconfig.node.json', './tsconfig.app.json'],
    tsconfigRootDir: import.meta.dirname,
  },
}
```

React 專屬規則可另外安裝 `eslint-plugin-react-x` 與 `eslint-plugin-react-dom`。
