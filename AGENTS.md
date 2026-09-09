# AGENTS.md — ISO 文件管理系統（Root）

> 本檔案是 agent 的入口點。詳細規格不在此複製，一律以 `architecture.md` 與 `SPEC.md` 為準；若本檔案與該兩份文件衝突，以 `SPEC.md` 為準並回報衝突，不得自行判斷取捨。

## 專案是什麼

企業內部 ISO 文件管理系統，多公司、多部門。前後端分離：`/backend` 為 ASP.NET Core 8 API，`/frontend` 為 React + TypeScript SPA。

## 必讀文件（依此順序）

1. `SPEC.md` — 資料庫 schema、API contract、Domain 規則、Acceptance Criteria。這是實作的唯一真實來源。
2. `architecture.md` — 專案結構、分層說明、技術選型理由。
3. `backend/AGENTS.md` — 動到 `/backend` 下任何檔案前必讀。
4. `frontend/AGENTS.md` — 動到 `/frontend` 下任何檔案前必讀。

## 目錄地圖

```
/ISO
  architecture.md
  SPEC.md
  /backend    ← ASP.NET Core 8 API
  /frontend   ← React + TypeScript + Vite
```

## 明確排除（Non-Goals）— 不得實作

以下功能**不在本輪範圍**，即使看起來合理或「順手就能加」，也不得主動實作：

- 審核流程（Draft → Review → Approve 多人簽核）
- 站內通知 / Email 通知
- SignalR / SSE / 任何即時推播
- LDAP / AD / SSO 登入整合
- PDF 浮水印
- 非同步備份 Job Queue（v1 為同步 streaming）

若任務描述暗示需要以上任一項，先停下回報，不得自行擴大範圍或猜測設計。

## 跨層共通規則

- **不得新增 `SPEC.md` 未定義的 API、資料表、欄位**。發現規格缺漏或矛盾時停下回報，不要自行補完後繼續執行。
- API 路徑、DTO 欄位命名、HTTP method 一律照 `SPEC.md` 第 5 節，前後端不可各自發明。
- 角色列舉固定為 `USER` / `COMPANY_ADMIN` / `SYSTEM_ADMIN`，不得增減或改名。
- 所有時間欄位以 UTC 儲存與傳輸，前端顯示才轉 `Asia/Taipei`。
- Commit message 使用祈使句、中文或英文皆可，但同一 PR 內語言需一致；不寫「fix bug」「update」這類無資訊量訊息，需說明改了什麼、為什麼。
- 產生程式碼後，若修改涉及 API contract 或 schema，需同步指出 `SPEC.md` 是否需要更新，不得默默讓文件與程式碼漂移。
- 完成任務前對照 `SPEC.md` 第 8 節 Acceptance Criteria，確認相關案例已被覆蓋。

## 何時停下詢問，而非自行假設

- 需求或規格未涵蓋的邊界情況（例如某欄位驗證規則沒寫清楚）。
- 需要新增第三方套件時（尤其是有 license 疑慮的套件，例如 PDF 處理相關）。
- 任務會影響前後端共用的 contract（DTO 形狀、路由）時，需同時考慮兩側是否都要改，不得只改一邊。
