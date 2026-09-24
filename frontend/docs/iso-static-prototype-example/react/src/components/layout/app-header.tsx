import type { CurrentUser } from "../../features/auth/types";

export interface AppHeaderProps {
  user: CurrentUser;
  showAdminLink?: boolean;
  onLogout: () => void;
}

export function AppHeader({
  user,
  showAdminLink = false,
  onLogout,
}: AppHeaderProps) {
  return (
    <header className="flex h-header items-center justify-between bg-shell-900 px-4 text-on-shell">
      <div className="flex items-center gap-2 font-semibold">
        <span className="grid size-6 place-items-center rounded-sm border border-shell-700 text-fine text-primary-on-shell">
          ISO
        </span>
        <span>首都集團 ISO 文件管理系統 V2.2</span>
      </div>

      <div className="flex items-center gap-3 text-label">
        <span className="text-ink-faint">
          {user.companyName} / {user.departmentName} / {user.name}
        </span>

        {showAdminLink ? (
          <a
            href="/admin/documents"
            className="text-primary-on-shell hover:underline"
          >
            管理
          </a>
        ) : null}

        <a
          href="/change-password"
          className="text-primary-on-shell hover:underline"
        >
          修改密碼
        </a>

        <button
          type="button"
          className="h-control-sm rounded-sm px-2 text-primary-on-shell hover:bg-shell-800"
          onClick={onLogout}
        >
          登出
        </button>
      </div>
    </header>
  );
}
