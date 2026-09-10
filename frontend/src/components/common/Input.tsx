import type { InputHTMLAttributes } from 'react'
import { classNames } from './classNames'

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  error?: boolean
}

export function Input({
  error = false,
  type = 'text',
  className,
  ...props
}: InputProps) {
  return (
    <input
      {...props}
      type={type}
      aria-invalid={error || undefined}
      className={classNames(
        'h-control w-full rounded-sm border bg-surface px-2 text-control text-ink outline-none transition-colors placeholder:text-ink-faint focus:ring-1 disabled:cursor-not-allowed disabled:bg-surface-header disabled:text-ink-disabled',
        error
          ? 'border-state-danger focus:border-state-danger focus:ring-state-danger'
          : 'border-line focus:border-primary focus:ring-primary',
        className,
      )}
    />
  )
}
