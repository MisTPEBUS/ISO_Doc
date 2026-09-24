# 文件批次新增：發行單位

入口：`/ISO/admin/documents/import`。

- 批次匯入表格與 Excel 對應均已移除「頁數」。新匯入草稿的 `pageCount` 為 null；舊客戶端仍可選填非負頁數。
- 手動新增及 Excel 預覽表格皆提供逐筆「發行部門」下拉選單，選項為所選公司的全部部門。
- Excel 可對應「發行部門」、「發行單位」、`deptId` 等欄位，內容可為部門 UUID 或所選公司中的完整部門名稱。
- 若 Excel 未填發行部門，依文件編號開頭推定：GM=總經理室、GA=總務部、OP=業務部、MT=機務部、FN=財務部、IT=資訊中心、HR=人力資源部。只有所選公司存在同名部門時才自動帶入。
- 明確填寫的 Excel 值優先於推定；無法對應的值保留在表格並提示修正。也可逐筆改選，或手動選擇「不指定發行部門」。
- 未能推定且未指定時送出 `deptId: null`，送出後欄位顯示空白。
- 部門載入失敗可重試；清除全部資料後才能切換公司，避免沿用其他公司的選項。

API：`POST /api/documents/bulk-import`，`items[].deptId` 為選填 UUID/null，`items[].pageCount` 也可省略。後端驗證部門公司範圍後寫入既有 `documents.dept_id`。不存在或跨公司的部門列入該筆 `failed.errors.deptId`，其他資料繼續處理。回應中的 `pageCount` 可為 null；儲存後的發行部門可由文件列表／詳情 API 讀取。

發行單位不改變文件讀取權限：成功建立時仍預設開放所屬公司全部部門。規格已同步更新於 `SPEC.md` 批次匯入契約及驗收案例，無資料庫 schema 變更。
