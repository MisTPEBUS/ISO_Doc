import { classNames } from './classNames'

export interface AppHeaderLink {
  label: string
  href: string
}

export interface AppHeaderProps {
  brand?: string
  brandMark?: string
  companyName?: string
  departmentName?: string
  userName?: string
  roleLabel?: string
  modeLink?: AppHeaderLink
  changePasswordHref?: string | null
  logoutLabel?: string
  onLogout?: () => void
  sticky?: boolean
  className?: string
}

export function AppHeader({
  brand = '首都集團 ISO 文件管理系統',
  brandMark = 'ISO',
  companyName,
  departmentName,
  userName = '使用者',
  roleLabel,
  modeLink,
  changePasswordHref = '/change-password',
  logoutLabel = '登出',
  onLogout,
  sticky = true,
  className,
}: AppHeaderProps) {
  const organization = [companyName, departmentName].filter(Boolean).join(' / ')

  return (
    <header
      className={classNames(
        'z-30 flex h-header items-center justify-between gap-4 bg-shell-900 px-4 text-on-shell',
        sticky && 'sticky top-0',
        className,
      )}
    >
      <div className="flex min-w-0 items-center gap-2.5 font-semibold">
        <span className="grid size-7 shrink-0 place-items-center rounded-sm border border-shell-700 font-mono text-fine text-primary-on-shell">
          {brandMark}
        </span>
        <span className="truncate text-control">{brand}</span>
      </div>

      <div className="flex shrink-0 items-center gap-3 text-label">
        {organization && (
          <span className="text-ink-faint max-md:hidden">{organization}</span>
        )}
        {organization && userName && (
          <span className="text-shell-700 max-md:hidden" aria-hidden="true">
            /
          </span>
        )}
        <span className="flex items-center gap-1.5 max-sm:hidden">
          <strong className="font-medium text-on-shell">{userName}</strong>
          {roleLabel && (
            <span className="rounded-xs bg-shell-800 px-1.5 py-0.5 text-fine text-ink-faint">
              {roleLabel}
            </span>
          )}
        </span>
        {modeLink && (
          <a
            href={modeLink.href}
            className="text-primary-on-shell hover:text-on-shell hover:underline focus-visible:rounded-xs"
          >
            {modeLink.label}
          </a>
        )}
        {changePasswordHref && (
          <a
            href={changePasswordHref}
            className="text-primary-on-shell hover:text-on-shell hover:underline focus-visible:rounded-xs"
          >
            修改密碼
          </a>
        )}
        <button
          type="button"
          className="h-control-sm rounded-sm px-2 text-primary-on-shell transition-colors hover:bg-shell-800 hover:text-on-shell"
          onClick={onLogout}
        >
          {logoutLabel}
        </button>
      </div>
    </header>
  )
}
