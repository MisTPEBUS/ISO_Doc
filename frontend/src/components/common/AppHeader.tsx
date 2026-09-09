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
  modeLink?: AppHeaderLink
  changePasswordHref?: string
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
        'z-30 flex h-14 items-center justify-between gap-4 bg-slate-950 px-4 text-white',
        sticky && 'sticky top-0',
        className,
      )}
    >
      <div className="flex min-w-0 items-center gap-2.5 font-semibold">
        <span className="grid size-6 shrink-0 place-items-center rounded-sm border border-slate-700 text-[10px] text-blue-400">
          {brandMark}
        </span>
        <span className="truncate text-sm sm:text-base">{brand}</span>
      </div>

      <div className="flex shrink-0 items-center gap-3 text-sm">
        {organization && (
          <span className="text-slate-400 max-md:hidden">{organization}</span>
        )}
        {organization && userName && (
          <span className="text-slate-600 max-md:hidden" aria-hidden="true">
            /
          </span>
        )}
        <strong className="font-medium text-white max-sm:hidden">{userName}</strong>
        {modeLink && (
          <a
            href={modeLink.href}
            className="text-blue-400 hover:text-blue-300 hover:underline focus-visible:rounded-xs focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-400"
          >
            {modeLink.label}
          </a>
        )}
        <a
          href={changePasswordHref}
          className="text-blue-400 hover:text-blue-300 hover:underline focus-visible:rounded-xs focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-400"
        >
          修改密碼
        </a>
        <button
          type="button"
          className="h-8 rounded-sm px-2 text-blue-400 transition-colors hover:bg-slate-800 hover:text-blue-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-400"
          onClick={onLogout}
        >
          {logoutLabel}
        </button>
      </div>
    </header>
  )
}
