import type { SelectHTMLAttributes } from 'react'
import { classNames } from './classNames'

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  error?: boolean
}

export function Select({
  error = false,
  className,
  children,
  ...props
}: SelectProps) {
  return (
    <span className="relative block w-full">
      <select
        {...props}
        aria-invalid={error || undefined}
        className={classNames(
          'h-9 w-full appearance-none rounded-sm border bg-surface py-0 pr-9 pl-3 text-sm text-ink outline-none transition-colors focus:ring-1 disabled:cursor-not-allowed disabled:bg-surface-header disabled:text-ink-disabled',
          error
            ? 'border-state-danger focus:border-state-danger focus:ring-state-danger'
            : 'border-control-border focus:border-primary focus:ring-primary',
          className,
        )}
      >
        {children}
      </select>
      <svg
        viewBox="0 0 20 20"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.75"
        className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-ink-muted"
        aria-hidden="true"
      >
        <path d="m6 8 4 4 4-4" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  )
}
