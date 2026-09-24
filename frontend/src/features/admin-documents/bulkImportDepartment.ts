import type { DeptResponse } from '@/features/departments/types'

export const DEPARTMENT_BY_DOCUMENT_PREFIX: Readonly<Record<string, string>> = {
  GM: '總經理室',
  GA: '總務部',
  OP: '業務部',
  MT: '機務部',
  FN: '財務部',
  IT: '資訊中心',
  HR: '人力資源部',
}

export function inferredDepartmentName(documentNo: string): string | undefined {
  const prefix = documentNo.trim().match(/^([A-Za-z]{2})(?:[^A-Za-z]|$)/)?.[1]?.toUpperCase()
  return prefix === undefined ? undefined : DEPARTMENT_BY_DOCUMENT_PREFIX[prefix]
}

/** Excel may contain an ID or an exact department name. Explicit values take precedence. */
export function resolveImportDept(
  rawDepartment: string,
  documentNo: string,
  departments: readonly DeptResponse[],
): { id: string | null; inferredName?: string; unknownExplicit: boolean } {
  const explicit = rawDepartment.trim()
  if (explicit !== '') {
    const matched = departments.find((dept) =>
      dept.id.toLowerCase() === explicit.toLowerCase() || dept.name === explicit,
    )
    return { id: matched?.id ?? null, unknownExplicit: matched === undefined }
  }

  const inferredName = inferredDepartmentName(documentNo)
  const matched = departments.find((dept) => dept.name === inferredName)
  return { id: matched?.id ?? null, inferredName, unknownExplicit: false }
}
