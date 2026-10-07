import { SORT_DIRECTION, type SortDirection, type SortParams } from '@/types/sort'

export interface TableSort<TKey extends string = string> {
  key: TKey
  direction: SortDirection
}

/** 點擊表頭的排序循環：未排序 → 升冪 → 降冪 → 未排序；改點其他欄位時從升冪開始。 */
export function nextTableSort<TKey extends string>(
  current: TableSort<TKey> | null | undefined,
  key: TKey,
): TableSort<TKey> | null {
  if (current?.key !== key) return { key, direction: SORT_DIRECTION.Asc }
  return current.direction === SORT_DIRECTION.Asc
    ? { key, direction: SORT_DIRECTION.Desc }
    : null
}

export function toSortParams<TKey extends string>(
  sort: TableSort<TKey> | null | undefined,
): SortParams<TKey> {
  return sort ? { sortBy: sort.key, sortDirection: sort.direction } : {}
}
