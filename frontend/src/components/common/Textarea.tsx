import type { TextareaHTMLAttributes } from 'react'
import { classNames } from './classNames'

export interface TextareaProps
  extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  error?: boolean
}

export function Textarea({
  error = false,
  rows = 3,
  className,
  ...props
}: TextareaProps) {
  return (
    <textarea
      {...props}
      rows={rows}
      aria-invalid={error || undefined}
      className={classNames(
        'min-h-20 w-full resize-y rounded-sm border bg-white px-3 py-2 text-sm text-slate-900 outline-none transition-colors placeholder:text-slate-400 focus:ring-2 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500',
        error
          ? 'border-red-500 focus:border-red-500 focus:ring-red-100'
          : 'border-slate-300 focus:border-blue-600 focus:ring-blue-100',
        className,
      )}
    />
  )
}
