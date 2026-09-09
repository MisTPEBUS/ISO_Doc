import { Fragment, type ReactNode } from 'react'
import { classNames } from './classNames'

export interface TableCellContext {
  expandable: boolean
  expanded: boolean
  toggleExpansion: () => void
}

export interface TableColumn<T> {
  key: string
  header: ReactNode
  render: (row: T, rowIndex: number, context: TableCellContext) => ReactNode
  headerClassName?: string
  cellClassName?: string
}

export interface TableExpansion<T> {
  isExpanded: (row: T, rowIndex: number) => boolean
  onToggle: (row: T, rowIndex: number) => void
  render: (row: T, rowIndex: number) => ReactNode
  toggleOnRowClick?: boolean
}

export interface TableProps<T> {
  columns: ReadonlyArray<TableColumn<T>>
  data: ReadonlyArray<T>
  getRowKey?: (row: T, rowIndex: number) => string | number
  loading?: boolean
  skeletonRows?: number
  emptyMessage?: ReactNode
  caption?: string
  className?: string
  rowClassName?: string | ((row: T, rowIndex: number) => string | undefined)
  expansion?: TableExpansion<T>
}

export function Table<T>({
  columns,
  data,
  getRowKey = (_row, rowIndex) => rowIndex,
  loading = false,
  skeletonRows = 5,
  emptyMessage = '目前沒有資料',
  caption,
  className,
  rowClassName,
  expansion,
}: TableProps<T>) {
  const safeSkeletonRows = Math.max(1, skeletonRows)

  return (
    <div className={classNames('overflow-x-auto rounded-md border border-slate-200 bg-white', className)}>
      <table className="w-full border-collapse text-left text-sm" aria-busy={loading}>
        {caption && <caption className="sr-only">{caption}</caption>}
        <thead className="bg-slate-200 text-slate-800">
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                className={classNames(
                  'h-10 border-b border-slate-300 px-3 text-xs font-semibold tracking-wide whitespace-nowrap',
                  column.headerClassName,
                )}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-200 text-slate-700">
          {loading &&
            Array.from({ length: safeSkeletonRows }, (_, rowIndex) => (
              <tr key={`skeleton-${rowIndex}`}>
                {columns.map((column, columnIndex) => (
                  <td key={column.key} className="h-11 px-3">
                    <span
                      className={classNames(
                        'block h-3 animate-pulse rounded-xs bg-slate-200',
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
                className="h-28 px-4 text-center text-sm text-slate-500"
                colSpan={Math.max(columns.length, 1)}
              >
                {emptyMessage}
              </td>
            </tr>
          )}
          {!loading &&
            data.map((row, rowIndex) => {
              const rowKey = getRowKey(row, rowIndex)
              const isExpanded = expansion?.isExpanded(row, rowIndex) ?? false
              const resolvedRowClassName =
                typeof rowClassName === 'function'
                  ? rowClassName(row, rowIndex)
                  : rowClassName

              return (
                <Fragment key={rowKey}>
                  <tr
                    aria-expanded={expansion ? isExpanded : undefined}
                    tabIndex={expansion?.toggleOnRowClick ? 0 : undefined}
                    className={classNames(
                      'transition-colors hover:bg-blue-50/60',
                      expansion?.toggleOnRowClick &&
                        'cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-blue-600',
                      isExpanded && 'bg-blue-50/70',
                      resolvedRowClassName,
                    )}
                    onClick={(event) => {
                      if (!expansion?.toggleOnRowClick) return
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
                        className={classNames('h-11 px-3 align-middle', column.cellClassName)}
                      >
                        {column.render(row, rowIndex, {
                          expandable: expansion !== undefined,
                          expanded: isExpanded,
                          toggleExpansion: () => expansion?.onToggle(row, rowIndex),
                        })}
                      </td>
                    ))}
                  </tr>
                  {expansion && isExpanded && (
                    <tr>
                      <td colSpan={Math.max(columns.length, 1)} className="bg-slate-50 p-3">
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
