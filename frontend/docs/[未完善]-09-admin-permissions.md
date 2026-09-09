# [未完善] 權限維護

## Route

```text
/admin/permissions
```

## 已知需求

系統具有：

- 多家公司
- 多部門
- 使用者
- ISO 文件權限

## 原則

前端只負責：

- 顯示權限設定 UI。
- 送出權限設定。
- 根據後端回傳結果控制可見操作。

不得在前端自行建立真正的授權判斷。

## TODO

尚未確認完整權限模型：

- 權限是 User-based、Department-based 或 Role-based。
- 文件讀取權限。
- 文件下載權限。
- 管理功能權限。
- 跨公司權限。
- Admin / Super Admin 定義。
