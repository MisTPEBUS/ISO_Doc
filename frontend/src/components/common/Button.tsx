import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { classNames } from './classNames'
import { Spinner } from './Spinner'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md'
  loading?: boolean
  loadingText?: ReactNode
}

const variantClasses: Record<NonNullable<ButtonProps['variant']>, string> = {
  primary:
    'border-primary bg-primary text-on-primary hover:border-primary-hover hover:bg-primary-hover focus-visible:outline-primary',
  secondary:
    'border-control-border bg-surface text-ink hover:bg-surface-header focus-visible:outline-primary',
  danger:
    'border-state-danger bg-state-danger text-on-primary hover:border-state-danger-hover hover:bg-state-danger-hover focus-visible:outline-state-danger',
  ghost:
    'border-transparent bg-transparent text-ink-muted hover:bg-surface-header hover:text-primary focus-visible:outline-primary',
}

const sizeClasses: Record<NonNullable<ButtonProps['size']>, string> = {
  sm: 'h-control-sm gap-1.5 px-2 text-control',
  md: 'h-control gap-2 px-3 text-control',
}

export function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  loadingText,
  disabled = false,
  type = 'button',
  className,
  children,
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={classNames(
        'inline-flex cursor-pointer items-center justify-center rounded-sm border font-medium whitespace-nowrap transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:border-line disabled:bg-surface-header disabled:text-ink-disabled',
        variantClasses[variant],
        sizeClasses[size],
        className,
      )}
    >
      {loading && <Spinner size="sm" decorative />}
      {loading ? (loadingText ?? children) : children}
    </button>
  )
}
