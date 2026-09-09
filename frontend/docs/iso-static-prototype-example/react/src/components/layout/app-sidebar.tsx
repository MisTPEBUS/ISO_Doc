export interface SidebarItem {
  label: string;
  to: string;
  iconText: string;
}

export interface SidebarGroup {
  label: string;
  items: SidebarItem[];
}

export interface AppSidebarProps {
  activePath: string;
}

const GROUPS: readonly SidebarGroup[] = [
  {
    label: "基礎設定",
    items: [
      { label: "部門維護", to: "/admin/departments", iconText: "部" },
      { label: "使用者維護", to: "/admin/users", iconText: "人" },
    ],
  },
  {
    label: "文件管理",
    items: [
      { label: "ISO 文件維護", to: "/admin/documents", iconText: "文" },
      { label: "權限維護", to: "/admin/permissions", iconText: "權" },
      { label: "ISO 文件備份", to: "/admin/backups", iconText: "備" },
    ],
  },
  {
    label: "系統",
    items: [{ label: "關於", to: "/admin/about", iconText: "i" }],
  },
];

export function AppSidebar({ activePath }: AppSidebarProps) {
  return (
    <aside className="w-sidebar bg-shell-900 p-2 text-on-shell max-xl:w-sidebar-collapsed">
      {GROUPS.map((group) => (
        <div key={group.label} className="mb-3">
          <div className="px-3 pb-1 pt-3 text-fine text-ink-muted max-xl:hidden">
            {group.label}
          </div>

          {group.items.map((item) => {
            const active = activePath === item.to;

            return (
              <a
                key={item.to}
                href={item.to}
                className={[
                  "mb-0.5 flex h-control items-center gap-2 rounded-sm px-3 text-control text-ink-faint hover:bg-shell-800 max-xl:justify-center max-xl:px-0",
                  active ? "bg-shell-700 text-on-shell" : "",
                ].join(" ")}
              >
                <span className="w-4 text-center text-primary-on-shell">
                  {item.iconText}
                </span>
                <span className="max-xl:hidden">{item.label}</span>
              </a>
            );
          })}
        </div>
      ))}
    </aside>
  );
}
