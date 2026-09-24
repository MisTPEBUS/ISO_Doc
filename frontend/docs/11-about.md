# 版本資訊（關於）

## Route

```text
/admin/about
```

Sidebar 位於「系統」群組，名稱為「版本資訊」。

## 目的

顯示系統名稱、目前版本，以及各版本的更新內容。

## 內容來源

- 版本更新內容為前端靜態資料：`src/features/about/releaseNotes.ts`，不呼叫後端 API。
- `RELEASE_NOTES` 依新到舊排列，第一筆即為「目前版本」。
- 發佈新版本時，在陣列最前面新增一筆即可。

## 每筆版本的欄位

- `version`：版號，不含 `v` 前綴，例如 `2.2`。
- `items`：條列內容，畫面依序編號為「一、二、三、…」。
  - `detail`：補充說明，例如選項範例。
  - `preformatted`：需保留換行與縮排的內容，例如資料夾結構。
- `notes`：版本附註，例如主機搬遷或部署注意事項，顯示為「備註：」。

## 尚未納入

原先建議的 Frontend Version、Backend Version、Build Version、Build Date、Copyright 尚未顯示。Backend Version 需要後端 API，`SPEC.md` 目前沒有定義，因此先不實作。

## 備註

此頁不需要複雜互動。
