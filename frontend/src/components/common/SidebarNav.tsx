import { useState, type ReactNode } from 'react'
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
  const [collapsed, setCollapsed] = useState(() =>
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(max-width: 1279px)').matches,
  )

  return (
    <aside
      className={classNames(
        'min-h-full shrink-0 bg-shell-900 p-2 text-on-shell transition-[width] duration-200',
        collapsed ? 'w-sidebar-collapsed' : 'w-sidebar',
        className,
      )}
      data-collapsed={collapsed || undefined}
    >
      <div className={classNames('mb-1 flex h-control items-center', collapsed ? 'justify-center' : 'justify-end')}>
        <button
          type="button"
          className="flex size-8 items-center justify-center rounded-sm text-ink-faint transition-colors hover:bg-shell-800 hover:text-on-shell focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-primary-on-shell"
          aria-label={collapsed ? '展開側邊導覽' : '收合側邊導覽'}
          aria-expanded={!collapsed}
          title={collapsed ? '展開側邊導覽' : '收合側邊導覽'}
          onClick={() => setCollapsed((current) => !current)}
        >
          <svg
            viewBox="0 0 20 20"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.75"
            className={classNames('size-4 transition-transform', collapsed && 'rotate-180')}
            aria-hidden="true"
          >
            <path d="m12 5-5 5 5 5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </button>
      </div>
      <nav aria-label={ariaLabel}>
        {groups.map((group) => (
          <div key={group.key} className="mb-3">
            <div className={classNames('px-3 pt-3 pb-1 text-fine font-medium text-ink-muted', collapsed && 'hidden')}>
              {group.label}
            </div>
            <div className="space-y-0.5">
              {group.items.map((item) => {
                const active = item.href === activeHref
                  || activeHref?.startsWith(`${item.href}/`) === true

                return (
                  <a
                    key={item.key}
                    href={item.href}
                    aria-current={active ? 'page' : undefined}
                    title={item.label}
                    className={classNames(
                      'flex h-control items-center gap-2 rounded-sm text-control font-medium text-ink-faint transition-colors hover:bg-shell-800 hover:text-on-shell focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-primary-on-shell',
                      collapsed ? 'justify-center px-0' : 'px-3',
                      active && 'bg-shell-700 text-on-shell',
                    )}
                  >
                    <span className="flex w-4 shrink-0 items-center justify-center text-label text-primary-on-shell" aria-hidden="true">
                      {item.icon}
                    </span>
                    <span className={classNames('truncate', collapsed && 'hidden')}>{item.label}</span>
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
