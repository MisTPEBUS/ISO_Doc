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
        'h-9 w-full rounded-sm border bg-white px-3 text-sm text-slate-900 outline-none transition-colors placeholder:text-slate-400 focus:ring-2 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500',
        error
          ? 'border-red-500 focus:border-red-500 focus:ring-red-100'
          : 'border-slate-300 focus:border-blue-600 focus:ring-blue-100',
        className,
      )}
    />
  )
}
