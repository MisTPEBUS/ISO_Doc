import type { HTMLAttributes } from 'react'
import { classNames } from './classNames'

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: 'neutral' | 'info' | 'success' | 'warning' | 'danger'
}

const variantClasses: Record<NonNullable<BadgeProps['variant']>, string> = {
  neutral: 'bg-slate-100 text-slate-700 ring-slate-300',
  info: 'bg-blue-50 text-blue-700 ring-blue-200',
  success: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-800 ring-amber-200',
  danger: 'bg-red-50 text-red-700 ring-red-200',
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
