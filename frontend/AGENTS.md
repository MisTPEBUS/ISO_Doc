# ISO 文件管理系統 - Frontend Codex Instructions

## 專案定位

本專案為 ISO 文件管理系統前端。

技術棧：

- Vite
- React
- TypeScript
- React Router
- TanStack Query
- TanStack Table
- Tailwind CSS
- shadcn/ui
- Lucide React

## 開發原則

1. 使用 SPA 架構。
2. 前台首頁與管理介面採不同 Layout。
3. 前台首頁不使用 Sidebar。
4. 管理介面使用 Sidebar。
5. Server State 統一使用 TanStack Query。
6. Table 排序、分頁、展開列等邏輯優先使用 TanStack Table。
7. UI 元件優先使用 shadcn/ui，避免重複自行實作通用元件。
8. Icon 使用 Lucide React。
9. API 呼叫集中管理，不直接散落在 React Component 中。
10. Route、Feature、API、型別需按功能模組拆分。
11. 不要在前端自行推導權限；權限結果應以後端回傳為準。
12. 尚未確定的需求不得自行補成正式商業規則。

## 文件閱讀順序

1. `README.md`
2. `01-frontend-architecture.md`
3. `02-routes-and-layout.md`
4. `03-home.md`
5. `04-change-password.md`
6. `05-admin-layout.md`
7. 各管理功能規格
8. `[未完善]-api-contract.md`
9. `[未完善]-permission-rules.md`


## TypeScript 規則

- **禁止 `any`**，包含隱式 any（`tsconfig` 需開 `noImplicitAny` / `strict`）。無法確定型別時，優先用 `unknown` 並窄化，而不是逃到 `any`。
- 所有 API response 必須對應明確的 `interface`，且欄位命名與型別需與 `SPEC.md` 第 5 節 DTO 定義一致，不得自行改名或改型別（例如後端回 `string` 日期，前端型別就是 `string`，不要標成 `Date`）。
- 列舉值（角色、文件狀態等）使用 `as const` + union type，不使用裸 `string`：

```typescript
export const USER_ROLE = {
  User: 'USER',
  CompanyAdmin: 'COMPANY_ADMIN',
  SystemAdmin: 'SYSTEM_ADMIN',
} as const;
export type UserRole = (typeof USER_ROLE)[keyof typeof USER_ROLE];
```

## API 呼叫規則

- 所有對後端的呼叫一律經過 `shared/api/httpClient.ts`，不得在 component 或 feature 內直接寫裸 `fetch`。
- 路由、method、request/response 形狀必須與 `SPEC.md` 第 5 節逐一對齊。新增呼叫前先確認該 API 是否已在 `SPEC.md` 定義；未定義的端點不得自行想像呼叫方式。
- 資料抓取一律透過 TanStack Query 的 `useQuery` / `useMutation`，`queryKey` 命名慣例：`[featureName, resource, ...params]`，例如 `['documents', 'available', { page, keyword }]`。
- 檔案上傳使用原生 `FormData`，不引入額外上傳套件。

## 授權與路由

- 頁面層級的角色限制一律透過 `RoleGuard` 包裹，不在個別 component 內用 `if (role === ...)` 判斷是否渲染整頁。
- 元件內隱藏按鈕或欄位**不能視為安全機制**，僅是 UX 優化；實際授權以後端回應（401/403）為準，前端需正確處理這兩種狀態並導向對應提示，不得假設「按鈕沒顯示就安全了」。

## 樣式

- Tailwind class 直接寫在 JSX，不額外抽 CSS Module 或 styled-components。
- 複雜度高、重複出現 3 次以上的 class 組合，才抽成共用元件，不要提早抽象。

## 測試與品質

- `tsc --noEmit` 必須通過，不允許以 `// @ts-ignore` 規避型別錯誤，除非該行有註解說明原因並經確認為第三方套件型別缺陷。
- 表單驗證規則需與 `SPEC.md` 第 4 節 Domain 規則一致（例如密碼規則、版本號輸入方式），不得在前端自訂與後端不一致的驗證邏輯。

## 明確排除

不得實作：站內通知 UI、即時推播（SignalR/SSE client）、審核流程相關畫面（如待審清單、簽核按鈕）、SSO 登入按鈕。這些若被要求，先回報而非直接實作，詳見 root `AGENTS.md`。

## 未完善規格處理

檔名前綴為 `[未完善]` 的文件表示規格仍可能調整。

Codex 在實作這些功能時：

- 可以建立 UI 骨架與型別介面。
- 不得自行發明未定義 API 欄位。
- 不得自行決定權限矩陣。
- 不得自行決定備份與還原流程。
- 需要 placeholder 時，需明確標註 TODO。
