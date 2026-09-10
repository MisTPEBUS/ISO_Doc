# [未完善] API Contract

## 說明

目前前端需求已確認，但後端 API Contract 尚未完整定義。

Codex 不得自行將以下內容視為正式 API。

## 首頁文件查詢

暫定：

```http
GET /api/documents
```

Query：

```text
page
pageSize
search
```

可能還會增加：

- companyId
- departmentId
- status
- documentNo
- effectiveDate

## 建議 Response Shape

```ts
type PaginatedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};
```

```ts
type DocumentListItem = {
  id: string;
  status: string;
  documentNo: string;
  name: string;
  pageCount: number | null;
  version: string;
  issueDate: string | null;
  effectiveDate: string | null;
  companyName: string;
  remark: string | null;
  attachments: AttachmentListItem[];
};
```

```ts
type AttachmentListItem = {
  id: string;
  status: string;
  attachmentNo: string;
  name: string;
  effectiveDate: string | null;
  /** 是否已上傳實體檔案；false 代表僅中繼資料、暫不可下載 */
  hasFile: boolean;
};
```

## 文件操作

暫定語意：

```text
檢視主文件
下載附件
```

實際 endpoint、下載方式、Authorization Header、Content-Disposition 規則仍待確認。

## 認證（Cookie + CSRF）

以 `SPEC.md` §4.5、§6.1 為準：

- 登入採 Cookie（`iso.session`，HttpOnly，前端不需也無法讀取），`httpClient` 一律帶 `credentials: 'include'`。
- **CSRF**：後端在「登入成功」與 `GET /api/auth/me` 回應時寫入非 HttpOnly 的 `iso.csrf` cookie。`httpClient` 需一個攔截器：所有非 `GET/HEAD` 請求，讀 `iso.csrf` cookie 值並帶入 `X-CSRF-TOKEN` header。
- `POST /api/auth/login` 不需帶 CSRF token；`POST /api/auth/logout`、`POST /api/auth/change-password` 需帶。
- 401 兩種來源：未登入 / cookie 過期（由後端 middleware 產生）、登入端點帳密錯誤。前端一律導向登入頁；帳密錯誤額外顯示錯誤訊息。

```ts
type LoginRequest = { empno: string; password: string };
type LoginResponse = {
  userId: string; name: string; role: string;
  companyId: string; companyName: string; deptName: string;
};

type MeResponse = {
  userId: string; empno: string; name: string;
  role: 'USER' | 'COMPANY_ADMIN' | 'SYSTEM_ADMIN';
  companyId: string; companyName: string;
  deptId: string; deptName: string;
  mustChangePassword: boolean;
};

type ChangePasswordRequest = {
  currentPassword: string; newPassword: string; newPasswordConfirmation: string;
};
```

## 修改密碼

- Endpoint：`POST /api/auth/change-password`，成功 `204`。
- 失敗一律 `400` + `ValidationProblemDetails`（`errors.currentPassword` / `errors.newPassword` 等），**不是 401**。
- 密碼政策：至少 8 碼，不限定字元類別（不要求中英數混合）；新密碼不可等於舊密碼；兩次新密碼需一致。前端驗證需與此一致。

## 文件維護 API（主文 / 版本 / 附件）

以 `SPEC.md` 第 5.5 節為準，重點：

- `POST /api/documents/{documentId}/versions`：`file` 選填。不帶 `file` → 版本建立為 `DRAFT`（預先建立）。
- `PUT /api/documents/{documentId}/versions/{versionId}/file`：為 `DRAFT` 版本補主文檔，轉 `PUBLISHED`。
- `POST /api/documents/{documentId}/versions/{versionId}/attachments`：multipart 陣列，單次 1..N 個附件，`items[].file` 選填（不帶即僅建立中繼資料）；批次全有全無。
- `PUT /api/attachments/{id}/file`：為未補檔的附件上傳檔案。
- 上傳一律使用原生 `FormData`，多檔以陣列欄位送出。

## 管理 API

### 公司搜尋（已確認）

```http
GET /api/companies?keyword=&page=&pageSize=
```

- 僅限 `SYSTEM_ADMIN`。
- `keyword` 比對公司代碼或名稱。
- 回傳 `PagedResult<{ id, code, name }>`。

以下仍未正式確認：

- Departments
- Users
- Permissions
- Backup
