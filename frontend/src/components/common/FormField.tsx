import type { ReactNode } from 'react'
import { classNames } from './classNames'

export interface FormFieldProps {
  label?: ReactNode
  htmlFor?: string
  hint?: ReactNode
  error?: ReactNode
  required?: boolean
  children?: ReactNode
  className?: string
}

export function FormField({
  label,
  htmlFor,
  hint,
  error,
  required = false,
  children,
  className,
}: FormFieldProps) {
  const hintId = htmlFor && hint ? `${htmlFor}-hint` : undefined
  const errorId = htmlFor && error ? `${htmlFor}-error` : undefined

  return (
    <div className={classNames('space-y-1.5', className)}>
      {label && (
        <label className="block text-sm font-medium text-slate-700" htmlFor={htmlFor}>
          {label}
          {required && (
            <span className="ml-1 text-red-600" aria-hidden="true">
              *
            </span>
          )}
        </label>
      )}
      {children}
      {!error && hint && (
        <p id={hintId} className="text-xs text-slate-500">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} className="text-xs font-medium text-red-600" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}
