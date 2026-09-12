# ISO 文件管理系統 — 後端 API 文件

依 `backend/Features/**`（Controller / Service / Validator）與 API 測試整理。此為後端對外契約的權威文件。

- Postman 匯入檔：[`ISO-Docs-API.postman_collection.json`](./ISO-Docs-API.postman_collection.json) ＋ [`ISO-Docs-API.postman_environment.json`](./ISO-Docs-API.postman_environment.json)（匯入方式見文末）。
- 資料庫結構見 [`Schema.md`](./Schema.md)。

---

## 1. 共通規範

### 1.1 位址

| Profile | URL |
| --- | --- |
| https（建議） | `https://localhost:7167` |
| http | `http://localhost:5170` |

所有路徑前綴 `/api`。認證 / CSRF cookie 皆為 `Secure`，建議一律走 https（Postman 需關閉憑證驗證，見文末）。

### 1.2 認證（Cookie）

- 登入成功後，後端種下 **HttpOnly** 認證 cookie `isodocs.auth`（8 小時、滑動展延）。
- 用戶端每次請求都要送出該 cookie（瀏覽器 `credentials: 'include'`；Postman 自動管理 cookie jar）。
- 未登入 / cookie 失效 → `401`，`application/problem+json`，`detail = "Authentication is required."`。

### 1.3 CSRF（Antiforgery）

後端對**所有非 GET/HEAD/OPTIONS/TRACE 請求**驗證 antiforgery token，唯一例外是 `POST /api/auth/login`。

1. 後端會在以下回應種下**非 HttpOnly** cookie `isodocs.xsrf`（用戶端可讀）：
   - `POST /api/auth/login` 成功
   - `GET /api/auth/me`
   - `GET /api/antiforgery/token`（回 `204`，專門用來取 token）
2. 用戶端對 `POST/PUT/DELETE/PATCH`：讀 `isodocs.xsrf` cookie 值，放入 request header **`X-XSRF-TOKEN`**。
3. 缺 token 或不符 → `400`。

### 1.4 分頁

清單端點回傳 `PagedResult<T>`：

```jsonc
{
  "items": [ /* T[] */ ],
  "page": 1,          // 目前頁碼（1-based）
  "pageSize": 20,     // 實際採用的每頁筆數
  "totalCount": 137
}
```

共通 query：`page`（預設 `1`）、`pageSize`（預設 `20`，上限 `100`）。非正整數視為預設值。

### 1.5 錯誤格式（RFC 7807 ProblemDetails）

一般錯誤：

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.x",
  "title": "Not Found",
  "status": 404,
  "detail": "The department was not found."
}
```

驗證錯誤（400）帶 `errors`：

```jsonc
{
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "empno": ["The employee number is already in use."]
  }
}
```

| 狀態碼 | 何時 | 備註 |
| --- | --- | --- |
| `400` | FluentValidation 失敗、商業規則驗證、antiforgery 失敗、JSON 格式錯誤 | 業務驗證帶自訂 `errors`（欄位為 camelCase） |
| `401` | 未登入 / session 過期 / 登入帳密錯誤 | `application/problem+json` |
| `403` | 已登入但角色 / 公司範圍不足 | 泛用訊息或具體 `detail` |
| `404` | 資源不存在或不可見 | |
| `409` | 唯一鍵衝突、狀態衝突、併發衝突 | |
| `500` | 未預期例外 | `detail = "An unexpected error occurred while processing the request."` |

### 1.6 角色與授權

角色 `role`：`USER`、`COMPANY_ADMIN`、`SYSTEM_ADMIN`。

| Policy | 允許 | 規則 |
| --- | --- | --- |
| 一般 `[Authorize]` | 已登入任一角色 | 首頁 / 下載 |
| `CompanyAdminScope` | `COMPANY_ADMIN`、`SYSTEM_ADMIN` | `SYSTEM_ADMIN` 可跨公司；`COMPANY_ADMIN` 限自己 `companyId`。帶 `companyId` query 且非本人公司 → `403` |
| `DocumentAccess` | 文件所屬公司管理者，或 `USER` 且其部門有該文件授權 | 首頁下載 |

- `COMPANY_ADMIN` 打清單端點若省略 `companyId`，自動過濾為自己公司。
- `COMPANY_ADMIN` 不能建立 / 改為 `SYSTEM_ADMIN` 帳號 → `403`。

### 1.7 資料型別

- 所有 id 為 `GUID` 字串。
- `createdAt` / `updatedAt` / `lastLoginAt`：`DateTimeOffset`，ISO 8601 含時區。
- `effectiveDate` / `expiredDate` / `publishDate`：`DateOnly`，`yyyy-MM-dd`。
- JSON 欄位一律 camelCase。

---

## 2. Auth　`/api/auth`

### `POST /api/auth/login`
匿名，**不需** `X-XSRF-TOKEN`。

```jsonc
// Request
{ "empno": "EMP001", "password": "..." }
```

- `200`：`{ "userId", "name", "role", "companyId", "companyName", "deptName" }`，並 `Set-Cookie` `isodocs.auth`（HttpOnly）＋ `isodocs.xsrf`。
- `400`：`empno` / `password` 未填。
- `401`：帳號不存在 / 停用 / 未設密碼 / 密碼錯誤 → `detail = "帳號或密碼錯誤。"`

### `POST /api/auth/logout`
已登入，需 `X-XSRF-TOKEN`。無 body。→ `204`（清除認證 cookie）。`401` 未登入。

### `GET /api/auth/me`
已登入。→ `200`：

```jsonc
{
  "userId", "empno", "name",
  "role": "USER | COMPANY_ADMIN | SYSTEM_ADMIN",
  "companyId", "companyName",
  "deptId", "deptName",
  "mustChangePassword": false
}
```

`companyName` / `deptName` 由後端 JOIN `companies` / `depts` 帶出，供前端頁首顯示。回應時刷新 `isodocs.xsrf`。`401` 未登入 / 已停用。

### `POST /api/auth/change-password`
已登入，需 `X-XSRF-TOKEN`。

```jsonc
{ "currentPassword": "...", "newPassword": "...", "newPasswordConfirmation": "..." }
```

- 規則：三欄必填；`newPasswordConfirmation` === `newPassword`；`newPassword` ≠ 目前密碼。（後端未驗長度 / 字元類別。）
- `204`：成功（重新簽發 cookie）。
- `400`：`errors.currentPassword` / `errors.newPassword` / `errors.newPasswordConfirmation`。密碼相關失敗一律 `400`，**不是** `401`。
- `401`：未登入。

---

## 3. Antiforgery　`/api/antiforgery`

### `GET /api/antiforgery/token`
匿名。→ `204`，`Set-Cookie: isodocs.xsrf=<token>`。送出非 GET 請求前用來確保有 token。

---

## 4. Health　`/api/health`

### `GET /api/health`
匿名。→ `200` `{ "status": "Healthy", "database": "Healthy" }` 或 `503` `{ "status": "Unhealthy", "database": "Unhealthy" }`。

---

## 5. 首頁 / 文件瀏覽（一般使用者）

路由 `[Authorize]`，任一已登入角色。

### `GET /api/documents/available`
目前登入者可閱讀的文件（依部門授權或身為公司管理者）。

- Query：`page`、`pageSize`、`keyword`（比對文件編號 / 名稱）。
- `200`：`PagedResult<AvailableDocumentResponse>`

```jsonc
{
  "documentId", "documentNo", "name", "companyName",
  "currentVersion": {
    "versionId", "version": "1.2",
    "effectiveDate": "2026-01-01",   // 可為 null
    "pageCount": 12                  // 可為 null
  }
}
```

- `401`：未登入 / 無部門。

### `GET /api/documents/{documentId}/versions/{versionId}/download`
下載主文件 PDF。授權 `DocumentAccess`。

- `200`：二進位串流，`Content-Type: application/pdf`，`Content-Disposition: attachment; filename=...`。
- `403`：無權限；或角色 `USER` 但該版本非 `PUBLISHED`（管理者可下載 `PUBLISHED` / `OBSOLETE`）。
- `404`：`"The document file has not been uploaded."`

### `GET /api/documents/{documentId}/versions/{versionId}/attachments/{attachmentId}/download`
下載附件檔。授權 `DocumentAccess`。`200` 串流（型別依副檔名）。`403` / `404`（`"The attachment file has not been uploaded."`）。

---

## 6. 管理 — 部門　`/api/depts`（`CompanyAdminScope`）

### `GET /api/depts`
Query：`companyId?`、`page`、`pageSize`。→ `200` `PagedResult<DeptResponse>`。`403`。

```jsonc
// DeptResponse
{ "id", "companyId", "name", "seq": 10, "createdAt", "updatedAt" }
```

### `POST /api/depts`

```jsonc
{ "companyId": "...", "name": "品保部", "seq": 10 }   // name ≤ 100；seq 選填
```

- `201`：`DeptResponse`（`Location` header）。
- `400`：`errors.companyId`（空 / 公司不存在）、`errors.name`（空 / 超長）。
- `403`：無權操作該公司。
- `409`：`"A department with the same name already exists in this company."`

### `GET /api/depts/{id}` → `200` `DeptResponse` / `403` / `404`

### `PUT /api/depts/{id}`

```jsonc
{ "name": "生產部", "seq": 20 }
```

→ `200` `DeptResponse` / `400` / `403` / `404` / `409`（同名）。

### `DELETE /api/depts/{id}`
→ `204` / `403` / `404` / `409`（`"The department cannot be deleted because it still has active users."`）。

---

## 7. 管理 — 使用者　`/api/users`（`CompanyAdminScope`）

### `GET /api/users`
Query：`companyId?`、`deptId?`、`keyword?`（empno / name / email）、`includeInactive`（預設 `false`）、`page`、`pageSize`。→ `200` `PagedResult<UserResponse>`。`400`（`deptId` 不屬於公司）。`403`。

```jsonc
// UserResponse
{
  "id", "companyId", "deptId", "empno", "name",
  "email": null, "role": "USER",
  "isActive": true, "mustChangePassword": false, "notifyEmailEnabled": true,
  "lastLoginAt": null, "createdAt", "updatedAt"
}
```

### `POST /api/users`

```jsonc
{
  "empno": "EMP100",         // ≤ 30
  "name": "Lobinda",           // ≤ 100
  "email": "a@b.com",        // 選填，email 格式，≤ 255
  "companyId": "...",
  "deptId": "...",           // 需屬於 companyId
  "role": "USER",            // USER | COMPANY_ADMIN | SYSTEM_ADMIN
  "password": "1",           // 選填；不給則帳號無法登入
  "passwordConfirmation": "1"// 有 password 時須一致；沒 password 時須為 null
}
```

- `201`：`UserResponse`（`isActive: true`、`notifyEmailEnabled: true`，`Location` header）。
- `400`：欄位驗證；`errors.deptId`（不屬於公司）；`errors.empno = ["The employee number is already in use."]`。
- `403`：無權操作該公司；或 `COMPANY_ADMIN` 建立 `SYSTEM_ADMIN` → `"Company administrators cannot create system administrator accounts."`

### `GET /api/users/{id}` → `200` `UserResponse` / `403` / `404`

### `PUT /api/users/{id}`

```jsonc
{
  "name": "王大明",
  "email": null,
  "deptId": "...",
  "role": "USER",
  "isActive": true,
  "notifyEmailEnabled": false
}
```

- 不能改 `empno`、不能在此改密碼。
- `200` `UserResponse` / `400` / `404`。
- `403`：無權；或 `COMPANY_ADMIN` 操作 / 改成 `SYSTEM_ADMIN` → `"Company administrators cannot create or modify system administrator accounts."`

### `DELETE /api/users/{id}`
軟刪（`isActive = false`）。→ `204` / `403` / `404`。

### `POST /api/users/{id}/reset-password`
無 body，需 `X-XSRF-TOKEN`。→ `200` `{ "temporaryPassword": "Ab3..." }`（12 碼），並設 `mustChangePassword = true`。`403` / `404`。

---

## 8. 管理 — ISO 文件主檔　`/api/documents`（`CompanyAdminScope`）

> 路由共用：`GET /api/documents/available` 屬第 5 節；`GET /api/documents/{id:guid}` 為本節。

### `GET /api/documents`
Query：`companyId?`、`keyword?`（documentNo / name）、`page`、`pageSize`。→ `200` `PagedResult<DocumentResponse>`。`403`。

```jsonc
// DocumentResponse
{ "id", "companyId", "documentNo", "name", "isActive": true,
  "createdBy", "createdAt", "updatedAt" }
```

### `POST /api/documents`

```jsonc
{ "companyId": "...", "documentNo": "HR-Gn-01", "name": "人事管理辦法" }
```

- `documentNo`：≤ 50，正規式 `^[A-Za-z0-9](?:[A-Za-z0-9-]{0,48}[A-Za-z0-9])?$`（英數與連字號，頭尾為英數），**同公司唯一**，不自動產生。
- `name`：≤ 255。
- `201`：`DocumentResponse`（`Location` header）。
- `400`：欄位驗證；`errors.documentNo = ["The document number is already in use for this company."]`。
- `403`。

### `GET /api/documents/{id}`
→ `200` `DocumentDetailResponse`：

```jsonc
{
  "id", "companyId", "documentNo", "name", "isActive": true,
  "createdBy", "createdAt", "updatedAt",
  "versions": [
    { "version": "2.0", "status": "PUBLISHED",
      "effectiveDate": "2026-01-01", "expiredDate": null }
  ]
}
```

`403` / `404`。

### `PUT /api/documents/{id}`
`{ "name": "..." }`（僅能改名稱，≤ 255）。→ `200` `DocumentResponse` / `400` / `403` / `404`。

### `DELETE /api/documents/{id}`
軟刪（`isActive = false`）。→ `204` / `403` / `404`。

---

## 9. 管理 — 文件版本　`/api/documents/{documentId}/versions`（`CompanyAdminScope`）

### `POST /api/documents/{documentId}/versions`
`multipart/form-data`，需 `X-XSRF-TOKEN`。上傳並發佈新版本（**檔案必填**，建立即 `PUBLISHED`，先前 `PUBLISHED` 版本轉 `OBSOLETE` 並設 `expiredDate = effectiveDate`）。

| 欄位 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `changeType` | text | ✔ | `MAJOR`（x+1.0）或 `MINOR`（x.y+1） |
| `effectiveDate` | text `yyyy-MM-dd` | ✔ | 不得早於發佈日（今天 UTC） |
| `pageCount` | text (int) | ✘ | 有給須 > 0 |
| `memo` | text | ✘ | 版本備註 |
| `file` | file | ✔ | 副檔名 `.pdf`，內容前置須為 `%PDF-`，檔名 ≤ 255 |

- `201`：`{ "versionId", "version": "1.1", "status": "PUBLISHED" }`（無有效 `Location` header，直接用 body）。
- `400`：`errors.changeType` / `effectiveDate` / `pageCount` / `file`；或 `errors.file = ["The document file content is not a valid PDF."]`。
- `403`：非同公司。
- `404`：文件不存在 / 公司不存在。
- `409`：文件已停用（`"已停用的文件無法新增版本。"`）；併發發佈（`"另一個版本已同時發佈，請重新載入文件後再試一次。"`）。

### `DELETE /api/documents/{documentId}/versions/{versionId}`（`CompanyAdminScope`）
需 `X-XSRF-TOKEN`。硬刪除**草稿版本**。

- `204`：成功（有主檔則搬 trash，記 audit `DELETE_DOCUMENT_VERSION`）。
- `404`：版本不存在，或不屬於該文件。
- `409`：版本非 `DRAFT`（`"只有草稿版本可以刪除。"`）；或該版本仍有附件（`"請先移除此版本的附件，再刪除版本。"`）。

---

## 10. 管理 — 附件

### `GET /api/documents/{documentId}/versions/{versionId}/attachments`（`CompanyAdminScope`）
→ `200` **陣列**（非分頁）：

```jsonc
[ { "attachmentId", "attachmentNo": "ATT-01", "name": "請假申請表", "hasFile": true } ]
```

`403` / `404`（版本不存在）。

### `POST /api/documents/{documentId}/versions/{versionId}/attachments`（`CompanyAdminScope`）
`multipart/form-data`，需 `X-XSRF-TOKEN`。批次新增（**全有全無**：任一項失敗整批 rollback、不留檔）。

| 欄位 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `items[i].attachmentNo` | text | ✔ | ≤ 50；同版本唯一、同批次不可重複 |
| `items[i].name` | text | ✔ | ≤ 255 |
| `items[i].file` | file | ✘ | 不給 → 僅建中繼資料。副檔名限 `.jpg .jpeg .png .pdf .doc .docx .xls .xlsx .odt .ods`，檔名 ≤ 255 |

（`i` 從 0 連續。至少 1 項。）

- `201`：`{ "created": [ { "attachmentId", "attachmentNo", "hasFile" } ] }`。
- `400`：`errors["items[0].attachmentNo"]` 等欄位錯；或 `errors.attachmentNo`（請求內重複 / 與現有版本衝突），例 `["附件編號「ATT-01」已在此版本使用。"]`。
- `403` / `404`。

### `PUT /api/attachments/{id}/file`（`CompanyAdminScope`）
`multipart/form-data`，需 `X-XSRF-TOKEN`。為「僅中繼資料」的附件（`hasFile: false`）補上實體檔案。

| 欄位 | 型別 | 必填 | 說明 |
| --- | --- | --- | --- |
| `file` | file | ✔ | 副檔名限 `.jpg .jpeg .png .pdf .doc .docx .xls .xlsx .odt .ods`，檔名 ≤ 255。與 `POST` 一致，不驗 magic bytes；`contentType` 依副檔名推導 |

- `200`：`{ "attachmentId", "hasFile": true }`。寫入 `file_key` / `original_file_name` / `content_type` / `file_size` / `checksum` 五欄（全有全無）。
- `400`：`errors.file`（未附檔 / 副檔名不允許 / 檔名超長）。
- `403` / `404`（附件不存在）。
- `409`：附件已有檔案（`"此附件已經有檔案，無法重複補檔。"`）。不限制附件所屬版本狀態（PUBLISHED / OBSOLETE / DRAFT 皆可補檔，比照 `DELETE`）。

### `DELETE /api/attachments/{id}`（`CompanyAdminScope`）
需 `X-XSRF-TOKEN`。→ `204`（實體檔移到 trash）/ `403` / `404`。

---

## 11. 管理 — 文件部門授權　`/api/documents/{documentId}/dept-permissions`（`CompanyAdminScope`）

### `GET`
→ `200` `{ "deptIds": ["...", "..."] }`（已排序）。`403` / `404`。

### `PUT`
以完整清單覆寫（送出的即為最終狀態，後端算增刪）。

```jsonc
{ "deptIds": ["...", "..."] }   // 必填、非空；每個 deptId 須存在且屬於文件所屬公司
```

- `200`：`{ "deptIds": [...] }`（套用後）。
- `400`：`errors.deptIds`（空清單 / 有 deptId 不屬於該公司）。
- `403` / `404`。

### `GET /api/documents/permission-matrix`（`CompanyAdminScope`）
文件 × 部門權限矩陣（清單型，供權限維護頁一次載入部門選項 + 分頁文件列 + 每份文件的授權部門）。

Query：`companyId?`、`companyCode?`（`companies.code`；僅在未帶 `companyId` 時採用，會先解析為 `companyId`，不分大小寫；查不到則回空結果）、`keyword?`（比對 documentNo / name，不分大小寫）、`page`（預設 1）、`pageSize`（預設 20，上限 100）。

**授權範圍一律由登入身分決定**，不信任 query 傳入的公司參數：
- `COMPANY_ADMIN`：強制自己公司，帶的 `companyId` / `companyCode` 直接忽略（**不回 403**）。
- `SYSTEM_ADMIN`：可用 `companyId` 或 `companyCode` 篩選，不帶則查全部公司。

→ `200`：

```jsonc
{
  "departments": [ { "id", "name", "seq": 1 } ],          // 範圍內公司的部門，依 company_id、seq
  "items": [
    {
      "documentId", "documentCode", "documentName",
      "version": "1.0",                                    // 代表版本；無版本時為 null
      "companyId",
      "status": {
        "mainDocument": { "code": "NORMAL|ERROR|MISSING", "label", "hasError": false },
        "attachment":   { "code": "NORMAL|ERROR|MISSING|NONE", "label", "hasError": false },
        "effective":    { "code": "DRAFT|SCHEDULED|EFFECTIVE|EXPIRED|CANCELLED", "label" }
      },
      "departmentIds": [ "dept-uuid", ... ]                // 無授權時為 []，不會是 null
    }
  ],
  "pagination": { "page": 1, "pageSize": 20, "totalCount": 135, "totalPages": 7 }
}
```

- `status` 由後端計算（見下）。`hasError` = `code == "ERROR"`。
- **代表版本**：`PUBLISHED` → 否則 `DRAFT` → 都沒有則視同空 `DRAFT`。
- `mainDocument` / `attachment` 的 `ERROR` 由 `IDocumentStorage.ExistsAsync` 對**當頁**文件即時檢查（實體檔遺失），不做背景快取；`attachment` 的 `ERROR` 優先於 `MISSING`。
- `effective`：`is_active=false` → `CANCELLED`（最優先）；`PUBLISHED` 且 `effectiveDate` 未到 → `SCHEDULED`，已到 → `EFFECTIVE`；其餘 → `DRAFT`。`EXPIRED` 目前不會產生。
- `403`：僅在無法判斷公司範圍時（理論上不會發生，policy 已限縮角色）。

### `PUT /api/documents/permission-matrix`（`CompanyAdminScope`）
矩陣**批次**儲存：一次送出多份文件各自完整的 `departmentIds`（整批覆蓋，非 diff 語意的 request——但後端內部仍用 diff 方式寫入，見下）。跟上面單文件的 `PUT .../dept-permissions` **並存、互不取代**：矩陣頁用這支，未來若有單文件詳情頁要單獨改權限，用那支。

```jsonc
// Request
{
  "items": [
    { "documentId": "7c263c91-...", "departmentIds": ["e91fa832-...", "8a032317-..."] },
    { "documentId": "a2b3c4d5-...", "departmentIds": [] }   // 空陣列 = 清空這份文件的所有部門權限
  ]
}
```

- `items`：至少 1 筆；`documentId` 不可重複。
- 單一 item 的 `departmentIds`：可為 `[]`（清空）；不可有重複 UUID；不可為 `null`（省略時視為 `[]`）。

**驗證（全部通過才寫入，任一步失敗 → 整批 400/403/404，不寫入任何一筆，全有全無）**：
1. 格式：`items` 空 / `documentId` 重複 / 某 item 的 `departmentIds` 重複 → `400`。
2. 每個 `documentId` 必須存在 → 否則 `404`。
3. 每份文件須在使用者的公司範圍：`COMPANY_ADMIN` 限自己公司；`SYSTEM_ADMIN` 不限公司 → 不符 `403`。
4. 每個 item 的 `departmentIds` 必須全部存在且屬於**該文件所屬公司** → 不符 `400`，`errors["items[<i>].departmentIds"]`。

**寫入方式**：對每個 item 各自算 `toAdd`（requested − current）/`toRemove`（current − requested），只 `INSERT`/`DELETE` 差異的部分（未變動的權限列保留原本 `granted_by` / `created_at`），所有 item 在同一個 transaction 內一次提交。整批完全無變動時不開 transaction、不寫 audit。

**Audit**：只對「實際有變動」的文件各寫一筆既有的 `document_dept_permissions` 異動紀錄（沿用單文件端點同一個 action，不新增名稱）。

→ `200`：

```jsonc
{
  "items": [
    {
      "documentId", "documentCode",
      "departmentIds": ["e91fa832-...", "8a032317-..."],
      "addedDepartmentIds": ["8a032317-..."],
      "removedDepartmentIds": []
    }
  ],
  "updatedBy": { "id", "name" },
  "updatedAt"   // 這次操作當下時間；非持久化欄位（document_dept_permissions 沒有 updated_at）
}
```

- `400` / `403` / `404`：見上方驗證規則。

---

## 12. 管理 — 公司備份　`/api/companies/{companyId}/backup`（`CompanyAdminScope`）

### `GET /api/companies/{companyId}/backup`
下載該公司所有 `PUBLISHED` 版本主文件與附件的 zip。

- `200`：`Content-Type: application/zip`，`Content-Disposition: attachment; filename*=<公司名>_backup_yyyyMMdd.zip`。
  - zip 結構：`<documentNo>/v<version>/main/<檔名>`、`<documentNo>/v<version>/attachments/<attachmentNo>_<檔名>`。
- `403`：非本人公司。
- `404`：公司不存在。

以 `blob` 接收，不要當 JSON。

---

## 13. 端點速查

| Method | Path | 授權 | 成功碼 |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | 匿名 | 200 |
| POST | `/api/auth/logout` | 已登入 + CSRF | 204 |
| GET | `/api/auth/me` | 已登入 | 200 |
| POST | `/api/auth/change-password` | 已登入 + CSRF | 204 |
| GET | `/api/antiforgery/token` | 匿名 | 204 |
| GET | `/api/health` | 匿名 | 200 / 503 |
| GET | `/api/documents/available` | 已登入 | 200 |
| GET | `/api/documents/{d}/versions/{v}/download` | DocumentAccess | 200（檔案） |
| GET | `/api/documents/{d}/versions/{v}/attachments/{a}/download` | DocumentAccess | 200（檔案） |
| GET / POST | `/api/depts` | CompanyAdminScope (+CSRF) | 200 / 201 |
| GET / PUT / DELETE | `/api/depts/{id}` | CompanyAdminScope (+CSRF) | 200 / 200 / 204 |
| GET / POST | `/api/users` | CompanyAdminScope (+CSRF) | 200 / 201 |
| GET / PUT / DELETE | `/api/users/{id}` | CompanyAdminScope (+CSRF) | 200 / 200 / 204 |
| POST | `/api/users/{id}/reset-password` | CompanyAdminScope + CSRF | 200 |
| GET / POST | `/api/documents` | CompanyAdminScope (+CSRF) | 200 / 201 |
| GET / PUT / DELETE | `/api/documents/{id}` | CompanyAdminScope (+CSRF) | 200 / 200 / 204 |
| POST | `/api/documents/{documentId}/versions` | CompanyAdminScope + CSRF | 201（multipart） |
| DELETE | `/api/documents/{documentId}/versions/{versionId}` | CompanyAdminScope + CSRF | 204（僅 DRAFT） |
| GET / POST | `/api/documents/{d}/versions/{v}/attachments` | CompanyAdminScope (+CSRF) | 200 / 201 |
| PUT | `/api/attachments/{id}/file` | CompanyAdminScope + CSRF | 200（multipart 補檔） |
| DELETE | `/api/attachments/{id}` | CompanyAdminScope + CSRF | 204 |
| GET / PUT | `/api/documents/{documentId}/dept-permissions` | CompanyAdminScope (+CSRF) | 200 |
| GET | `/api/documents/permission-matrix` | CompanyAdminScope | 200（權限矩陣） |
| PUT | `/api/documents/permission-matrix` | CompanyAdminScope + CSRF | 200（批次儲存） |
| GET | `/api/companies/{companyId}/backup` | CompanyAdminScope | 200（zip） |

---

## 14. 匯入 Postman

1. Postman → **Import** → 選 `ISO-Docs-API.postman_collection.json` 與 `ISO-Docs-API.postman_environment.json`。
2. 右上角環境切到 **ISO Docs — Local**，確認 `baseUrl`（預設 `https://localhost:7167`）。
3. Settings → General → 關閉 **SSL certificate verification**（本機自簽憑證）。
4. 先跑 **Auth → Login**（填 `empno` / `password` 環境變數）。之後：
   - Collection 內有 pre-request script：非 GET 請求會自動從 cookie jar 取 `isodocs.xsrf` 塞進 `X-XSRF-TOKEN`。
   - 認證 cookie `isodocs.auth` 由 Postman cookie jar 自動帶。
   - Login / Create 類請求的 test script 會把 `companyId` / `deptId` / `documentId` / `versionId` / `userId` / `attachmentId` 寫回環境變數，後續請求直接引用。
5. 版本 / 附件上傳請求為 `form-data`，`file` 欄位需自行在 Postman 選檔。

### 另一種方式：直接匯入 OpenAPI

後端在 Development 環境會輸出自動產生的 OpenAPI 文件（`AddOpenApi()` / `MapOpenApi()`）：

```
GET https://localhost:7167/openapi/v1.json
```

Postman → Import → Link，貼上該網址即可（永遠與程式碼同步，但沒有本集合的 CSRF pre-request script 與範例 body / 變數串接）。
