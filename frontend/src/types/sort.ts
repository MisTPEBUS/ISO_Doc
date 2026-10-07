export const SORT_DIRECTION = {
  Asc: 'asc',
  Desc: 'desc',
} as const

export type SortDirection = (typeof SORT_DIRECTION)[keyof typeof SORT_DIRECTION]

/** 列表 API 共用的排序查詢參數（SPEC 第 4 節「列表排序」）；省略時由後端套用預設排序。 */
export interface SortParams<TField extends string = string> {
  sortBy?: TField
  sortDirection?: SortDirection
}
