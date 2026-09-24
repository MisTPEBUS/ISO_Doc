# 管理介面 Layout

## Route Prefix

```text
/admin
```

## Layout

管理介面使用 Sidebar。

```text
┌──────────────┬─────────────────────────┐
│ Sidebar      │ Header                  │
│              ├─────────────────────────┤
│ 部門維護     │                         │
│ 使用者維護   │ Main Content            │
│ ISO文件維護  │                         │
│ 權限維護     │                         │
│ ISO文件備份  │                         │
│ 版本資訊     │                         │
└──────────────┴─────────────────────────┘
```

## Sidebar

項目：

- 部門維護
- 使用者維護
- ISO 文件維護
- 權限維護
- ISO 文件備份
- 版本資訊（/admin/about）

## 建議

Sidebar route config 集中管理：

```ts
type AdminNavItem = {
  label: string;
  path: string;
  icon?: React.ComponentType;
};
```

不要將 Sidebar 項目分散硬編碼在多個 Component。
