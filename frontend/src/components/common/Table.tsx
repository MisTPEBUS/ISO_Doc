import { Fragment, type ReactNode } from 'react'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'

import { SORT_DIRECTION } from '@/types/sort'

import { classNames } from './classNames'
import { nextTableSort, type TableSort } from './tableSort'

export interface TableCellContext {
  expandable: boolean
  expanded: boolean
  toggleExpansion: () => void
}

export interface TableColumn<T, TSortKey extends string = string> {
  key: string
  header: ReactNode
  render: (row: T, rowIndex: number, context: TableCellContext) => ReactNode
  headerClassName?: string
  cellClassName?: string
  /** 設定後此欄表頭可點擊排序；值即為送往列表 API 的 sortBy。 */
  sortKey?: TSortKey
}

export interface TableExpansion<T> {
  canExpand?: (row: T, rowIndex: number) => boolean
  isExpanded: (row: T, rowIndex: number) => boolean
  onToggle: (row: T, rowIndex: number) => void
  render: (row: T, rowIndex: number) => ReactNode
  toggleOnRowClick?: boolean
}

export interface TableProps<T, TSortKey extends string = string> {
  columns: ReadonlyArray<TableColumn<T, TSortKey>>
  data: ReadonlyArray<T>
  getRowKey?: (row: T, rowIndex: number) => string | number
  loading?: boolean
  loadingLabel?: string
  skeletonRows?: number
  emptyMessage?: ReactNode
  caption?: string
  className?: string
  rowClassName?: string | ((row: T, rowIndex: number) => string | undefined)
  expansion?: TableExpansion<T>
  /** 目前排序；搭配 onSortChange 使用，未傳 onSortChange 時表頭不可點擊。 */
  sort?: TableSort<TSortKey> | null
  onSortChange?: (sort: TableSort<TSortKey> | null) => void
}

const ariaSortByDirection = {
  [SORT_DIRECTION.Asc]: 'ascending',
  [SORT_DIRECTION.Desc]: 'descending',
} as const

function SortIcon({ direction }: { direction: TableSort['direction'] | undefined }) {
  const className = 'size-3.5 shrink-0'
  if (direction === SORT_DIRECTION.Asc) return <ArrowUp aria-hidden="true" className={className} />
  if (direction === SORT_DIRECTION.Desc) return <ArrowDown aria-hidden="true" className={className} />
  return <ArrowUpDown aria-hidden="true" className={classNames(className, 'opacity-50')} />
}

export function Table<T, TSortKey extends string = string>({
  columns,
  data,
  getRowKey = (_row, rowIndex) => rowIndex,
  loading = false,
  loadingLabel = '資料載入中',
  skeletonRows = 5,
  emptyMessage = '目前沒有資料',
  caption,
  className,
  rowClassName,
  expansion,
  sort,
  onSortChange,
}: TableProps<T, TSortKey>) {
  const safeSkeletonRows = Math.max(1, skeletonRows)

  return (
    <div className={classNames('overflow-x-auto border border-line-strong bg-surface', className)}>
      <table className="w-full border-collapse text-left text-cell" aria-busy={loading}>
        {caption && <caption className="sr-only">{caption}</caption>}
        <thead className="bg-surface-header text-ink-muted">
          <tr>
            {columns.map((column) => {
              const sortKey = column.sortKey
              const direction =
                sortKey !== undefined && sort?.key === sortKey ? sort.direction : undefined

              return (
                <th
                  key={column.key}
                  scope="col"
                  aria-sort={direction ? ariaSortByDirection[direction] : undefined}
                  className={classNames(
                    'h-table-header border-b border-line-strong px-3 text-table-header whitespace-nowrap',
                    column.headerClassName,
                  )}
                >
                  {sortKey !== undefined && onSortChange ? (
                    <button
                      type="button"
                      className={classNames(
                        '-mx-1 inline-flex items-center gap-1 rounded-xs px-1 py-0.5 transition-colors hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary',
                        direction && 'text-ink',
                      )}
                      onClick={() => onSortChange(nextTableSort(sort, sortKey))}
                    >
                      {column.header}
                      <SortIcon direction={direction} />
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody className="divide-y divide-line text-ink">
          {loading && (
            <tr className="sr-only">
              <td colSpan={Math.max(columns.length, 1)} role="status">{loadingLabel}</td>
            </tr>
          )}
          {loading &&
            Array.from({ length: safeSkeletonRows }, (_, rowIndex) => (
              <tr key={`skeleton-${rowIndex}`}>
                {columns.map((column, columnIndex) => (
                  <td key={column.key} className="h-row px-3">
                    <span
                      className={classNames(
                        'block h-3 animate-pulse rounded-xs bg-surface-header',
                        columnIndex % 3 === 0
                          ? 'w-20'
                          : columnIndex % 3 === 1
                            ? 'w-32'
                            : 'w-24',
                      )}
                    />
                  </td>
                ))}
              </tr>
            ))}
          {!loading && data.length === 0 && (
            <tr>
              <td
                className="h-28 px-4 text-center text-cell text-ink-muted"
                colSpan={Math.max(columns.length, 1)}
              >
                {emptyMessage}
              </td>
            </tr>
          )}
          {!loading &&
            data.map((row, rowIndex) => {
              const rowKey = getRowKey(row, rowIndex)
              const isExpandable =
                expansion !== undefined &&
                (expansion.canExpand?.(row, rowIndex) ?? true)
              const isExpanded =
                isExpandable && (expansion?.isExpanded(row, rowIndex) ?? false)
              const resolvedRowClassName =
                typeof rowClassName === 'function'
                  ? rowClassName(row, rowIndex)
                  : rowClassName

              return (
                <Fragment key={rowKey}>
                  <tr
                    aria-expanded={isExpandable ? isExpanded : undefined}
                    tabIndex={isExpandable && expansion?.toggleOnRowClick ? 0 : undefined}
                    className={classNames(
                      'transition-colors',
                      isExpandable && 'hover:bg-surface-hover',
                      isExpandable && expansion?.toggleOnRowClick &&
                        'cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-primary',
                      isExpanded && 'border-l-2 border-l-primary bg-surface-hover',
                      resolvedRowClassName,
                    )}
                    onClick={(event) => {
                      if (!isExpandable || !expansion?.toggleOnRowClick) return
                      if (
                        event.target instanceof Element &&
                        event.target.closest('a, button, input, select, textarea, [role="button"]')
                      ) {
                        return
                      }
                      expansion.onToggle(row, rowIndex)
                    }}
                    onKeyDown={(event) => {
                      if (
                        !isExpandable ||
                        !expansion?.toggleOnRowClick ||
                        event.target !== event.currentTarget ||
                        (event.key !== 'Enter' && event.key !== ' ')
                      ) {
                        return
                      }
                      event.preventDefault()
                      expansion.onToggle(row, rowIndex)
                    }}
                  >
                    {columns.map((column) => (
                      <td
                        key={column.key}
                        className={classNames('h-row px-3 align-middle', column.cellClassName)}
                      >
                        {column.render(row, rowIndex, {
                          expandable: isExpandable,
                          expanded: isExpanded,
                          toggleExpansion: () => expansion?.onToggle(row, rowIndex),
                        })}
                      </td>
                    ))}
                  </tr>
                  {expansion && isExpandable && isExpanded && (
                    <tr>
                      <td colSpan={Math.max(columns.length, 1)} className="border-l-2 border-l-primary bg-surface p-3">
                        {expansion.render(row, rowIndex)}
                      </td>
                    </tr>
                  )}
                </Fragment>
              )
            })}
        </tbody>
      </table>
    </div>
  )
}
