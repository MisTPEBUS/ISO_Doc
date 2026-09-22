# AGENTS.md — Backend (ASP.NET Core 8)

> 適用範圍：`/backend` 目錄下所有檔案。與 root `AGENTS.md` 疊加生效，衝突時以 `SPEC.md` 為準。

## 技術棧（不得替換或降級）

- .NET 8, C#
- EF Core + Npgsql（PostgreSQL 16+）
- FluentValidation
- Cookie-based Auth（HttpOnly, Secure, SameSite=Lax）+ Antiforgery
- Serilog（stdout structured log）

## 專案結構（新檔案必須放對位置，不得自創資料夾）

```
src/IsoDocs.Api/
  Program.cs
  Common/            # ApiProblem, PagedResult, Result, DomainException, ExceptionHandlingMiddleware
  Data/
    IsoDbContext.cs
    Entities/        # 純資料模型，不含業務邏輯
    Configurations/   # 一個 entity 一個 IEntityTypeConfiguration<T>
    Migrations/
  Features/
    <FeatureName>/
      <FeatureName>Controller.cs
      <FeatureName>Service.cs
      Dtos/
      Validators/
  Storage/           # IDocumentStorage, LocalFileStorage, StorageKeyBuilder, StoragePathGuard, StorageOptions
  Security/
    CurrentUser.cs
    Authorization/    # Policies, *Requirement, *Handler
  Jobs/               # BackgroundService，例如 TrashGcJob
tests/IsoDocs.Tests/
```

新功能一律放進 `Features/<FeatureName>/`，不得散落在 `Controllers/` `Services/` 這種扁平資料夾（本專案不採此結構）。

## 分層鐵則（違反視為 bug，不是風格問題）

- **Controller 只做 model binding、呼叫 Service、回傳結果。** Controller 內不得出現 `_db`、`DbContext`、任何 `System.IO` 檔案路徑操作、任何 `if (user.Role == ...)` 的手動角色判斷。
- **Business Logic 全部在 Service。** Service 不得直接讀 `HttpContext`；需要當前使用者資訊一律透過 `ICurrentUser` 注入。
- **檔案路徑操作只能發生在 `Storage/` 資料夾內。** 其他地方一律透過不透明的 `objectKey` 字串操作，禁止自行 `Path.Combine` 組出實體路徑。`StorageKeyBuilder` 是產生 `objectKey` 的唯一入口。
- **Authorization 一律用 policy-based handler**（`Security/Authorization/`），不得在 Controller 或 Service 內寫角色判斷式。新增授權規則時新增對應的 `*Requirement` + `*Handler`，不得直接在既有 handler 塞 if-else 硬加特例。
- **Validation 用 FluentValidation**，validator 放在對應 Feature 的 `Validators/` 下，不使用 DataAnnotations 承擔業務規則驗證。

## Domain 規則（實作時必須強制於 Service 層，詳見 SPEC.md 第 4 節）

- 帶ISO管理程序檔案建立新版本時直接進入 `PUBLISHED` 狀態（本輪無審核關卡），並在同一 transaction 內將該文件先前的 `PUBLISHED` 版本轉為 `OBSOLETE`；不帶ISO管理程序檔案（預先建立）時版本為 `DRAFT`，之後補檔才轉 `PUBLISHED` 並套用同一轉 `OBSOLETE` 規則。`DRAFT` 版本不出現在首頁 / 下載 / 備份。
- `objectKey` 寫入後永不重算；寫入採 `FileMode.CreateNew`（不可覆蓋）；刪除一律 move 到 `trash/{yyyyMM}/`，不得 `File.Delete`。
- 表單及附件身份掛在 `document_id` 底下，表單及附件版本歷程完全獨立於ISO管理程序版本；ISO管理程序或表單及附件任一方改版不得改動另一方狀態。
- 本輪表單及附件版本只支援帶檔建立並直接進入 `PUBLISHED`；同一表單及附件先前的 `PUBLISHED` 在同一 transaction 內轉為 `OBSOLETE`，且只補 `expired_date`，不得覆寫原 `publish_date` / `effective_date`。
- 文件與表單及附件批次匯入採兩階段、逐筆 continue-on-error；第二階段只依既有 `document_no` 關聯，不得自動建立文件。
- `COMPANY_ADMIN` 不可建立或修改 `role = SYSTEM_ADMIN` 的使用者，此規則必須在 Service 層擋，不得只靠前端隱藏按鈕。
- 使用者刪除一律 `is_active = false`，禁止實體 `DELETE`（多處外鍵依賴歷史紀錄）。
- 部門刪除前必須檢查底下是否仍有 `is_active = true` 的使用者，若有回傳 409，不得級聯刪除。
- 備份 API 必須用 `ZipArchive` 直接串流寫入 `Response.Body`，逐檔以 `FileStream` 讀取，禁止把檔案內容整批讀進 `byte[]` 或 `MemoryStream`。

## 資料庫變更

- Schema 變動必須先確認 `SPEC.md` 第 3 節 DDL 是否已涵蓋；未涵蓋的表/欄位不得新增，需先回報。
- 一律透過 EF Core Migration（`dotnet ef migrations add`），不得手寫 SQL 直接改資料庫結構後才補 migration。
- 所有時間欄位使用 `timestamptz`，程式內一律以 UTC 處理。

## 測試

- 每個新增或修改的 API endpoint，至少補一則成功案例 + 一則失敗案例（authorization 失敗、validation 失敗擇一)的整合測試，放在 `tests/IsoDocs.Tests/Features/<FeatureName>/`。
- `Storage/` 的邏輯（尤其 `StoragePathGuard` 的路徑穿越防護）需要獨立單元測試，不得只靠整合測試間接覆蓋。

## 明確排除

不得為以下項目新增程式碼或空殼結構（即使看似「順手」）：審核流程狀態機、`notifications` 相關表與服務、SignalR/SSE、LDAP/SSO、非同步備份 Job Queue。這些若被要求，先回報而非直接實作，詳見 root `AGENTS.md`。
