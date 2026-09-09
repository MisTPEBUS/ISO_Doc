# [未完善] 使用者維護

## Route

```text
/admin/users
```

## 已知需求

管理系統使用者。

## 建議初始 UI

- 使用者 Table
- 關鍵字查詢
- 新增使用者
- 編輯使用者
- 重設密碼
- 啟用 / 停用

## 目前已知資料概念

先前 Schema 曾出現：

```text
users
├─ id
├─ empno
├─ dept_id
├─ isadmin
└─ password_digest
```

正式欄位仍需後端 Schema 確認。

## TODO

- 使用者姓名欄位。
- 公司與部門關聯方式。
- 帳號登入欄位。
- Admin 是否改成 Role。
- 啟用 / 停用規則。
- 密碼重設流程。
