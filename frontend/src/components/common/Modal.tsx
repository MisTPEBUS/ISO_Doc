import { useEffect, useId, useRef, type ReactNode, type RefObject } from 'react'
import { classNames } from './classNames'

export interface ModalProps {
  open?: boolean
  onClose?: () => void
  title?: ReactNode
  description?: ReactNode
  children?: ReactNode
  footer?: ReactNode
  size?: 'sm' | 'md' | 'lg'
  closeOnBackdrop?: boolean
  closeOnEscape?: boolean
  showCloseButton?: boolean
  initialFocusRef?: RefObject<HTMLElement | null>
  className?: string
}

const sizeClasses: Record<NonNullable<ModalProps['size']>, string> = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
}

export function Modal({
  open = false,
  onClose,
  title = '對話框',
  description,
  children,
  footer,
  size = 'md',
  closeOnBackdrop = true,
  closeOnEscape = true,
  showCloseButton = true,
  initialFocusRef,
  className,
}: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const descriptionId = useId()

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (open && !dialog.open) {
      dialog.showModal()
      initialFocusRef?.current?.focus()
    } else if (!open && dialog.open) {
      dialog.close()
    }
  }, [initialFocusRef, open])

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      className={classNames(
        'm-auto w-[calc(100%-2rem)] rounded-md border border-line-strong bg-surface p-0 text-left text-ink shadow-float backdrop:bg-overlay',
        sizeClasses[size],
        className,
      )}
      onCancel={(event) => {
        event.preventDefault()
        if (closeOnEscape) onClose?.()
      }}
      onClick={(event) => {
        if (closeOnBackdrop && event.target === event.currentTarget) onClose?.()
      }}
    >
      <div className="flex items-start justify-between gap-4 border-b border-line px-5 py-4">
        <div className="min-w-0">
          <h2 id={titleId} className="text-section-label text-ink">
            {title}
          </h2>
          {description && (
            <p id={descriptionId} className="mt-1 text-meta text-ink-muted">
              {description}
            </p>
          )}
        </div>
        {showCloseButton && (
          <button
            type="button"
            className="inline-flex size-8 shrink-0 items-center justify-center rounded-sm text-xl leading-none text-ink-muted transition-colors hover:bg-surface-header hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            aria-label="關閉對話框"
            onClick={onClose}
          >
            ×
          </button>
        )}
      </div>
      <div className="px-5 py-4">{children}</div>
      {footer && (
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-line bg-surface-header px-5 py-3">
          {footer}
        </div>
      )}
    </dialog>
  )
}
