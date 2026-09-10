import { useEffect, type HTMLAttributes, type ReactNode } from 'react'
import { classNames } from './classNames'

export interface AlertProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  variant?: 'info' | 'success' | 'warning' | 'error'
  title?: ReactNode
  dismissAfterMs?: number
  onDismiss?: () => void
}

const variantClasses: Record<NonNullable<AlertProps['variant']>, string> = {
  info: 'border-primary bg-primary-subtle text-primary',
  success: 'border-state-active bg-state-active-subtle text-state-active',
  warning: 'border-state-expiring bg-state-expiring-subtle text-state-expiring',
  error: 'border-state-danger bg-state-danger-subtle text-state-danger',
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
  dismissAfterMs,
  onDismiss,
  className,
  children,
  ...props
}: AlertProps) {
  const hasTitle = title !== undefined && title !== null
  const hasChildren = children !== undefined && children !== null

  useEffect(() => {
    if (dismissAfterMs === undefined || onDismiss === undefined) return

    const timer = window.setTimeout(onDismiss, dismissAfterMs)
    return () => window.clearTimeout(timer)
  }, [children, dismissAfterMs, onDismiss, title])

  return (
    <div
      {...props}
      className={classNames(
        'flex gap-3 rounded-sm border px-3 py-2.5 text-meta',
        variantClasses[variant],
        className,
      )}
      role={variant === 'error' ? 'alert' : 'status'}
    >
      <span
        className="mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full border border-current text-fine font-semibold leading-none"
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
