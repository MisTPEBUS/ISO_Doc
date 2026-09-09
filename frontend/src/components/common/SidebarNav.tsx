import type { ReactNode } from 'react'
import { classNames } from './classNames'

export interface SidebarNavItem {
  key: string
  label: string
  href: string
  icon?: ReactNode
}

export interface SidebarNavGroup {
  key: string
  label: string
  items: ReadonlyArray<SidebarNavItem>
}

export interface SidebarNavProps {
  groups?: ReadonlyArray<SidebarNavGroup>
  activeHref?: string
  ariaLabel?: string
  className?: string
}

export function SidebarNav({
  groups = [],
  activeHref,
  ariaLabel = '管理導覽',
  className,
}: SidebarNavProps) {
  return (
    <aside
      className={classNames(
        'min-h-full w-56 shrink-0 bg-slate-950 p-2 text-white max-xl:w-14',
        className,
      )}
    >
      <nav aria-label={ariaLabel}>
        {groups.map((group) => (
          <div key={group.key} className="mb-3">
            <div className="px-3 pt-3 pb-1 text-xs font-medium text-slate-500 max-xl:hidden">
              {group.label}
            </div>
            <div className="space-y-0.5">
              {group.items.map((item) => {
                const active = item.href === activeHref

                return (
                  <a
                    key={item.key}
                    href={item.href}
                    aria-current={active ? 'page' : undefined}
                    title={item.label}
                    className={classNames(
                      'flex h-9 items-center gap-2 rounded-sm px-3 text-sm font-medium text-slate-300 transition-colors hover:bg-slate-800 hover:text-white focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-blue-400 max-xl:justify-center max-xl:px-0',
                      active && 'bg-slate-700 text-white',
                    )}
                  >
                    <span className="flex w-4 shrink-0 items-center justify-center text-sm text-blue-400" aria-hidden="true">
                      {item.icon}
                    </span>
                    <span className="truncate max-xl:hidden">{item.label}</span>
                  </a>
                )
              })}
            </div>
          </div>
        ))}
      </nav>
    </aside>
  )
}
