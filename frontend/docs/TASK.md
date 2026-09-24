# TASK — 前端里程碑（給 Codex 的提示）

搭配 [`backend-api.md`](./backend-api.md)（API 契約）與 [`AGENTS.md`](../AGENTS.md)（規則）。
本檔只給「目標 / 產出哪些檔 / 驗收」，**API 細節一律去 `backend-api.md` 查，不在這裡展開**。

## 目前完成度（2026-09）

- ✅ `src/components/common/*`：手寫 primitives（Button / Input / Select / Table / Modal / Pagination / Alert / Badge / FormField / Spinner / AppHeader / SidebarNav）。
- ✅ Vite + React 19 + TS strict + Tailwind v4；`react-router-dom` v7、`@tanstack/react-query` v5、`zod` v4 已在 deps。
- ❌ 尚無：API 層、axios、QueryClient/Provider、`.env`/proxy、`features/`、正式 `pages/`、`shared/`、`lib/`、router（僅 starter page）。

## 給 Codex 的通則

- **HTTP：** 一律用 **單一 axios instance**（`src/shared/api/httpClient.ts`）。禁止 component / feature 內裸 `fetch` / `axios`。
- **可以裝套件，選型交給你**：UI 用 shadcn/ui（`npx shadcn add …`）、Icon 用 `lucide-react`、Table 進階行為用 `@tanstack/react-table`、表單可用 `react-hook-form` + `@hookform/resolvers/zod` 或純受控 — 自行決定，但整個專案要一致。既有 `components/common/*` 能用就用，別重造。
- **一檔一職責**，不要把 api + 型別 + zod + hook 塞進同一份檔（結構見下）。
- `queryKey` 慣例：`[featureName, resource, ...params]`，例 `['documents','available',{ page, keyword }]`。
- 列舉用 `as const` + union，不用裸 string。權限不在前端推導，以後端 401 / 403 為準。
- 每個里程碑結束：`npm run build`（含 `tsc -b`）＋ `npm run lint` 綠燈。
- 一次做一個里程碑，PR 內說明碰到的 `TODO` / 待確認項。

## 檔案結構（每個里程碑遵守）

每個 feature 一包，職責拆檔：

```text
src/features/<name>/
  api.ts        裸函式：呼叫 httpClient，回 Promise<DTO>
  types.ts      interface / union（對齊 backend-api.md，日期是 string）
  schemas.ts    zod（表單輸入驗證，規則對齊後端，不加嚴）
  queries.ts    useQuery / useMutation；超過 ~5 個 hook 就拆 hooks/，一個 hook 一檔
  components/    該 feature 專屬 UI
```

共用層：

```text
src/api/     httpClient.ts  apiError.ts  fileDownload.ts  formData.ts
src/types/   pagination.ts  problem.ts
src/config/  env.ts
src/lib/            queryClient.ts
src/app/            providers/  router/  guards/
```

---

## 里程碑 0 — HTTP 與 Query 基礎建設

**Prompt：**
> 裝 `axios`。建 `src/shared/api/httpClient.ts`：axios instance，`baseURL` 取 `env.apiBaseUrl`（`VITE_API_BASE_URL`，預設 `/api`）、`withCredentials: true`。
> Request interceptor：非 GET/HEAD 時從 `isodocs.xsrf` cookie 取值塞 `X-XSRF-TOKEN` header。
> Response interceptor：成功回 `response.data`；失敗把 `application/problem+json` 轉成 `ApiError`（`status / title / detail / errors?`，提供 `fieldErrors()`、`isValidation`）；`401`（非 login 端點）發 `auth:unauthorized` 事件。
> 建 `shared/types/pagination.ts`、`shared/types/problem.ts`（型別見 `backend-api.md` §1.4 / §1.5）。
> 建 `lib/queryClient.ts`：`ApiError` 且 status ∈ {401,403,404} 不 retry，其餘 retry 1；mutation 不 retry。
> 建 `app/providers/AppProviders.tsx` 包 `QueryClientProvider`（dev 加 devtools），掛進 `main.tsx`。
> `vite.config.ts` 加 `server.proxy` `/api` → 後端 https（`secure:false`）；新增 `.env.example`。

**驗收：** 透過 dev server 打 `GET /api/health` 回 200；未登入打 `/api/auth/me` 得 `ApiError{status:401}`。

---

## 里程碑 1 — Auth 與路由骨架

契約：`backend-api.md` §2 / §3 / §1.2 / §1.3 / §1.6。規格：`02-routes-and-layout.md`、`04-change-password.md`。

**Prompt：**
> `features/auth`：`types.ts`（`LoginRequest/LoginResponse/MeResponse/ChangePasswordRequest`、`Role` union）、`api.ts`（`login/logout/getMe/changePassword/fetchAntiforgeryToken`）、`schemas.ts`（登入、修改密碼 zod；改密碼：三欄必填、兩次一致、新≠舊）、`queries.ts`（`useCurrentUser`（401→null、不 retry）/ `useLogin` / `useLogout` / `useChangePassword`，成功後 invalidate `['auth','me']`）。
> `app/guards/RoleGuard.tsx`：未登入→`/login`；`mustChangePassword`→鎖在 `/change-password`；角色不符→403 頁。
> 監聽 `auth:unauthorized`→清 me 快取、導 `/login`。
> `app/router/`：`/login`、`/change-password`、`/`（MainLayout）、`/admin/*`（AdminLayout + RoleGuard `['COMPANY_ADMIN','SYSTEM_ADMIN']`）。Layout 用既有 `AppHeader` / `SidebarNav` 或 shadcn。
> `pages/login`、`pages/change-password`：最小可用表單，錯誤用 `ApiError` 呈現。

**驗收：** 冷啟動→登入頁；登入成功→`/`；被 reset 的帳號→強制 `/change-password`；刪 cookie 後任一請求→自動回登入。

---

## 里程碑 2 — 首頁：可讀文件 + 下載

契約：`backend-api.md` §5。規格：`03-home.md`。

**Prompt：**
> `shared/api/fileDownload.ts`：`downloadBlob(blob, filename)`、`requestFileDownload(path, params?)`（axios `responseType:'blob'`，從 `Content-Disposition` 解析檔名含 `filename*`）。
> `features/documents`：`types.ts`、`api.ts`（`listAvailable` / `downloadDocument` / `downloadAttachment`）、`queries.ts`（`useAvailableDocuments` query；下載為 mutation）。
> `pages/home`：搜尋框 + 分頁表格 + 每列下載；下載 403/404 用 Alert 顯示 `detail`。
> 表單及附件展開列：`03-home.md` 有標「後端目前缺使用者可讀的表單及附件清單端點」，先做主檔清單，表單及附件子表格留 `TODO`。

**驗收：** USER 登入能看清單、分頁、下載 PDF，檔名與後端一致。

---

## 里程碑 3 — 部門維護（CRUD 樣板）

契約：`backend-api.md` §6。此里程碑的結構＝之後所有 admin feature 的模板。

**Prompt：**
> `features/departments`：`types.ts`、`api.ts`（`list/get/create/update/remove`）、`schemas.ts`（`name` 必填 ≤100、`seq` 選填整數）、`queries.ts`（`useDepts/useDept/useCreateDept/useUpdateDept/useDeleteDept`，mutation 成功 invalidate list）。
> 錯誤：`409`（同名）綁 `name` 欄位；`409`（刪除時仍有在職使用者）給對話框層級訊息，不當未知錯誤。
> `pages/admin/departments`：表格 +「新增」Modal 表單 + 每列編輯 / 刪除；欄位錯誤讀 `ApiError.fieldErrors()`。

**驗收：** 建立 / 重名 409 / 編輯 / 刪除（有人時 409）流程正確，列表自動刷新。

---

## 里程碑 4 — 使用者維護

契約：`backend-api.md` §7。規格：`[未完善]-07-admin-users.md`（未定案處標 `TODO`）。

**Prompt：**
> 照里程碑 3 結構建 `features/users`。`api.ts` 多 `resetPassword(id) → { temporaryPassword }`。
> `schemas.ts`：`empno` ≤30 必填、`name` ≤100 必填、`email` 選填 email、`role` union、密碼與確認碼一致（可留空）。
> `queries.ts`：list（含 `deptId` / `keyword` / `includeInactive`）、CRUD、`useResetPassword`。
> 錯誤：empno 重複→`errors.empno`；`403` 提及 system administrator→明確權限提示。
> 頁面：篩選列 + 表格 + 新增 / 編輯 Modal +「重設密碼」（一次性顯示臨時密碼可複製）。刪除為軟刪，需 `includeInactive` 切換。

**驗收：** 建立（短密碼可）/ 重複 empno 400 / reset 拿到臨時密碼 / 軟刪後需勾選才可見。

---

## 里程碑 5 — ISO 文件主檔 + 詳情

契約：`backend-api.md` §8。規格：`[未完善]-08-admin-documents.md`。

**Prompt：**
> `features/admin-documents`：`types.ts`（`AdminDocument`、`DocumentDetail` 含 `versions[]`）、`api.ts`（`list/get/create/update/remove`）、`schemas.ts`（`documentNo`：≤50、僅英數與連字號、頭尾英數；`name` ≤255）、`queries.ts`。
> `update` 只有 `name`；`remove` 為軟刪。`documentNo` 重複→`errors.documentNo`。
> `pages/admin/documents`：清單頁；`pages/admin/documents/[id]`：詳情頁 + 版本時間軸（`PUBLISHED`/`OBSOLETE`）。版本 / 表單及附件操作留下一個里程碑。

**驗收：** 建立（含 documentNo 格式驗證）/ 詳情看到版本清單。

---

## 里程碑 6 — 版本上傳（multipart）

契約：`backend-api.md` §9。

**Prompt：**
> `shared/api/formData.ts`：`buildVersionFormData(input)`，欄位 `changeType` / `effectiveDate`(yyyy-MM-dd) / `pageCount?` / `memo?` / `file`，只 append 有值欄位。
> `features/admin-documents` 增 `versionsApi.ts`（`createVersion(documentId, formData)`）、`versionSchemas.ts`（`changeType` ∈ MAJOR/MINOR、`effectiveDate` 不早於今天、`pageCount>0`、`file` 為 `.pdf`、檔名 ≤255）、`useCreateVersion`（成功 invalidate 文件詳情）。
> 錯誤分流：`400`（欄位 / 非 PDF）、`404`（文件不存在）、`409`（文件停用 / 併發）各給不同訊息。
> 詳情頁加「新增版本」Modal 表單。

**驗收：** 上傳 MINOR 版本後詳情刷新、舊版轉 OBSOLETE；非 PDF 前後端各擋一次。

---

## 里程碑 7 — 表單及附件批次上傳 + 刪除

契約：`backend-api.md` §10。

**Prompt：**
> `shared/api/formData.ts` 增 `buildAttachmentsFormData(items)`：每項 `items[i].attachmentNo` / `items[i].name` / `items[i].file?`，索引 0 起連續。
> `features/admin-documents` 增 `attachmentsApi.ts`（`listAttachments`（回陣列，非分頁）/ `createAttachments` / `removeAttachment`）、`attachmentSchemas.ts`（`attachmentNo` ≤50、`name` ≤255、`file` 副檔名限 jpg/jpeg/png/pdf/doc/docx/xls/xlsx/odt/ods、批次內 `attachmentNo` 不重複）、對應 hooks。
> 批次全有全無：任一項 `400`→提示是哪一列（讀 `errors["items[i].*"]` 或 `errors.attachmentNo`）。
> 詳情頁版本區塊加表單及附件清單 + 批次上傳表單（可多列、可「僅中繼資料」不帶檔）+ 刪除。

**驗收：** 一次上傳「帶檔 + 純中繼資料」兩項成功；副檔名不符整批被拒；刪除後清單刷新。

---

## 里程碑 8 — 權限維護

契約：`backend-api.md` §11。規格：`[未完善]-09-admin-permissions.md`。

**Prompt：**
> `features/permissions`：`api.ts`（`get(documentId) → { deptIds }`、`update(documentId, { deptIds })`）、`queries.ts`（`useDeptPermissions` / `useUpdateDeptPermissions`）。
> `update` 送**完整** `deptIds` 陣列（非 diff）；空陣列後端會 400，UI 送出前擋。
> 頁面：選一份文件→用 `features/departments` 的 `useDepts({ companyId })` 取可選部門→多選 checkbox→儲存。

**驗收：** 設定部門後，該部門的 USER 在首頁看得到該文件並可下載。

---

## 里程碑 9 — 公司備份下載

契約：`backend-api.md` §12。規格：`[未完善]-10-admin-backup.md`。

**Prompt：**
> `features/backup`：`api.ts`（`downloadBackup(companyId)` 走 `shared/api/fileDownload.ts`）、`queries.ts`（`useDownloadBackup` mutation）。
> 頁面：一顆下載鍵，進行中 loading（檔案大、串流），`403/404` 用 Alert。

**驗收：** 下載 zip 成功、檔名為 `<公司>_backup_yyyyMMdd.zip`。

---

## 收尾煙霧測試

以 `COMPANY_ADMIN` 走完整流程：登入 → 部門 CRUD（重名 409 / 刪除 409）→ 使用者 CRUD + reset → 文件建立 + 上傳版本 + 批次表單及附件 → 設權限 → 換該部門 USER 登入首頁下載 → 備份下載 → 登出後 API 401 導回登入。
