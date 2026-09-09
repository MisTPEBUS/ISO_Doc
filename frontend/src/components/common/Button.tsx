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
    'border-blue-600 bg-blue-600 text-white hover:border-blue-700 hover:bg-blue-700 focus-visible:outline-blue-600',
  secondary:
    'border-slate-300 bg-white text-slate-700 hover:bg-slate-50 focus-visible:outline-blue-600',
  danger:
    'border-red-600 bg-red-600 text-white hover:border-red-700 hover:bg-red-700 focus-visible:outline-red-600',
  ghost:
    'border-transparent bg-transparent text-slate-700 hover:bg-slate-100 focus-visible:outline-blue-600',
}

const sizeClasses: Record<NonNullable<ButtonProps['size']>, string> = {
  sm: 'h-8 gap-1.5 px-2.5 text-sm',
  md: 'h-9 gap-2 px-3.5 text-sm',
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
        'inline-flex cursor-pointer items-center justify-center rounded-sm border font-medium whitespace-nowrap transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
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
