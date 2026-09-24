# Routes And Layout

## Route 規劃

```text
/
├─ /                     首頁
├─ /login                原版登入頁
├─ /login_1              高對比登入頁
├─ /change-password      修改密碼
└─ /admin
   ├─ /departments       部門維護
   ├─ /users             使用者維護
   ├─ /documents         ISO 文件維護
   ├─ /permissions       權限維護
   ├─ /backup            ISO 文件備份
   └─ /about             關於
```

## MainLayout

適用：

- `/`
- `/change-password`

規則：

- 不使用 Sidebar。
- 可保留 Header。
- Header 可提供使用者資訊、修改密碼、登出等操作。

## AdminLayout

適用：

- `/admin/*`

規則：

- 使用 Sidebar。
- Main content 顯示目前管理頁。
- Sidebar 可收合，但不是必要條件。

## Admin Sidebar

建議項目：

1. 部門維護
2. 使用者維護
3. ISO 文件維護
4. 權限維護
5. ISO 文件備份
6. 關於
