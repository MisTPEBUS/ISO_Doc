# 後端 API 文件（ISO 文件管理系統）

> 依 `backend/` 目前實作整理（Controllers / Services / Validators / Entities / API 測試）。
> 與 `frontend/docs/[未完善]-api-contract.md` 不一致時，**以本文件為準**（該檔為早期草稿，含未實作的 `DRAFT` 版本、`PUT .../file` 等）。

---

## 1. 共通規範

### 1.1 Base URL

| 環境 | 說明                                                                                                          |
| ---- | ------------------------------------------------------------------------------------------------------------- |
| 開發 | 後端預設 `https://localhost:<port>`，所有路徑前綴 `/api`。前端建議走 Vite proxy 或 `VITE_API_BASE_URL` 設定。 |
| 正式 | 與前端同網域，反向代理 `/api/*` 至後端。                                                                      |

- 所有回應 `Content-Type: application/json`；錯誤回應為 `application/problem+json`（RFC 7807）。
- 檔案下載回應為對應的二進位 `Content-Type`（PDF / zip / office 檔），並帶 `Content-Disposition`。
- JSON 欄位命名一律 **camelCase**。
- 時間格式：
  - `createdAt` / `updatedAt` / `lastLoginAt` → `DateTimeOffset`，ISO 8601 含時區，例：`2026-09-09T03:11:58.4521240+00:00`。
  - `effectiveDate` / `expiredDate` / `publishDate` → `DateOnly`，格式 `yyyy-MM-dd`。
- 所有 id 皆為 `GUID` 字串。

### 1.2 認證（Cookie）

- 登入成功後，後端種下 **HttpOnly** 認證 cookie `isodocs.auth`（前端讀不到也不需讀），有效 8 小時、滑動展延。
- 前端所有請求必須帶 `credentials: 'include'`（fetch）／ `withCredentials: true`（axios）。
- 未登入 / cookie 失效 → `401`（`application/problem+json`，`detail = "Authentication is required."`）。前端統一導向登入頁。

### 1.3 CSRF（Antiforgery）

後端對**所有非 GET/HEAD/OPTIONS/TRACE 請求**啟用 antiforgery 驗證（`AutoValidateAntiforgeryTokenAttribute`），唯一例外是 `POST /api/auth/login`。

流程：

1. 後端會在下列時機種下**非 HttpOnly** cookie `isodocs.xsrf`（前端可讀）：
   - `POST /api/auth/login` 成功
   - `GET /api/auth/me`
   - `GET /api/antiforgery/token`（純粹為了拿 token，回 `204`）
2. 前端需要一個攔截器：對所有 `POST/PUT/DELETE/PATCH`，讀取 `isodocs.xsrf` cookie 值，放入 request header `X-XSRF-TOKEN`。
3. 缺 token 或 token 不符 → `400`（antiforgery 驗證失敗）。

> App 啟動（尚未登入）若需要先打非 GET 端點，可先 `GET /api/antiforgery/token`。一般情況登入流程本身不需 token，登入後即已取得。

### 1.4 分頁格式

清單端點回傳 `PagedResult<T>`：

```ts
type PagedResult<T> = {
  items: T[];
  page: number; // 目前頁碼（1-based）
  pageSize: number; // 實際採用的每頁筆數
  totalCount: number;
};
```

- 共通 query 參數：`page`（預設 `1`）、`pageSize`（預設 `20`，上限 `100`，超過會被夾到 100）。
- 非正整數的 `page` / `pageSize` 會被視為預設值。

### 1.5 錯誤格式（ProblemDetails）

一般錯誤（401 / 403 / 404 / 409 / 500）：

```ts
type ProblemDetails = {
  type?: string;
  title: string; // 例："Not Found" / "Conflict" / "Forbidden"
  status: number;
  detail?: string; // 人可讀訊息，前端可直接顯示
  instance?: string;
};
```

驗證錯誤（400）：

```ts
type ValidationProblemDetails = ProblemDetails & {
  errors: Record<string, string[]>; // key 為欄位名（camelCase），value 為訊息陣列
};
```

| 狀態碼 | 何時發生                                                                 | title（範例）                                                   | 備註                                                                    |
| ------ | ------------------------------------------------------------------------ | --------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `400`  | FluentValidation 失敗、商業規則驗證失敗、antiforgery 失敗、JSON 格式錯誤 | `Validation Failed` / `One or more validation errors occurred.` | 業務驗證帶自訂 `errors`；模型繫結錯誤為 ASP.NET 預設格式                |
| `401`  | 未登入 / session 過期 / 登入帳密錯誤                                     | `Unauthorized`                                                  | `detail` 可能是 `Authentication is required.` 或帳密錯誤訊息            |
| `403`  | 已登入但角色 / 公司範圍不足                                              | `Forbidden`                                                     | 泛用訊息或具體 `detail`                                                 |
| `404`  | 資源不存在或不可見                                                       | `Not Found`                                                     |                                                                         |
| `409`  | 唯一鍵衝突、狀態衝突（如刪部門仍有人）、併發衝突                         | `Conflict`                                                      |                                                                         |
| `500`  | 未預期例外                                                               | `Internal Server Error`                                         | `detail = "An unexpected error occurred while processing the request."` |

#### 範例：400（業務驗證）

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "empno": ["The employee number is already in use."]
  }
}
```

#### 範例：401

```json
{
  "title": "Unauthorized",
  "status": 401,
  "detail": "Authentication is required."
}
```

#### 範例：403

```json
{
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access departments for this company."
}
```

#### 範例：404

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "The department was not found."
}
```

#### 範例：409

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "The department cannot be deleted because it still has active users."
}
```

#### 範例：500

```json
{
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred while processing the request."
}
```

### 1.6 角色與授權範圍

角色（`role`）：`USER`、`COMPANY_ADMIN`、`SYSTEM_ADMIN`。

| Policy                 | 允許角色                                             | 額外規則                                                                                                     |
| ---------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| （一般 `[Authorize]`） | 已登入任一角色                                       | 首頁 / 下載端點                                                                                              |
| `CompanyAdminScope`    | `COMPANY_ADMIN`、`SYSTEM_ADMIN`                      | `SYSTEM_ADMIN` 可跨公司；`COMPANY_ADMIN` 只能操作自己 `companyId`。帶 `companyId` query 且非本人公司 → `403` |
| `DocumentAccess`       | 文件所屬公司的管理者，或 `USER` 且其部門有該文件授權 | 用於首頁下載                                                                                                 |

- `COMPANY_ADMIN` 打清單端點若省略 `companyId`，後端自動過濾成自己公司。
- `COMPANY_ADMIN` 不能建立 / 改為 `SYSTEM_ADMIN` 帳號 → `403`。

---

## 2. 認證 Auth

### `POST /api/auth/login`

- Auth：匿名。**不需** `X-XSRF-TOKEN`。
- Body：

```ts
type LoginRequest = { empno: string; password: string };
```

- `200 OK`：

```json
{
  "userId": "8f9c...",
  "name": "Lobinda",
  "role": "COMPANY_ADMIN",
  "companyId": "1a2b...",
  "companyName": "首都客運股份有限公司",
  "deptName": "資訊中心"
}
```

同時 `Set-Cookie: isodocs.auth=...`（HttpOnly）與 `isodocs.xsrf=...`（可讀）。

- `400`：`empno` / `password` 未填（`errors.empno` / `errors.password`）。
- `401`：帳號不存在 / 停用 / 未設密碼 / 密碼錯誤 →
  `{ "title": "尚未登入", "status": 401, "detail": "帳號或密碼錯誤。" }`

### `POST /api/auth/logout`

- Auth：已登入。需 `X-XSRF-TOKEN`。
- Body：無。
- `204 No Content`（清除認證 cookie）。
- `401`：未登入。

### `GET /api/auth/me`

- Auth：已登入。
- `200 OK`：

```ts
type MeResponse = {
  userId: string;
  empno: string;
  name: string;
  role: "USER" | "COMPANY_ADMIN" | "SYSTEM_ADMIN";
  companyId: string;
  companyName: string;
  deptId: string;
  deptName: string;
  mustChangePassword: boolean;
};
```

`companyName` / `deptName` 由後端 JOIN `companies` / `depts` 帶出（頁首顯示用）。回應時會刷新 `isodocs.xsrf` cookie。

- `401`：未登入 / 使用者已停用。

### `POST /api/auth/change-password`

- Auth：已登入。需 `X-XSRF-TOKEN`。
- Body：

```ts
type ChangePasswordRequest = {
  currentPassword: string;
  newPassword: string;
  newPasswordConfirmation: string;
};
```

- 規則：三欄皆必填；`newPasswordConfirmation` 必須等於 `newPassword`；`newPassword` 不可等於目前密碼。（後端未強制長度／字元類別；如需前端提示以產品規格為準。）
- `204 No Content`：成功（並重新簽發登入 cookie）。
- `400`：驗證失敗，例如
  - `errors.currentPassword = ["The current password is incorrect."]`
  - `errors.newPassword = ["The new password must be different from the current password."]`
  - `errors.newPasswordConfirmation = ["Password confirmation does not match."]`
- `401`：未登入。

> 密碼相關失敗一律 `400`，**不是** `401`。

---

## 3. Antiforgery

### `GET /api/antiforgery/token`

- Auth：匿名。
- `204 No Content`，`Set-Cookie: isodocs.xsrf=<token>`。
- 用途：前端在需要送出非 GET 請求前，確保已有 `isodocs.xsrf` cookie。

---

## 4. 健康檢查 Health

### `GET /api/health`

- Auth：匿名。
- `200 OK`：`{ "status": "Healthy", "database": "Healthy" }`
- `503 Service Unavailable`：`{ "status": "Unhealthy", "database": "Unhealthy" }`

---

## 5. 首頁 / 文件瀏覽（一般使用者）

> 路由 `[Authorize]`，任何已登入角色皆可；實際可見範圍依部門授權與角色。

### `GET /api/documents/available`

目前登入者「可閱讀」的文件清單（依其部門被授權、或其為公司管理者）。

- Auth：已登入（需有 `deptId`）。
- Query：`page`、`pageSize`、`keyword`（可選，比對文件編號 / 名稱）。
- `200 OK`：`PagedResult<AvailableDocumentResponse>`

```ts
type AvailableDocumentResponse = {
  documentId: string;
  documentNo: string;
  name: string;
  companyName: string;
  currentVersion: {
    versionId: string;
    version: string; // 例："1.2"
    effectiveDate: string | null; // yyyy-MM-dd
    pageCount: number | null;
  };
};
```

- `401`：未登入 / 無部門。

### `GET /api/documents/{documentId}/versions/{versionId}/download`

下載ISO管理程序檔（PDF）。

- Auth：`DocumentAccess`。
- `200 OK`：二進位串流，`Content-Type: application/pdf`（或實際型別），`Content-Disposition: attachment; filename=...`。
- `403`：無權限；或角色為 `USER` 但該版本非 `PUBLISHED`（管理者可下載 `PUBLISHED` / `OBSOLETE`）。
- `404`：`{ "detail": "The document file has not been uploaded." }`（版本存在但未上傳檔案）。

### `GET /api/documents/{documentId}/versions/{versionId}/attachments/{attachmentId}/download`

下載表單及附件檔。

- Auth：`DocumentAccess`。
- `200 OK`：二進位串流，型別依表單及附件副檔名（PDF / 影像 / office）。
- `403` / `404`：同上（`"The attachment file has not been uploaded."`）。

---

## 6. 管理 — 部門 Departments

Base：`/api/depts`，全部需 `CompanyAdminScope`。

### `GET /api/depts`

- Query：`companyId`（可選；`COMPANY_ADMIN` 帶非本人公司 → `403`）、`page`、`pageSize`。
- `200 OK`：`PagedResult<DeptResponse>`

```ts
type DeptResponse = {
  id: string;
  companyId: string;
  name: string;
  seq: number | null;
  createdAt: string;
  updatedAt: string;
};
```

- `403`。

### `POST /api/depts`

- Body：

```ts
type CreateDeptRequest = {
  companyId: string;
  name: string; // 必填，最長 100，前後空白會被 trim
  seq?: number | null;
};
```

- `201 Created`：`DeptResponse`（`Location` header 指向該部門）。
- `400`：`companyId` 空 / 公司不存在（`errors.companyId`）；`name` 空 / 超長（`errors.name`）。
- `403`：無權限操作該公司。
- `409`：同公司同名部門已存在 —
  `{ "detail": "A department with the same name already exists in this company." }`

### `GET /api/depts/{id}`

- `200 OK`：`DeptResponse`。
- `403`：非同公司。
- `404`。

### `PUT /api/depts/{id}`

- Body：

```ts
type UpdateDeptRequest = { name: string; seq?: number | null };
```

- `200 OK`：`DeptResponse`。
- `400` / `403` / `404` / `409`（同名衝突）。

### `DELETE /api/depts/{id}`

- `204 No Content`。
- `403` / `404`。
- `409`：部門仍有在職使用者 —
  `{ "detail": "The department cannot be deleted because it still has active users." }`

---

## 7. 管理 — 使用者 Users

Base：`/api/users`，全部需 `CompanyAdminScope`。

### `GET /api/users`

- Query：
  - `companyId`（可選）
  - `deptId`（可選）
  - `keyword`（可選；比對 empno / name / email）
  - `includeInactive`（`bool`，預設 `false`）
  - `page`、`pageSize`
- `200 OK`：`PagedResult<UserResponse>`

```ts
type UserResponse = {
  id: string;
  companyId: string;
  deptId: string;
  empno: string;
  name: string;
  email: string | null;
  role: "USER" | "COMPANY_ADMIN" | "SYSTEM_ADMIN";
  isActive: boolean;
  mustChangePassword: boolean;
  notifyEmailEnabled: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  updatedAt: string;
};
```

- `400`：`deptId` 不屬於該公司（`errors.deptId`）。
- `403`。

### `POST /api/users`

- Body：

```ts
type CreateUserRequest = {
  empno: string; // 必填，最長 30
  name: string; // 必填，最長 100
  email?: string | null; // 可選，需 email 格式，最長 255
  companyId: string; // 必填，公司需存在
  deptId: string; // 必填，需屬於 companyId
  role: "USER" | "COMPANY_ADMIN" | "SYSTEM_ADMIN";
  password?: string | null; // 可選；不給則帳號暫無密碼、無法登入
  passwordConfirmation?: string | null; // 有給 password 時必須一致；沒 password 時必須為 null
};
```

- `201 Created`：`UserResponse`（`isActive: true`、`notifyEmailEnabled: true`）。`Location` header 指向該使用者。
- `400`：
  - 欄位驗證（`errors.empno` / `errors.name` / `errors.email` / `errors.role` / `errors.password` / `errors.passwordConfirmation`）
  - `deptId` 不屬於公司（`errors.deptId`）
  - empno 重複 → `errors.empno = ["The employee number is already in use."]`
- `403`：無權限操作該公司；或 `COMPANY_ADMIN` 嘗試建立 `SYSTEM_ADMIN` —
  `{ "detail": "Company administrators cannot create system administrator accounts." }`

### `GET /api/users/{id}`

- `200 OK`：`UserResponse`。
- `403` / `404`。

### `PUT /api/users/{id}`

- Body：

```ts
type UpdateUserRequest = {
  name: string; // 必填，最長 100
  email?: string | null; // 可選，email 格式
  deptId: string; // 必填
  role: "USER" | "COMPANY_ADMIN" | "SYSTEM_ADMIN";
  isActive: boolean;
  notifyEmailEnabled: boolean;
};
```

- 不能透過此端點改密碼。`empno` 不可改。
- `200 OK`：`UserResponse`。
- `400`：欄位驗證；`deptId` 不屬於公司。
- `403`：無權限；或 `COMPANY_ADMIN` 嘗試操作 / 改成 `SYSTEM_ADMIN` —
  `{ "detail": "Company administrators cannot create or modify system administrator accounts." }`
- `404`。

### `DELETE /api/users/{id}`

- **軟刪除**：設 `isActive = false`，資料仍在。
- `204 No Content`。
- `403` / `404`。

### `POST /api/users/{id}/reset-password`

- Body：無。需 `X-XSRF-TOKEN`。
- `200 OK`：

```ts
type ResetPasswordResponse = { temporaryPassword: string }; // 12 碼隨機
```

同時設 `mustChangePassword = true`。前端需把臨時密碼顯示給管理者（僅此一次）。

- `403` / `404`。

---

## 8. 管理 — 文件主檔 Documents

Base：`/api/documents`，全部需 `CompanyAdminScope`。

> 注意路由共用：`GET /api/documents/available` 屬第 5 節（一般使用者）；`GET /api/documents/{id:guid}` 為本節管理端。

### `GET /api/documents`

- Query：`companyId`（可選）、`keyword`（可選；比對 documentNo / name）、`page`、`pageSize`。
- `200 OK`：`PagedResult<DocumentResponse>`

```ts
type DocumentResponse = {
  id: string;
  companyId: string;
  documentNo: string;
  name: string;
  isActive: boolean;
  createdBy: string; // userId
  createdAt: string;
  updatedAt: string;
};
```

- `403`。

### `POST /api/documents`

- Body：

```ts
type CreateDocumentRequest = {
  companyId: string; // 必填，公司需存在
  documentNo: string; // 必填，最長 50；僅允許英數與連字號，且頭尾須為英數；同公司不可重複
  name: string; // 必填，最長 255
};
```

- `201 Created`：`DocumentResponse`（`isActive: true`）。`Location` header 指向該文件。
- `400`：欄位驗證；documentNo 重複 →
  `errors.documentNo = ["The document number is already in use for this company."]`
- `403`。

### `GET /api/documents/{id}`

- `200 OK`：`DocumentDetailResponse`（含版本摘要）

```ts
type DocumentDetailResponse = {
  id: string;
  companyId: string;
  documentNo: string;
  name: string;
  isActive: boolean;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  versions: Array<{
    version: string; // 例："2.0"
    status: "PUBLISHED" | "OBSOLETE";
    effectiveDate: string | null; // yyyy-MM-dd
    expiredDate: string | null;
  }>;
};
```

- `403` / `404`。

### `PUT /api/documents/{id}`

- Body：`{ "name": string }`（必填，最長 255）。只能改名稱。
- `200 OK`：`DocumentResponse`。
- `400` / `403` / `404`。

### `DELETE /api/documents/{id}`

- **軟刪除**：設 `isActive = false`。
- `204 No Content`。
- `403` / `404`。

---

## 9. 管理 — 文件版本 Document Versions

Base：`/api/documents/{documentId}/versions`，需 `CompanyAdminScope`。

### `POST /api/documents/{documentId}/versions`

上傳並發佈新版本（**檔案為必填**，發佈後即 `PUBLISHED`，同時把先前 `PUBLISHED` 版本轉為 `OBSOLETE` 並設其 `expiredDate = effectiveDate`）。

- Content-Type：`multipart/form-data`。需 `X-XSRF-TOKEN`。
- Form 欄位：

| 欄位            | 型別         | 必填 | 說明                                                        |
| --------------- | ------------ | ---- | ----------------------------------------------------------- |
| `changeType`    | string       | ✔    | `MAJOR`（x+1.0）或 `MINOR`（x.y+1）                         |
| `effectiveDate` | `yyyy-MM-dd` | ✔    | 不得早於發佈日（今天，UTC）                                 |
| `pageCount`     | int          | ✘    | 有給須 > 0                                                  |
| `memo`          | string       | ✘    | 版本備註                                                    |
| `file`          | file         | ✔    | 副檔名須 `.pdf`，且內容前置位元須為 `%PDF-`，檔名長度 ≤ 255 |

- `201 Created`：

```ts
type DocumentVersionResponse = {
  versionId: string;
  version: string; // 例："1.1"
  status: "PUBLISHED";
};
```

> 目前實作不會回傳有效的 `Location` header（`DocumentVersions` 無 `GET` 動作），前端請直接用回應 body。

- `400`：
  - `errors.changeType`（非 MAJOR/MINOR）
  - `errors.effectiveDate`（未填 / 早於發佈日）
  - `errors.pageCount`（≤ 0）
  - `errors.file`（未附檔 / 非 .pdf / 檔名超長）
  - `errors.file = ["The document file content is not a valid PDF."]`（副檔名對但內容不是 PDF）
- `403`：非同公司。
- `404`：文件不存在（`"The document was not found."`）／公司不存在。
- `409`：
  - 文件已停用（`isActive = false`）→ `"A new version cannot be added to an inactive document."`
  - 併發發佈衝突 → `"Another version was published concurrently. Reload the document and try again."`

---

## 10. 管理 — 表單及附件 Attachments

### `GET /api/documents/{documentId}/versions/{versionId}/attachments`

- Auth：`CompanyAdminScope`。
- `200 OK`：**陣列**（非分頁）

```ts
type AttachmentResponse = {
  attachmentId: string;
  attachmentNo: string;
  name: string;
  hasFile: boolean; // false 代表僅建立中繼資料、尚未有實體檔
};
```

- `403` / `404`（版本不存在）。

### `POST /api/documents/{documentId}/versions/{versionId}/attachments`

批次新增表單及附件（**全有全無**：任一項失敗整批 rollback，且不留下已寫入的檔案）。

- Content-Type：`multipart/form-data`。需 `X-XSRF-TOKEN`。
- Form 欄位（`items` 為陣列，索引從 0）：

| 欄位                    | 型別   | 必填 | 說明                                                                                                          |
| ----------------------- | ------ | ---- | ------------------------------------------------------------------------------------------------------------- |
| `items[i].attachmentNo` | string | ✔    | 最長 50；同一版本內不可重複（也不可與請求內其他項重複）                                                       |
| `items[i].name`         | string | ✔    | 最長 255                                                                                                      |
| `items[i].file`         | file   | ✘    | 不給 → 僅建立中繼資料。副檔名須為 `.jpg .jpeg .png .pdf .doc .docx .xls .xlsx .odt .ods` 之一，檔名長度 ≤ 255 |

至少要有 1 項（`errors.items`）。

- `201 Created`：

```ts
type CreateAttachmentsResponse = {
  created: Array<{
    attachmentId: string;
    attachmentNo: string;
    hasFile: boolean;
  }>;
};
```

- `400`：
  - `errors.items` / `errors["items[0].attachmentNo"]` / `errors["items[0].name"]` / `errors["items[0].file"]`（欄位驗證；副檔名不允許）
  - `errors.attachmentNo`（請求內重複，或與現有版本表單及附件編號衝突）—
    例：`["Attachment number 'ATT-01' is already in use for this version."]`
- `403` / `404`。

### `DELETE /api/attachments/{id}`

- Auth：`CompanyAdminScope`。需 `X-XSRF-TOKEN`。
- `204 No Content`（實體檔會被移到 trash）。
- `403` / `404`。

---

## 11. 管理 — 文件部門授權 Permissions

Base：`/api/documents/{documentId}/dept-permissions`，需 `CompanyAdminScope`。

### `GET /api/documents/{documentId}/dept-permissions`

- `200 OK`：

```ts
type DocumentDeptPermissionsResponse = { deptIds: string[] }; // 已排序
```

- `403` / `404`（文件不存在）。

### `PUT /api/documents/{documentId}/dept-permissions`

以「完整清單」覆寫該文件的部門授權（送出的即為最終狀態，後端計算增刪）。

- Body：

```ts
type UpdateDocumentDeptPermissionsRequest = { deptIds: string[] }; // 必填、非空
```

- 所有 `deptId` 必須存在且屬於文件所屬公司。
- `200 OK`：`DocumentDeptPermissionsResponse`（回傳套用後的清單）。
- `400`：
  - `errors.deptIds = ["Department ids are required."]` / `["At least one department id is required."]`
  - `errors.deptIds = ["Every department id must exist and belong to the document's company."]`
- `403` / `404`。

---

## 12. 管理 — 公司備份 Backup

### `GET /api/companies/{companyId}/backup`

下載該公司所有 `PUBLISHED` 版本ISO管理程序與表單及附件的 zip。

- Auth：`CompanyAdminScope`（`COMPANY_ADMIN` 僅限自己公司）。
- `200 OK`：`Content-Type: application/zip`，`Content-Disposition: attachment; filename*=<公司名>_backup_yyyyMMdd.zip`。
  - zip 內結構：`<documentNo>/v<version>/main/<檔名>`、`<documentNo>/v<version>/attachments/<attachmentNo>_<檔名>`。
- `403`：非本人公司。
- `404`：公司不存在（以 `application/problem+json` 回傳）。

> 這是串流下載，前端請以 `blob` 接收並觸發存檔，不要用 JSON 解析。

---

## 13. AI 匯入 Import（AI 輔助批次匯入）

> 適用情境：使用者一次拖曳整個資料夾（可能含多份ISO管理程序與其表單及附件）進來，由 AI（gpt-5.6-terra，透過 Responses API + Structured Outputs）協助解析檔名/資料夾結構，經人工確認（Human Confirm）編輯後才真正寫入。與第 8-10 節既有的單筆 CRUD／版本 API 並存；本節是「批次 + AI 輔助」情境專用的協調層，底層仍遵循相同的版本／狀態規則，不新增資料表。

流程分兩步：`analyze`（唯讀，不寫入、不需檔案內容）→ 使用者在畫面上編輯確認 → `commit`（multipart，帶檔案，真正落地）。

### `POST /api/documents/ai-import/analyze`

- Auth：`CompanyAdminScope` + CSRF。
- Body：

```ts
type AnalyzeImportRequest = {
  companyId: string;
  files: Array<{
    originalFileName: string;
    relativePath: string;      // 批次內的自然鍵，commit 時用它對應檔案
    size: number;
    role: "MAIN" | "ATTACHMENT" | "MAIN_CANDIDATE" | "UNRESOLVED";
    documentNo: string;        // role=MAIN/ATTACHMENT 時來自前端本地解析的初猜
    attachmentNo: string;      // role=MAIN 時為空字串
    displayName: string;
    extension: string;
    parseStatus: "OK" | "WARNING";
    checksum: string;          // 前端算好的 SHA-256 hex
  }>;
};
```

- `200 OK`：

```ts
type AnalyzeImportResponse = {
  analysisId: string;
  documents: Array<{
    documentNo: string;
    name: string;
    effectiveDate: string | null;      // yyyy-MM-dd，AI 解析不出來就是 null
    suggestedVersion: string | null;   // 見下方版號規則
    predictedAction: "NEW_DOCUMENT" | "UPLOAD_DRAFT_FILE" | "NEW_VERSION" | "SKIP_UNCHANGED";
    confidence: "HIGH" | "MEDIUM" | "LOW";
    existing: {
      documentId: string | null;
      latestVersion: string | null;
      latestVersionStatus: "DRAFT" | "PUBLISHED" | "OBSOLETE" | null;
      latestVersionHasFile: boolean;
    };
    mainFile: { relativePath: string } | null;
    attachments: Array<{
      attachmentNo: string | null;   // null 表示辨識不出編號；一律視為 NEW_ATTACHMENT，不與既有表單及附件比對
      name: string;
      effectiveDate: string | null;
      suggestedVersion: string | null;
      predictedAction: "NEW_ATTACHMENT" | "NEW_VERSION" | "SKIP_UNCHANGED";
      relativePath: string;
      existing: { attachmentId: string | null; latestVersion: string | null };
    }>;
  }>;
  unresolved: Array<{ relativePath: string; reason: string }>;
};
```

`suggestedVersion` 規則：`NEW_DOCUMENT` / `NEW_ATTACHMENT` → `"1.0"`；`NEW_VERSION` → 目前最新版 **MINOR bump**（x.y+1，AI 一律採小版本，如需跳 MAJOR 版由使用者在確認畫面手動改）；`UPLOAD_DRAFT_FILE` → 沿用該草稿本身版號（不變）；`SKIP_UNCHANGED` → 僅供參考。此欄位前端可編輯，使用者改過的值才是 `commit` 送出的依據。

- `400`：`errors.files`（空陣列）、`errors.companyId`。
- `403`：非本人公司。
- `500`：呼叫 LLM 服務失敗（逾時／回應不符 Structured Output schema），沿用既有未預期例外的錯誤格式。

### `POST /api/documents/ai-import/commit`

使用者於 Human Confirm 畫面編輯確認 `analyze` 的結果後送出，帶著實體檔案，依規則寫入。以「一個ISO管理程序＋它的表單及附件」為處理單位，各自獨立成功／失敗；同一份文件下的表單及附件若因ISO管理程序那步驟失敗而被跳過，原因標記為 `PARENT_DOCUMENT_FAILED`。

- Content-Type：`multipart/form-data`。需 `X-XSRF-TOKEN`。
- Form 欄位（`documents[i]` / `documents[i].attachments[j]`，索引皆從 0）：

| 欄位 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `companyId` | string | ✔ | |
| `analysisId` | string | ✘ | 對應 `analyze` 的結果，供稽核追溯 |
| `documents[i].documentNo` | string | ✔ | |
| `documents[i].name` | string | ✔ | |
| `documents[i].version` | string | 有 `mainFile` 或該文件是新建時必填 | 使用者確認／編輯過的最終版號 |
| `documents[i].effectiveDate` | `yyyy-MM-dd` | 有 `mainFile` 時必填 | |
| `documents[i].pageCount` | int | ✘ | |
| `documents[i].isoCategoryId` | uuid | ✘ | 品質系統；新文件留空為 null，既有文件留空保留原值；指定時須屬於 companyId |
| `documents[i].deptId` | uuid | ✘ | 發行部門；新文件留空為 null，既有文件留空保留原值；指定時須屬於 companyId |
| `documents[i].mainFile` | file | ✘ | 不給表示這筆僅更新中繼資料或維持草稿無檔 |
| `documents[i].attachments[j].attachmentNo` | string | ✘ | 可留空；部分掃描檔案本來就沒有編號規則，留空一律視為新增表單及附件（不比對既有表單及附件、不會被判定 `UNCHANGED`） |
| `documents[i].attachments[j].name` | string | ✔ | |
| `documents[i].attachments[j].version` | string | 有 `file` 時必填 | |
| `documents[i].attachments[j].effectiveDate` | `yyyy-MM-dd` | ✘ | |
| `documents[i].attachments[j].file` | file | ✘ | 不給表示僅中繼資料 |

`isoCategoryId`／`deptId` 無效（不存在或跨公司）時只讓該群組列入 `documents.failed`，其附件以 `PARENT_DOCUMENT_FAILED` 略過；其餘群組仍處理。既有文件要清空這兩欄請用文件編輯頁；`SKIP_UNCHANGED` 只指主文檔案／版本未變，主檔欄位仍可能更新。

- `200 OK`：

```ts
type CommitImportResponse = {
  documents: {
    total: number; successCount: number; failureCount: number;
    succeeded: Array<{
      index: number; documentNo: string; documentId: string;
      action: "NEW_DOCUMENT" | "UPLOAD_DRAFT_FILE" | "NEW_VERSION" | "SKIP_UNCHANGED";
      documentVersionId: string | null; version: string | null;
      effectiveDate: string | null; status: string;
    }>;
    failed: Array<{ index: number; documentNo: string; errors: Record<string, string[]> }>;
  };
  attachments: {
    total: number; successCount: number; skippedCount: number; failureCount: number;
    succeeded: Array<{
      index: number; documentIndex: number; attachmentNo: string | null; attachmentId: string;
      action: "NEW_ATTACHMENT" | "NEW_VERSION";
      attachmentVersionId: string | null; version: string | null;
    }>;
    skipped: Array<{
      index: number; documentIndex: number; attachmentNo: string | null;
      reason: "UNCHANGED" | "PARENT_DOCUMENT_FAILED";
    }>;
    failed: Array<{ index: number; documentIndex: number; attachmentNo: string | null; errors: Record<string, string[]> }>;
  };
};
```

**業務規則（儲存規則）**：

1. ISO管理程序：判斷 `documentNo` 是否存在於該公司。
   - 不存在 → 新增 Document ＋ 第一版 DocumentVersion（`action = NEW_DOCUMENT`）。
   - 存在 → 取該文件目前最新一版：
     - 最新版 `Status = DRAFT` 且無 `FileKey`（草稿沒有表單及附件狀態）→ 帶檔案的話補到該版本（`action = UPLOAD_DRAFT_FILE`，版號不變）。
     - 最新版已有 `FileKey` → 帶新檔案就新增版本、舊版轉 `OBSOLETE`（`action = NEW_VERSION`）；沒帶新檔案 → `SKIP_UNCHANGED`。
2. 表單及附件：
   - 3-1：對應ISO管理程序這次未成功建立／找不到 → 該ISO管理程序底下所有表單及附件標記 `skipped(reason = PARENT_DOCUMENT_FAILED)`，不處理。
   - 3-2：ISO管理程序成立的情況下：
     - `attachmentNo` 為空／null（無法辨識編號）→ 一律視為新增（`action = NEW_ATTACHMENT`），不查詢、不比對既有表單及附件，儲存路徑改用表單及附件的內部 GUID 命名。
     - `attachmentNo` 於此文件下不存在 → 新增 Attachment ＋ 第一版（`action = NEW_ATTACHMENT`）。
     - `attachmentNo` 已存在 → 比對現有最新版 `Checksum` 與這次上傳檔案內容：相同 → `skipped(reason = UNCHANGED)`（即使 `version` 欄位有填也忽略，不建立新版本）；不同 → 新增版本（`action = NEW_VERSION`）。

- `400`：欄位驗證失敗、`version` 格式不符（同 §9 規則）、`documentNo` / `attachmentNo` 於批次內重複。
- `403`：非本人公司。
- `409`：`version` 與該文件／表單及附件既有版號重複。

---

## 14. 端點速查表

| #   | Method | Path                                                                                   | 授權                     | 成功碼           |
| --- | ------ | -------------------------------------------------------------------------------------- | ------------------------ | ---------------- |
| 1   | POST   | `/api/auth/login`                                                                      | 匿名                     | 200              |
| 2   | POST   | `/api/auth/logout`                                                                     | 已登入 + CSRF            | 204              |
| 3   | GET    | `/api/auth/me`                                                                         | 已登入                   | 200              |
| 4   | POST   | `/api/auth/change-password`                                                            | 已登入 + CSRF            | 204              |
| 5   | GET    | `/api/antiforgery/token`                                                               | 匿名                     | 204              |
| 6   | GET    | `/api/health`                                                                          | 匿名                     | 200 / 503        |
| 7   | GET    | `/api/documents/available`                                                             | 已登入                   | 200              |
| 8   | GET    | `/api/documents/{documentId}/versions/{versionId}/download`                            | DocumentAccess           | 200（檔案）      |
| 9   | GET    | `/api/documents/{documentId}/versions/{versionId}/attachments/{attachmentId}/download` | DocumentAccess           | 200（檔案）      |
| 10  | GET    | `/api/depts`                                                                           | CompanyAdminScope        | 200              |
| 11  | POST   | `/api/depts`                                                                           | CompanyAdminScope + CSRF | 201              |
| 12  | GET    | `/api/depts/{id}`                                                                      | CompanyAdminScope        | 200              |
| 13  | PUT    | `/api/depts/{id}`                                                                      | CompanyAdminScope + CSRF | 200              |
| 14  | DELETE | `/api/depts/{id}`                                                                      | CompanyAdminScope + CSRF | 204              |
| 15  | GET    | `/api/users`                                                                           | CompanyAdminScope        | 200              |
| 16  | POST   | `/api/users`                                                                           | CompanyAdminScope + CSRF | 201              |
| 17  | GET    | `/api/users/{id}`                                                                      | CompanyAdminScope        | 200              |
| 18  | PUT    | `/api/users/{id}`                                                                      | CompanyAdminScope + CSRF | 200              |
| 19  | DELETE | `/api/users/{id}`                                                                      | CompanyAdminScope + CSRF | 204              |
| 20  | POST   | `/api/users/{id}/reset-password`                                                       | CompanyAdminScope + CSRF | 200              |
| 21  | GET    | `/api/documents`                                                                       | CompanyAdminScope        | 200              |
| 22  | POST   | `/api/documents`                                                                       | CompanyAdminScope + CSRF | 201              |
| 23  | GET    | `/api/documents/{id}`                                                                  | CompanyAdminScope        | 200              |
| 24  | PUT    | `/api/documents/{id}`                                                                  | CompanyAdminScope + CSRF | 200              |
| 25  | DELETE | `/api/documents/{id}`                                                                  | CompanyAdminScope + CSRF | 204              |
| 26  | POST   | `/api/documents/{documentId}/versions`                                                 | CompanyAdminScope + CSRF | 201（multipart） |
| 27  | GET    | `/api/documents/{documentId}/versions/{versionId}/attachments`                         | CompanyAdminScope        | 200（陣列）      |
| 28  | POST   | `/api/documents/{documentId}/versions/{versionId}/attachments`                         | CompanyAdminScope + CSRF | 201（multipart） |
| 29  | DELETE | `/api/attachments/{id}`                                                                | CompanyAdminScope + CSRF | 204              |
| 30  | GET    | `/api/documents/{documentId}/dept-permissions`                                         | CompanyAdminScope        | 200              |
| 31  | PUT    | `/api/documents/{documentId}/dept-permissions`                                         | CompanyAdminScope + CSRF | 200              |
| 32  | GET    | `/api/companies/{companyId}/backup`                                                    | CompanyAdminScope        | 200（zip）       |
| 33  | POST   | `/api/documents/ai-import/analyze`                                                     | CompanyAdminScope + CSRF | 200              |
| 34  | POST   | `/api/documents/ai-import/commit`                                                      | CompanyAdminScope + CSRF | 200（multipart） |
