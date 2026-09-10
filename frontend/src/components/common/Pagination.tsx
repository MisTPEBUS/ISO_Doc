import { classNames } from './classNames'

export interface PaginationProps {
  page?: number
  pageSize?: number
  totalCount?: number
  siblingCount?: number
  onPageChange?: (page: number) => void
  className?: string
}

type PaginationItem = number | 'ellipsis-start' | 'ellipsis-end'

function getPaginationItems(
  currentPage: number,
  totalPages: number,
  siblingCount: number,
): PaginationItem[] {
  const visibleCount = siblingCount * 2 + 5
  if (totalPages <= visibleCount) {
    return Array.from({ length: totalPages }, (_, index) => index + 1)
  }

  const leftSibling = Math.max(currentPage - siblingCount, 1)
  const rightSibling = Math.min(currentPage + siblingCount, totalPages)
  const showLeftEllipsis = leftSibling > 2
  const showRightEllipsis = rightSibling < totalPages - 1

  if (!showLeftEllipsis) {
    const leftItems = Array.from(
      { length: 3 + siblingCount * 2 },
      (_, index) => index + 1,
    )
    return [...leftItems, 'ellipsis-end', totalPages]
  }

  if (!showRightEllipsis) {
    const start = totalPages - (2 + siblingCount * 2)
    const rightItems = Array.from(
      { length: 3 + siblingCount * 2 },
      (_, index) => start + index,
    )
    return [1, 'ellipsis-start', ...rightItems]
  }

  const middleItems = Array.from(
    { length: rightSibling - leftSibling + 1 },
    (_, index) => leftSibling + index,
  )
  return [1, 'ellipsis-start', ...middleItems, 'ellipsis-end', totalPages]
}

export function Pagination({
  page = 1,
  pageSize = 10,
  totalCount = 0,
  siblingCount = 1,
  onPageChange,
  className,
}: PaginationProps) {
  const safePageSize = Math.max(1, pageSize)
  const totalPages = Math.max(1, Math.ceil(Math.max(0, totalCount) / safePageSize))
  const currentPage = Math.min(Math.max(1, page), totalPages)
  const safeSiblingCount = Math.max(0, Math.floor(siblingCount))
  const startItem = totalCount === 0 ? 0 : (currentPage - 1) * safePageSize + 1
  const endItem = Math.min(currentPage * safePageSize, totalCount)
  const items = getPaginationItems(currentPage, totalPages, safeSiblingCount)

  const goToPage = (nextPage: number) => {
    if (nextPage !== currentPage && nextPage >= 1 && nextPage <= totalPages) {
      onPageChange?.(nextPage)
    }
  }

  const buttonClasses =
    'inline-flex size-8 items-center justify-center rounded-sm border text-meta font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:cursor-not-allowed disabled:border-line disabled:bg-surface-header disabled:text-ink-disabled'

  return (
    <nav
      className={classNames(
        'flex min-h-pagination flex-wrap items-center justify-between gap-3 text-meta text-ink-muted',
        className,
      )}
      aria-label="分頁"
    >
      <p>
        顯示 {startItem}–{endItem} 筆，共 {totalCount} 筆
      </p>
      <div className="flex items-center gap-1">
        <button
          type="button"
          className={classNames(
            buttonClasses,
            'w-auto border-line bg-surface px-2 hover:bg-surface-header',
          )}
          disabled={currentPage === 1}
          onClick={() => goToPage(currentPage - 1)}
        >
          上一頁
        </button>
        {items.map((item) =>
          typeof item === 'number' ? (
            <button
              key={item}
              type="button"
              className={classNames(
                buttonClasses,
                item === currentPage
                  ? 'border-primary bg-primary text-on-primary'
                  : 'border-line bg-surface text-ink hover:bg-surface-header',
              )}
              aria-current={item === currentPage ? 'page' : undefined}
              aria-label={`第 ${item} 頁`}
              onClick={() => goToPage(item)}
            >
              {item}
            </button>
          ) : (
            <span
              key={item}
              className="inline-flex size-8 items-center justify-center"
              aria-hidden="true"
            >
              …
            </span>
          ),
        )}
        <button
          type="button"
          className={classNames(
            buttonClasses,
            'w-auto border-line bg-surface px-2 hover:bg-surface-header',
          )}
          disabled={currentPage === totalPages}
          onClick={() => goToPage(currentPage + 1)}
        >
          下一頁
        </button>
      </div>
    </nav>
  )
}
