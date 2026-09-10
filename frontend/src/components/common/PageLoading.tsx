import { Spinner } from './Spinner'
import { classNames } from './classNames'

export interface PageLoadingProps {
  label?: string
  className?: string
}

export function PageLoading({ label = '載入中', className }: PageLoadingProps) {
  return (
    <main
      className={classNames(
        'flex min-h-screen items-center justify-center bg-canvas text-ink-muted',
        className,
      )}
    >
      <div className="flex flex-col items-center gap-3 text-meta" role="status">
        <Spinner size="lg" decorative />
        <span>{label}</span>
      </div>
    </main>
  )
}
