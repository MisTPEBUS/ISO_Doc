# ISO 文件管理系統 — Architecture

## 0. 範圍與假設

本文件對應以下 5 個功能模組：

1. 首頁（文件瀏覽與下載）
2. 部門維護
3. 使用者維護
4. ISO 文件維護（主文 / 附件）
5. 權限維護（文件 × 部門）
6. ISO 文件備份（依公司打包下載）

另含一個非業務的基礎設施端點：健康檢查（`GET /api/health`），見 §4.7。

**明確排除於本版本之外**（若未來需要，屬另一輪設計）：

- 審核流程（Draft → Review → Approve → Publish 的多人審核）。本版本上傳版本即視為可發布狀態，無審核關卡。
- 站內通知 / Email 通知。
- SignalR / SSE 即時推播。
- LDAP / AD / SSO 登入整合，本版本僅本地帳密。

**本版本納入之建立模型**：主文版本與附件皆支援「先建立中繼資料、稍後補檔」；附件為選配（版本可 0 附件）；附件上傳單次可多檔。詳見 §4.4 與 SPEC.md 第 3、4、5 節。

以上為明確假設，非最終定案；若與實際需求不符，需在動工前修正本文件與 SPEC.md。

---

## 1. 技術棧

| 項目          | 選擇                                               | 版本                                   |
| ------------- | -------------------------------------------------- | -------------------------------------- |
| Runtime       | .NET                                               | 8 (LTS 至 2026-11，屆時評估升 .NET 10) |
| Web Framework | ASP.NET Core Web API (Controllers)                 | —                                      |
| ORM           | Entity Framework Core + Npgsql                     | —                                      |
| Database      | PostgreSQL                                         | 16+                                    |
| Validation    | FluentValidation                                   | —                                      |
| Auth          | Cookie-based (同源) + Antiforgery                  | —                                      |
| Log           | Serilog（stdout structured）                       | —                                      |
| 前端          | React + TypeScript + Vite（靜態檔，由 nginx 服務） | —                                      |
| 部署          | Docker Compose，單一 instance，NAS bind mount      | —                                      |

**單一 instance 前提**：背景工作（備份、GC）以 `BackgroundService` in-process 執行，不做水平擴展。若未來需要多 instance，需重新設計背景工作協調機制。

---

## 2. 專案結構

```
[後端專案架構]
IsoDocs.sln
    Program.cs
    appsettings.json
    appsettings.Development.json

    Common/
      ApiProblem.cs
      PagedResult.cs
      Result.cs                    # Success / NotFound / Conflict / Forbidden / Unauthorized / ValidationFailed
      DomainException.cs
      ExceptionHandlingMiddleware.cs

    Data/
      IsoDbContext.cs
      Entities/
        Company.cs
        Dept.cs
        User.cs
        Document.cs
        DocumentVersion.cs
        Attachment.cs
        DocumentDeptPermission.cs
        AuditLog.cs
      Configurations/              # 一個 entity 一個 IEntityTypeConfiguration<T>
      Migrations/

    Features/
      Auth/
        AuthController.cs
        AuthService.cs
        Dtos/
      Home/
        DocumentsBrowseController.cs
        DocumentsBrowseService.cs
        Dtos/
      Depts/
        DeptsController.cs
        DeptService.cs
        Dtos/
        Validators/
      Companies/
        CompaniesController.cs
        CompanyService.cs
        Dtos/
      Users/
        UsersController.cs
        UserService.cs
        Dtos/
        Validators/
      Documents/
        DocumentsController.cs
        DocumentVersionsController.cs
        AttachmentsController.cs
        DocumentService.cs
        DocumentVersionService.cs
        AttachmentService.cs
        Dtos/
        Validators/
      Permissions/
        DocumentPermissionsController.cs
        DocumentPermissionService.cs
        Dtos/
      Backup/
        BackupController.cs
        BackupService.cs
      Health/
        HealthController.cs
        HealthService.cs
        Dtos/
      AuditLogs/
        AuditLogService.cs        # 內部服務，無獨立 controller（v1 不開放查詢 UI）

    Storage/
      IDocumentStorage.cs
      LocalFileStorage.cs
      StorageKeyBuilder.cs
      StoragePathGuard.cs
      StorageOptions.cs
      StorageHealthCheck.cs

    Security/
      CurrentUser.cs
      Authorization/
        Policies.cs
        DocumentAccessRequirement.cs
        DocumentAccessHandler.cs
        CompanyScopeRequirement.cs
        CompanyScopeHandler.cs

    Jobs/
      TrashGcJob.cs

tests/
  IsoDocs.Tests/
    Features/
    Storage/
```

**分層原則**：Controller 只做 model binding、呼叫 Service、回應結果。Business Logic 全部在 Service。Service 不直接操作 `HttpContext`，需要當前使用者資訊時透過 `ICurrentUser` 注入。Storage 的實體路徑細節（`Path.Combine`、guard）只存在 `Storage/` 資料夾內，其他地方一律透過 `objectKey` 字串操作，不得自行組路徑。

---

## 3. 角色與權限模型

三種角色，對應 `users.role`：

| Role            | Scope    | 可執行操作                                                              |
| --------------- | -------- | ----------------------------------------------------------------------- |
| `USER`          | 自己部門 | 首頁瀏覽 / 下載有權限的文件                                             |
| `COMPANY_ADMIN` | 自己公司 | 上述 + 部門維護、使用者維護、ISO 文件維護、權限維護、備份，範圍限本公司 |
| `SYSTEM_ADMIN`  | 全部     | 上述 + 跨公司操作                                                       |

**授權策略統一在 `Security/Authorization/` 定義**，不散落在各 Controller 用 `if (user.Role == ...)` 判斷。兩個 requirement，皆為 **resource-based，handler 不注入 `IHttpContextAccessor`、不讀 route/query**：

- `CompanyScopeRequirement` + `CompanyScopeResource(Guid TargetCompanyId)`：驗證操作對象（dept / user / document）所屬 `company_id` 是否在使用者可管理範圍內。`SYSTEM_ADMIN` 放行任一；`COMPANY_ADMIN` 僅限自身 `companyId`。
- `DocumentAccessRequirement` + `DocumentAccessResource(Guid DocumentId)`：驗證使用者所屬部門是否在該文件的 `document_dept_permissions` 內（首頁瀏覽 / 下載用）。

`[Authorize(Policy = Policies.CompanyAdminScope)]` attribute 只做**角色閘門**（role ∈ {`COMPANY_ADMIN`, `SYSTEM_ADMIN`}）。公司租戶比對由 **Controller** 解析 target company id 後呼叫 `IAuthorizationService.AuthorizeAsync(User, new CompanyScopeResource(id), Policies.CompanyAdminScope)`：list / create 從 route/query/body 取；by-id 操作由 Service 先載入實體取得 `company_id` 再回呼授權。細節見 `SPEC.md` §4.5。

```csharp
[Authorize(Policy = Policies.CompanyAdminScope)]   // 角色閘門
[HttpPost]
public async Task<IActionResult> Create(CreateDeptRequest request)
{
    await _authz.AuthorizeOrThrow(User, new CompanyScopeResource(request.CompanyId));
    // ...
}
```

### 認證（Cookie + Antiforgery）

- Cookie scheme 名 `isodocs.auth`（HttpOnly、Secure、SameSite=Lax、`ExpireTimeSpan = 8h`、`SlidingExpiration`、session cookie）。`OnRedirectToLogin` / `OnRedirectToAccessDenied` 覆寫為回 401 / 403 JSON。
- 全域 `AutoValidateAntiforgeryToken`；`login` 以 `[IgnoreAntiforgeryToken]` 豁免，`logout` / `change-password` 需帶 `X-XSRF-TOKEN`。CSRF request token 由 `GET /api/antiforgery/token`、登入成功與 `GET /api/auth/me` 寫入非 HttpOnly 的 `isodocs.xsrf` cookie。
- 完整設定表見 `SPEC.md` §4.5。

---

## 4. 各模組設計

### 4.1 首頁（文件瀏覽 / 下載）

- 資料來源：`document_dept_permissions` join 使用者部門，僅回傳每份文件**目前 Published 版本**。
- 下載主文與下載附件是兩個獨立 endpoint，都需經過 `DocumentAccessRequirement` 授權檢查，不得用可猜測 URL 直接存取檔案。
- 下載一律記 `audit_logs`（action = `DOWNLOAD_DOCUMENT` / `DOWNLOAD_ATTACHMENT`）。

### 4.2 部門維護

標準 CRUD，範圍依角色限制在所屬公司。刪除為軟刪除概念上的限制：部門底下若仍有 `is_active = true` 的使用者，禁止刪除（回傳 409），避免使用者變成孤兒資料。

### 4.3 使用者維護

CRUD + 密碼重設。刪除一律是 `is_active = false`（軟刪除），理由：`audit_logs.user_id`、`document_versions.created_by` 等多處外鍵依賴使用者存在，實體刪除會破壞歷史紀錄的可追溯性。`COMPANY_ADMIN` 不可建立 `SYSTEM_ADMIN` 帳號，此規則在 Service 層以 domain rule 驗證，不是前端擋。

### 4.4 ISO 文件維護（主文 / 附件）

- `Document` 為靜態識別（編號、名稱、所屬公司），實際內容在 `DocumentVersion`。
- 主文版本與附件皆可「先建中繼資料、後補檔案」：
  - `DocumentVersion` 可先以 `Draft` 建立（無主文檔），補檔後轉 `Published`。
  - `Attachment` 可先以 `attachment_no` + `name` 建立（檔案欄位為 NULL），補檔後即帶檔。
- 上傳新版本 = 新增一筆 `DocumentVersion`；帶檔時一步標記 `Published`，不帶檔時為 `Draft`。版本轉入 `Published` 的同一 transaction 內，將該文件先前的 `Published` 版本轉為 `Obsolete`。
- 附件為選配：一個版本可有 0..N 個附件。單次 API 可上傳多個附件，批次全有全無。
- 附件從屬於特定版本（`attachments.document_version_id`），换版時需重新上傳或延用（由 Service 決定是否複製檔案）。
- `Draft` 的版本與未補檔的附件不出現在首頁 / 下載 / 備份。
- 檔案寫入採 immutable：`objectKey` 一旦寫入不重算、不覆蓋，`File.Move(overwrite: false)` 強制。
- 主文限 `.pdf`；附件依原 model 限制的副檔名清單。

### 4.5 權限維護

- 一次性覆寫語意：`PUT /api/documents/{id}/dept-permissions` 帶完整 `deptIds[]`，Service 內以 diff 方式新增/刪除列，非逐筆 add/remove API。
- 每次變更寫入 `audit_logs`（`old_value` / `new_value` 存 dept id 陣列）。

### 4.6 ISO 文件備份

- 範圍：`SYSTEM_ADMIN` 可備份任一公司；`COMPANY_ADMIN` 僅能備份自己公司。
- 實作為**同步 streaming**：直接在 HTTP response 上開 `ZipArchive`，逐筆從 NAS 讀檔寫入 zip entry，不整批讀進記憶體。因為備份範圍限定單一公司（非全系統），資料量可控；若未來資料量顯著成長，才升級為背景 Job + 完成後下載的非同步模式。
- 備份動作寫入 `audit_logs`（`action = 'BACKUP_COMPANY_DOCUMENTS'`），比照舊系統 `readme.txt` 的精神記錄操作者與時間。

### 4.7 健康檢查（Health）

非業務功能，供容器編排 / 監控探測使用。

- `GET /api/health`，`[AllowAnonymous]`，不需登入、不寫 `audit_logs`。
- 檢查項目：API 進程存活 + PostgreSQL 可連線（`DbContext.Database.CanConnectAsync`）。
- 回應：DB 正常回 `200`，DB 不可用回 `503`；body 兩種狀況皆為 `HealthResponse { status, database }`（非 `ProblemDetails`，此端點刻意不套用共通錯誤格式，方便探測工具解析）。
- DB 探測失敗只記 `LogWarning`，不拋例外、不進全域例外處理。
- 放在 `Features/Health/`，維持 Controller → `IHealthService` 分層；`HealthService` 可直接注入 `IsoDbContext`（此模組不經 Feature Service 以外的規則限制，屬唯一例外）。
- `StorageHealthCheck` 專供啟動時 NAS 掛載 fail-fast，不併入此公開端點。

---

## 5. API 總覽

| 模組        | Method | Path                                                                                   | 說明                                                   |
| ----------- | ------ | -------------------------------------------------------------------------------------- | ------------------------------------------------------ |
| Auth        | POST   | `/api/auth/login`                                                                      | 登入                                                   |
| Auth        | POST   | `/api/auth/logout`                                                                     | 登出                                                   |
| Auth        | GET    | `/api/auth/me`                                                                         | 目前使用者資訊                                         |
| Auth        | POST   | `/api/auth/change-password`                                                            | 變更密碼                                               |
| Auth        | GET    | `/api/antiforgery/token`                                                               | 發放 SPA antiforgery token                             |
| Home        | GET    | `/api/documents/available`                                                             | 有權限瀏覽的文件清單（分頁）                           |
| Home        | GET    | `/api/documents/{documentId}/versions/{versionId}/download`                            | 下載主文                                               |
| Home        | GET    | `/api/documents/{documentId}/versions/{versionId}/attachments/{attachmentId}/download` | 下載附件                                               |
| Companies   | GET    | `/api/companies`                                                                       | 搜尋公司（限 SYSTEM_ADMIN）                            |
| Depts       | GET    | `/api/depts`                                                                           | 列表（依角色自動限縮公司範圍）                         |
| Depts       | POST   | `/api/depts`                                                                           | 新增                                                   |
| Depts       | GET    | `/api/depts/{id}`                                                                      | 單筆                                                   |
| Depts       | PUT    | `/api/depts/{id}`                                                                      | 更新                                                   |
| Depts       | DELETE | `/api/depts/{id}`                                                                      | 刪除（有使用者則 409）                                 |
| Users       | GET    | `/api/users`                                                                           | 列表                                                   |
| Users       | POST   | `/api/users`                                                                           | 新增                                                   |
| Users       | GET    | `/api/users/{id}`                                                                      | 單筆                                                   |
| Users       | PUT    | `/api/users/{id}`                                                                      | 更新                                                   |
| Users       | DELETE | `/api/users/{id}`                                                                      | 停用（軟刪除）                                         |
| Users       | POST   | `/api/users/{id}/reset-password`                                                       | 重設密碼                                               |
| Documents   | GET    | `/api/documents`                                                                       | 文件列表（管理用，含所有狀態）                         |
| Documents   | POST   | `/api/documents`                                                                       | 新增文件（僅建立主檔，不含版本）                       |
| Documents   | GET    | `/api/documents/{id}`                                                                  | 單筆（含版本歷程）                                     |
| Documents   | PUT    | `/api/documents/{id}`                                                                  | 更新文件基本資料                                       |
| Documents   | DELETE | `/api/documents/{id}`                                                                  | 停用文件                                               |
| Versions    | POST   | `/api/documents/{documentId}/versions`                                                 | 建立新版本（multipart；`file` 選填，省略則為 `Draft`） |
| Versions    | PUT    | `/api/documents/{documentId}/versions/{versionId}/file`                                | 為 `Draft` 版本補主文檔，轉 `Published`                |
| Versions    | GET    | `/api/documents/{documentId}/versions/{versionId}`                                     | 單一版本詳情                                           |
| Attachments | GET    | `/api/documents/{documentId}/versions/{versionId}/attachments`                         | 附件列表                                               |
| Attachments | POST   | `/api/documents/{documentId}/versions/{versionId}/attachments`                         | 批次建立／上傳附件（multipart，1..N 筆，全有全無）     |
| Attachments | PUT    | `/api/attachments/{id}/file`                                                           | 為未補檔的附件上傳檔案                                 |
| Attachments | DELETE | `/api/attachments/{id}`                                                                | 刪除附件（限同一版本尚可編輯時）                       |
| Permissions | GET    | `/api/documents/{documentId}/dept-permissions`                                         | 目前可觀看部門清單                                     |
| Permissions | PUT    | `/api/documents/{documentId}/dept-permissions`                                         | 覆寫可觀看部門清單                                     |
| Backup      | GET    | `/api/companies/{companyId}/backup`                                                    | 觸發備份並串流回傳 zip                                 |
| Health      | GET    | `/api/health`                                                                          | 匿名檢查 API 與 PostgreSQL 可用狀態                    |

錯誤格式統一採 `ProblemDetails`（`AddProblemDetails()`），前端只需處理單一錯誤 shape。唯一例外為 `GET /api/health`（見 §4.7），其 200 / 503 皆回固定的 `{ status, database }`。

---

## 6. 跨模組共通機制

- **Authorization**：全部透過 policy-based handler，不在 Controller 內寫角色判斷式。
- **Validation**：FluentValidation，validator 與 feature 放在同一資料夾。
- **File Storage**：`IDocumentStorage` 抽象，v1 實作 `LocalFileStorage`（NAS bind mount），未來可替換不影響 Service 層。`StorageKeyBuilder` 是唯一產生 `objectKey` 的入口。
- **Audit Log**：新增 / 修改 / 刪除 / 下載 / 備份等動作於 Service 層統一寫入，透過 `IAuditLogWriter`，避免各處手動組字串。
- **並發控制**：`document_versions` 狀態切換（尤其 Publish）使用 DB transaction + partial unique index（同文件同時只能有一筆 `Published`）雙重保護。
- **時間**：全部欄位使用 `timestamptz`，儲存 UTC，前端顯示轉 `Asia/Taipei`。

---

## 7. 非目標與後續擴充點（明確標註，避免誤植入本輪程式碼）

- 審核流程：`document_versions.status` 目前僅 `Draft` / `Published` / `Obsolete`（本輪 `Draft` 語意為「已建立、主文檔待補」，非審核關卡），若加審核需擴充狀態值與 `ApprovalHistory` 表，屬另一輪工作。
- 通知：`notifications` 相關表本輪不建立。
- 即時推播：本輪無 SignalR / SSE。
- 若需要 email 通知或即時審核提醒，於功能確認後另行設計，不應在本輪 Codex 產出中預先建立空殼程式碼。
