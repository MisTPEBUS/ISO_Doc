import type { HTMLAttributes } from 'react'
import { classNames } from './classNames'

export interface SpinnerProps extends HTMLAttributes<HTMLSpanElement> {
  size?: 'sm' | 'md' | 'lg'
  label?: string
  decorative?: boolean
}

const sizeClasses: Record<NonNullable<SpinnerProps['size']>, string> = {
  sm: 'size-4 border-2',
  md: 'size-5 border-2',
  lg: 'size-8 border-3',
}

export function Spinner({
  size = 'md',
  label = '載入中',
  decorative = false,
  className,
  ...props
}: SpinnerProps) {
  return (
    <span
      {...props}
      className={classNames(
        'inline-block shrink-0 animate-spin rounded-full border-current border-r-transparent align-[-0.125em]',
        sizeClasses[size],
        className,
      )}
      role={decorative ? undefined : 'status'}
      aria-hidden={decorative || undefined}
      aria-label={decorative ? undefined : label}
    />
  )
}
