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
          'h-9 w-full appearance-none rounded-sm border bg-white py-0 pr-9 pl-3 text-sm text-slate-900 outline-none transition-colors focus:ring-2 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500',
          error
            ? 'border-red-500 focus:border-red-500 focus:ring-red-100'
            : 'border-slate-300 focus:border-blue-600 focus:ring-blue-100',
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
        className="pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 text-slate-500"
        aria-hidden="true"
      >
        <path d="m6 8 4 4 4-4" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </span>
  )
}
