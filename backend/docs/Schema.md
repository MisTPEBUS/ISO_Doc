# ISO 文件管理系統資料庫 Schema

## 1. 設計原則

- 資料庫：PostgreSQL
- 主鍵統一使用 `uuid`
- UUID 預設值統一使用 `gen_random_uuid()`
- 時間欄位統一使用 `timestamptz`
- 資料表與欄位名稱統一使用 `snake_case`
- ISO 主文件與附件皆採版本化管理
- 檔案實體儲存於 MinIO，資料庫僅保存 Object Key 與檔案 Metadata
- 已發布版本原則上不進行 Hard Delete
- 使用者透過部門取得公司歸屬，不在 `users` 重複保存 `company_id`
- ISO 主文件版本與附件皆支援「先建立中繼資料、稍後補檔」：未補檔前檔案欄位可為 NULL / 版本維持 `DRAFT`
- 附件為選配：一份文件（或一個主文件版本）可對應 0..N 個附件；附件檔案可一次上傳多個

> 註：本專案以 `SPEC.md` 第 3 節 DDL 為權威資料結構定義。本文件與 `SPEC.md` 在附件模型、版本狀態列舉、儲存後端（MinIO / 本機）等處尚有差異，實作時以 `SPEC.md` 為準。

---

## 2. 資料表定義

### 2.1 `companies` — 公司

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 公司識別碼 |
| `name` | varchar(100) | 否 | — | UNIQUE | 公司名稱 |
| `name_en` | varchar(100) | 是 | `NULL` | — | 公司浮水印英文名稱 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |
| `updated_at` | timestamptz | 否 | `now()` | — | 更新時間 |

---

### 2.2 `depts` — 部門

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 部門識別碼 |
| `company_id` | uuid | 否 | — | FK、INDEX | 所屬公司 |
| `name` | varchar(100) | 否 | — | — | 部門名稱 |
| `seq` | integer | 是 | `NULL` | — | 顯示排序 |
| `is_active` | boolean | 否 | `true` | INDEX | 是否啟用 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |
| `updated_at` | timestamptz | 否 | `now()` | — | 更新時間 |

約束：

- `depts.company_id -> companies.id`
- `UNIQUE(company_id, name)`

---

### 2.3 `users` — 使用者

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 使用者識別碼 |
| `dept_id` | uuid | 否 | — | FK、INDEX | 所屬部門 |
| `empno` | varchar(50) | 否 | — | INDEX | 員工編號 / 帳號 |
| `name` | varchar(100) | 否 | — | — | 員工姓名 |
| `email` | varchar(255) | 是 | `NULL` | INDEX | 電子郵件 |
| `password_hash` | varchar(255) | 否 | — | — | 密碼雜湊 |
| `role` | varchar(30) | 否 | `USER` | INDEX | 系統角色 |
| `is_active` | boolean | 否 | `true` | INDEX | 是否啟用 |
| `notify_email_enabled` | boolean | 否 | `false` | — | 是否啟用 Email 通知 |
| `last_login_at` | timestamptz | 是 | `NULL` | — | 最後登入時間 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |
| `updated_at` | timestamptz | 否 | `now()` | — | 更新時間 |

角色：

| enum | Scope | 說明 |
|---|---|---|
| `USER` | DEPT | 只能讀取授權給所屬部門的文件 |
| `COMPANY_ADMIN` | COMPANY | 管理所屬公司的文件、附件及文件權限 |
| `SYSTEM_ADMIN` | GLOBAL | 管理全部公司與系統設定 |

約束：

- `users.dept_id -> depts.id`
- 使用者公司歸屬由 `users.dept_id -> depts.company_id` 取得
- 員工編號唯一性以公司為範圍，由應用程式或資料庫約束保證
- `role` 允許值：
  - `USER`
  - `COMPANY_ADMIN`
  - `SYSTEM_ADMIN`

---

### 2.4 `documents` — ISO 主文件

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 文件識別碼 |
| `company_id` | uuid | 否 | — | FK、INDEX | 所屬公司 |
| `document_no` | varchar(100) | 否 | — | — | 文件編號，例如 `HR-I-01` |
| `name` | varchar(255) | 否 | — | — | 文件名稱 |
| `is_active` | boolean | 否 | `true` | INDEX | 文件是否啟用 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |
| `updated_at` | timestamptz | 否 | `now()` | — | 更新時間 |

約束：

- `documents.company_id -> companies.id`
- `UNIQUE(company_id, document_no)`

---

### 2.5 `document_versions` — ISO 文件版本

用於管理文件版本、發布日期、生效日期與歷史版本。

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 文件版本識別碼 |
| `document_id` | uuid | 否 | — | FK、INDEX | 所屬文件 |
| `version` | varchar(20) | 否 | — | — | 版本，例如 `1.0` |
| `status` | varchar(20) | 否 | `DRAFT` | INDEX | 文件版本狀態 |
| `publish_date` | date | 是 | `NULL` | — | 發布日期 |
| `effective_date` | date | 是 | `NULL` | INDEX | 生效日期（`DRAFT` 預先建立時可為 NULL，發布前須補齊） |
| `expired_date` | date | 是 | `NULL` | — | 失效日期 |
| `page_count` | integer | 是 | `NULL` | — | 頁數 |
| `memo` | text | 是 | `NULL` | — | 備註 |
| `file_key` | varchar(500) | 是 | `NULL` | UNIQUE | MinIO Object Key（未補檔時為 NULL） |
| `original_file_name` | varchar(255) | 是 | `NULL` | — | 原始檔名（未補檔時為 NULL） |
| `content_type` | varchar(100) | 是 | `NULL` | — | MIME Type（未補檔時為 NULL） |
| `file_size` | bigint | 是 | `NULL` | — | 檔案大小，Bytes（未補檔時為 NULL） |
| `checksum` | varchar(64) | 是 | `NULL` | — | SHA-256 Hex |
| `created_by` | uuid | 否 | — | FK、INDEX | 建立人 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |

> 允許先建立「僅中繼資料」的主文件版本（`status = DRAFT`、檔案與 `effective_date` 欄位為 NULL），檔案稍後補上後轉為生效流程。

版本狀態：

| status | 說明 |
|---|---|
| `DRAFT` | 草稿 |
| `SCHEDULED` | 已排程，等待生效 |
| `EFFECTIVE` | 目前生效版本 |
| `EXPIRED` | 已失效版本 |
| `CANCELLED` | 已取消版本 |

約束：

- `document_versions.document_id -> documents.id`
- `document_versions.created_by -> users.id`
- `UNIQUE(document_id, version)`
- 主文件僅允許 PDF
- 檔案驗證至少包含副檔名、MIME Type、檔案大小
- `checksum` 固定使用 SHA-256

---

### 2.6 `attachments` — 文件附件主檔

代表附件本身，不保存實體版本檔案。

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 附件識別碼 |
| `document_id` | uuid | 否 | — | FK、INDEX | 所屬 ISO 文件 |
| `attachment_no` | varchar(50) | 否 | — | — | 附件編號 |
| `name` | varchar(255) | 否 | — | — | 附件名稱 |
| `is_active` | boolean | 否 | `true` | INDEX | 是否啟用 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |
| `updated_at` | timestamptz | 否 | `now()` | — | 更新時間 |

約束：

- `attachments.document_id -> documents.id`
- `UNIQUE(document_id, attachment_no)`
- 附件為選配：一份文件可有 0..N 個附件
- 可只建立附件主檔（`attachment_no` + `name`），稍後再建立附件版本 / 上傳檔案

---

### 2.7 `attachment_versions` — 附件版本

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 附件版本識別碼 |
| `attachment_id` | uuid | 否 | — | FK、INDEX | 所屬附件 |
| `version` | varchar(20) | 否 | — | — | 附件版本 |
| `status` | varchar(20) | 否 | `DRAFT` | INDEX | 版本狀態 |
| `publish_date` | date | 是 | `NULL` | — | 發布日期 |
| `effective_date` | date | 是 | `NULL` | INDEX | 生效日期（預先建立時可為 NULL） |
| `expired_date` | date | 是 | `NULL` | — | 失效日期 |
| `file_key` | varchar(500) | 是 | `NULL` | UNIQUE | MinIO Object Key（未補檔時為 NULL） |
| `original_file_name` | varchar(255) | 是 | `NULL` | — | 原始檔名（未補檔時為 NULL） |
| `content_type` | varchar(100) | 是 | `NULL` | — | MIME Type（未補檔時為 NULL） |
| `file_size` | bigint | 是 | `NULL` | — | 檔案大小，Bytes（未補檔時為 NULL） |
| `checksum` | varchar(64) | 是 | `NULL` | — | SHA-256 Hex |
| `created_by` | uuid | 否 | — | FK、INDEX | 建立人 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |

> 允許先建立「僅中繼資料」的附件版本（`status = DRAFT`、檔案欄位為 NULL），檔案稍後補上；單次 API 可批次建立 / 上傳多個附件，批次採全有全無。

約束：

- `attachment_versions.attachment_id -> attachments.id`
- `attachment_versions.created_by -> users.id`
- `UNIQUE(attachment_id, version)`
- 附件允許：
  - `.jpg`
  - `.jpeg`
  - `.png`
  - `.pdf`
  - `.doc`
  - `.docx`
  - `.xls`
  - `.xlsx`
  - `.odt`
  - `.ods`
- 檔案驗證至少包含副檔名、MIME Type、檔案大小
- `checksum` 固定使用 SHA-256

---

### 2.8 `document_version_attachments` — 文件版本與附件版本關聯

用於記錄某一主文件版本實際搭配的附件版本，確保歷史版本可完整還原。附件為選配，一個主文件版本可關聯 0..N 個附件版本。

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 關聯識別碼 |
| `document_version_id` | uuid | 否 | — | FK、INDEX | 文件版本 |
| `attachment_version_id` | uuid | 否 | — | FK、INDEX | 附件版本 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |

約束：

- `document_version_attachments.document_version_id -> document_versions.id`
- `document_version_attachments.attachment_version_id -> attachment_versions.id`
- `UNIQUE(document_version_id, attachment_version_id)`

---

### 2.9 `document_dept_permissions` — 文件部門存取權限

權限設定在文件層級，各版本共用同一份文件權限。

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 權限識別碼 |
| `document_id` | uuid | 否 | — | FK、INDEX | 文件 |
| `dept_id` | uuid | 否 | — | FK、INDEX | 可讀取文件的部門 |
| `created_by` | uuid | 否 | — | FK、INDEX | 設定權限的人 |
| `created_at` | timestamptz | 否 | `now()` | — | 建立時間 |

約束：

- `document_dept_permissions.document_id -> documents.id`
- `document_dept_permissions.dept_id -> depts.id`
- `document_dept_permissions.created_by -> users.id`
- `UNIQUE(document_id, dept_id)`

---

### 2.10 `audit_logs` — 系統稽核紀錄

取代舊版 `tracers`，用於記錄登入、查看、下載、建立、發布、權限異動等操作。

| 欄位 | 型別 | NULL | 預設值 | 索引 | 說明 |
|---|---|---:|---|---|---|
| `id` | uuid | 否 | `gen_random_uuid()` | PK | 稽核紀錄識別碼 |
| `user_id` | uuid | 是 | `NULL` | FK、INDEX | 操作者 |
| `action` | varchar(50) | 否 | — | INDEX | 操作類型 |
| `resource_type` | varchar(50) | 是 | `NULL` | INDEX | 資源類型 |
| `resource_id` | uuid | 是 | `NULL` | INDEX | 資源識別碼 |
| `ip_address` | varchar(45) | 是 | `NULL` | — | IPv4 / IPv6 |
| `user_agent` | text | 是 | `NULL` | — | User Agent |
| `metadata` | jsonb | 是 | `NULL` | — | 額外操作資訊 |
| `created_at` | timestamptz | 否 | `now()` | INDEX | 操作時間 |

建議 Action：

| action | 說明 |
|---|---|
| `LOGIN` | 登入 |
| `LOGOUT` | 登出 |
| `VIEW` | 查看 |
| `DOWNLOAD` | 下載 |
| `CREATE` | 建立 |
| `UPDATE` | 修改 |
| `DELETE` | 刪除 |
| `PUBLISH` | 發布 |
| `CHANGE_PERMISSION` | 修改文件權限 |

設計原則：

- Audit Log 原則上只新增，不修改
- 不提供一般 Hard Delete
- `metadata` 可保存文件編號、版本、附件版本等快照資訊

---

## 3. 主要關聯

```text
companies
    │
    ├── depts
    │     │
    │     └── users
    │
    └── documents
          │
          ├── document_versions
          │     │
          │     └── document_version_attachments
          │                    │
          │                    └── attachment_versions
          │
          ├── attachments
          │     │
          │     └── attachment_versions
          │
          └── document_dept_permissions
                      │
                      └── depts

users
    ├── document_versions.created_by
    ├── attachment_versions.created_by
    ├── document_dept_permissions.created_by
    └── audit_logs.user_id
```

---

## 4. 索引與 Unique Constraint 一覽

| 資料表 | 欄位 | 類型 |
|---|---|---|
| `companies` | `name` | UNIQUE |
| `depts` | `company_id` | INDEX |
| `depts` | `company_id, name` | UNIQUE |
| `depts` | `is_active` | INDEX |
| `users` | `dept_id` | INDEX |
| `users` | `empno` | INDEX |
| `users` | `email` | INDEX |
| `users` | `role` | INDEX |
| `users` | `is_active` | INDEX |
| `documents` | `company_id` | INDEX |
| `documents` | `company_id, document_no` | UNIQUE |
| `documents` | `is_active` | INDEX |
| `document_versions` | `document_id` | INDEX |
| `document_versions` | `document_id, version` | UNIQUE |
| `document_versions` | `document_id, status` | INDEX |
| `document_versions` | `effective_date` | INDEX |
| `document_versions` | `file_key` | UNIQUE |
| `attachments` | `document_id` | INDEX |
| `attachments` | `document_id, attachment_no` | UNIQUE |
| `attachments` | `is_active` | INDEX |
| `attachment_versions` | `attachment_id` | INDEX |
| `attachment_versions` | `attachment_id, version` | UNIQUE |
| `attachment_versions` | `effective_date` | INDEX |
| `attachment_versions` | `file_key` | UNIQUE |
| `document_version_attachments` | `document_version_id` | INDEX |
| `document_version_attachments` | `attachment_version_id` | INDEX |
| `document_version_attachments` | `document_version_id, attachment_version_id` | UNIQUE |
| `document_dept_permissions` | `document_id` | INDEX |
| `document_dept_permissions` | `dept_id` | INDEX |
| `document_dept_permissions` | `document_id, dept_id` | UNIQUE |
| `audit_logs` | `user_id` | INDEX |
| `audit_logs` | `action` | INDEX |
| `audit_logs` | `resource_type, resource_id` | INDEX |
| `audit_logs` | `created_at` | INDEX |

---

## 5. 檔案儲存規則

資料庫不保存 MinIO 完整 URL，只保存 Object Key。

範例：

```text
documents/{company_id}/{document_id}/{document_version_id}/document.pdf

attachments/{company_id}/{document_id}/{attachment_id}/{attachment_version_id}/attachment.xlsx
```

應用程式透過 Storage Service 處理：

```text
API
 ↓
IStorageService
 ↓
MinIO
```

未來若由 MinIO 遷移至 GCS、S3 或其他 Object Storage，不需修改資料庫中的完整網址。

---

## 6. 生效版本規則

範例：

```text
2026-09-30

HR-I-01
v1.0  EFFECTIVE
v2.0  SCHEDULED
      effective_date = 2026-10-01
```

2026-10-01 生效後：

```text
v1.0 -> EXPIRED
v2.0 -> EFFECTIVE
```

系統一般查詢預設只顯示：

```text
documents.is_active = true
AND document_versions.status = EFFECTIVE
```

管理介面可另外查詢：

```text
DRAFT
SCHEDULED
EFFECTIVE
EXPIRED
CANCELLED
```
