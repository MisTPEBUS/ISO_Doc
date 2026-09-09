import type { HTMLAttributes, ReactNode } from 'react'
import { classNames } from './classNames'

export interface AlertProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  variant?: 'info' | 'success' | 'warning' | 'error'
  title?: ReactNode
}

const variantClasses: Record<NonNullable<AlertProps['variant']>, string> = {
  info: 'border-blue-200 bg-blue-50 text-blue-800',
  success: 'border-emerald-200 bg-emerald-50 text-emerald-800',
  warning: 'border-amber-200 bg-amber-50 text-amber-900',
  error: 'border-red-200 bg-red-50 text-red-800',
}

const icons: Record<NonNullable<AlertProps['variant']>, string> = {
  info: 'i',
  success: '✓',
  warning: '!',
  error: '×',
}

export function Alert({
  variant = 'info',
  title,
  className,
  children,
  ...props
}: AlertProps) {
  const hasTitle = title !== undefined && title !== null
  const hasChildren = children !== undefined && children !== null

  return (
    <div
      {...props}
      className={classNames(
        'flex gap-3 rounded-sm border px-3 py-2.5 text-sm',
        variantClasses[variant],
        className,
      )}
      role={variant === 'error' ? 'alert' : 'status'}
    >
      <span
        className="mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full border border-current text-[10px] font-bold leading-none"
        aria-hidden="true"
      >
        {icons[variant]}
      </span>
      <div className="min-w-0">
        {hasTitle && <div className="font-semibold">{title}</div>}
        {hasChildren && (
          <div className={classNames(hasTitle && 'mt-0.5')}>{children}</div>
        )}
      </div>
    </div>
  )
}
