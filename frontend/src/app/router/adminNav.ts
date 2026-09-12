import type { SidebarNavGroup } from '@/components/common'

const baseUrl = import.meta.env.BASE_URL

export const adminNavGroups: ReadonlyArray<SidebarNavGroup> = [
  {
    key: 'documents',
    label: '文件管理',
    items: [
      {
        key: 'documents',
        label: 'ISO 文件維護',
        href: `${baseUrl}admin/documents`,
        icon: '文',
      },
      {
        key: 'permissions',
        label: '權限維護',
        href: `${baseUrl}admin/permissions`,
        icon: '權',
      },
      {
        key: 'backup',
        label: 'ISO 文件備份',
        href: `${baseUrl}admin/backup`,
        icon: '備',
      },
    ],
  },
  {
    key: 'settings',
    label: '基礎設定',
    items: [
      {
        key: 'departments',
        label: '部門維護',
        href: `${baseUrl}admin/departments`,
        icon: '部',
      },
      {
        key: 'users',
        label: '使用者維護',
        href: `${baseUrl}admin/users`,
        icon: '人',
      },
    ],
  },
]
