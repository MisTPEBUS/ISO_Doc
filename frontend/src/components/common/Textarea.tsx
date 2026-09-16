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
        'min-h-20 w-full resize-y rounded-sm border bg-surface px-3 py-2 text-sm text-ink outline-none transition-colors placeholder:text-ink-faint focus:ring-1 disabled:cursor-not-allowed disabled:bg-surface-header disabled:text-ink-disabled',
        error
          ? 'border-state-danger focus:border-state-danger focus:ring-state-danger'
          : 'border-control-border focus:border-primary focus:ring-primary',
        className,
      )}
    />
  )
}
