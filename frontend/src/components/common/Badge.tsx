import type { HTMLAttributes } from 'react'
import { classNames } from './classNames'

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: 'neutral' | 'info' | 'success' | 'warning' | 'danger'
}

const variantClasses: Record<NonNullable<BadgeProps['variant']>, string> = {
  neutral: 'bg-state-obsolete-subtle text-state-obsolete ring-state-obsolete',
  info: 'bg-state-review-subtle text-state-review ring-state-review',
  success: 'bg-state-active-subtle text-state-active ring-state-active',
  warning: 'bg-state-expiring-subtle text-state-expiring ring-state-expiring',
  danger: 'bg-state-danger-subtle text-state-danger ring-state-danger',
}

export function Badge({
  variant = 'neutral',
  className,
  children,
  ...props
}: BadgeProps) {
  return (
    <span
      {...props}
      className={classNames(
        'inline-flex items-center rounded-sm px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        variantClasses[variant],
        className,
      )}
    >
      {children}
    </span>
  )
}
