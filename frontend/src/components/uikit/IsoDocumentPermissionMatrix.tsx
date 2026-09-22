import { useState } from 'react'

import { Badge, FormField, Select } from '@/components/common'

export type Company = {
  id: string
  name: string
}

export type Department = {
  id: string
  name: string
}

export type DocumentPermission = {
  id: string
  code: string
  name: string
  mainStatus: string
  attachmentStatus: string
  effectiveStatus: string
  departmentIds: string[]
}

type CompanyPermissionData = {
  departments: Department[]
  documents: DocumentPermission[]
}

const companies: Company[] = [
  { id: 'taipei-bus', name: '臺北客運' },
  { id: 'capital-bus', name: '首都客運' },
  { id: 'metro-bus', name: '大都會客運' },
]

const initialDataByCompany: Record<string, CompanyPermissionData> = {
  'taipei-bus': {
    departments: [
      { id: 'tp-general-manager', name: '總經理室' },
      { id: 'tp-human-resources', name: '人力資源部' },
      { id: 'tp-general-affairs', name: '總務部' },
      { id: 'tp-information', name: '資訊中心' },
      { id: 'tp-finance', name: '財務部' },
    ],
    documents: [
      {
        id: 'tp-cp-t-qm',
        code: 'CP-T-QM',
        name: '品質手冊',
        mainStatus: 'ISO管理程序有誤',
        attachmentStatus: '表單及附件有誤',
        effectiveStatus: '已生效',
        departmentIds: [
          'tp-general-manager',
          'tp-human-resources',
          'tp-general-affairs',
          'tp-information',
        ],
      },
      {
        id: 'tp-hr-i-01',
        code: 'HR-I-01',
        name: '人力資源管理規章',
        mainStatus: 'ISO管理程序正常',
        attachmentStatus: '表單及附件正常',
        effectiveStatus: '已生效',
        departmentIds: [
          'tp-general-manager',
          'tp-human-resources',
          'tp-information',
          'tp-finance',
        ],
      },
      {
        id: 'tp-it-s-02',
        code: 'IT-S-02',
        name: '資訊安全管理規範',
        mainStatus: 'ISO管理程序正常',
        attachmentStatus: '無表單及附件',
        effectiveStatus: '待生效',
        departmentIds: ['tp-general-manager', 'tp-information'],
      },
    ],
  },
  'capital-bus': {
    departments: [
      { id: 'capital-president', name: '總經理室' },
      { id: 'capital-operations', name: '營運部' },
      { id: 'capital-safety', name: '工安室' },
      { id: 'capital-maintenance', name: '修護部' },
    ],
    documents: [
      {
        id: 'capital-op-p-03',
        code: 'OP-P-03',
        name: '營運作業管理程序',
        mainStatus: 'ISO管理程序正常',
        attachmentStatus: '表單及附件正常',
        effectiveStatus: '已生效',
        departmentIds: ['capital-president', 'capital-operations', 'capital-safety'],
      },
      {
        id: 'capital-mt-i-07',
        code: 'MT-I-07',
        name: '車輛保養檢查規範',
        mainStatus: 'ISO管理程序有誤',
        attachmentStatus: '表單及附件正常',
        effectiveStatus: '已生效',
        departmentIds: ['capital-operations', 'capital-maintenance'],
      },
    ],
  },
  'metro-bus': {
    departments: [
      { id: 'metro-president', name: '總經理室' },
      { id: 'metro-planning', name: '企劃室' },
      { id: 'metro-audit', name: '稽核室' },
      { id: 'metro-customer-service', name: '客服中心' },
      { id: 'metro-accounting', name: '會計部' },
      { id: 'metro-information', name: '資訊中心' },
    ],
    documents: [
      {
        id: 'metro-cs-p-01',
        code: 'CS-P-01',
        name: '客訴處理程序',
        mainStatus: 'ISO管理程序正常',
        attachmentStatus: '表單及附件有誤',
        effectiveStatus: '已生效',
        departmentIds: ['metro-president', 'metro-audit', 'metro-customer-service'],
      },
      {
        id: 'metro-qa-m-01',
        code: 'QA-M-01',
        name: '品質管理手冊',
        mainStatus: 'ISO管理程序正常',
        attachmentStatus: '表單及附件正常',
        effectiveStatus: '已生效',
        departmentIds: [
          'metro-president',
          'metro-planning',
          'metro-audit',
          'metro-accounting',
          'metro-information',
        ],
      },
      {
        id: 'metro-fi-i-04',
        code: 'FI-I-04',
        name: '費用核銷作業規範',
        mainStatus: 'ISO管理程序正常',
        attachmentStatus: '無表單及附件',
        effectiveStatus: '已生效',
        departmentIds: ['metro-president', 'metro-accounting'],
      },
    ],
  },
}

function isErrorStatus(status: string): boolean {
  return status.includes('有誤')
}

function isPendingStatus(status: string): boolean {
  return status.includes('待')
}

export function IsoDocumentPermissionMatrix() {
  const [selectedCompanyId, setSelectedCompanyId] = useState(companies[0]?.id ?? '')
  const [dataByCompany, setDataByCompany] = useState(initialDataByCompany)
  const selectedCompany = companies.find((company) => company.id === selectedCompanyId)
  const selectedData = dataByCompany[selectedCompanyId]

  const togglePermission = (documentId: string, departmentId: string) => {
    setDataByCompany((current) => {
      const companyData = current[selectedCompanyId]
      if (companyData === undefined) return current

      return {
        ...current,
        [selectedCompanyId]: {
          ...companyData,
          documents: companyData.documents.map((document) => {
            if (document.id !== documentId) return document

            const hasPermission = document.departmentIds.includes(departmentId)
            return {
              ...document,
              departmentIds: hasPermission
                ? document.departmentIds.filter((id) => id !== departmentId)
                : [...document.departmentIds, departmentId],
            }
          }),
        },
      }
    })
  }

  return (
    <section className="overflow-hidden rounded-md border border-line-strong bg-surface">
      <div className="flex flex-col gap-4 border-b border-line bg-surface-header/50 p-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-section-label text-ink">ISO 文件權限矩陣</h2>
          <p className="mt-1 text-meta text-ink-muted">勾選可查看各ISO管理程序的部門。</p>
        </div>
        <FormField label="公司別" htmlFor="uikit-permission-company" className="w-full sm:w-64">
          <Select
            id="uikit-permission-company"
            value={selectedCompanyId}
            onChange={(event) => setSelectedCompanyId(event.target.value)}
          >
            {companies.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))}
          </Select>
        </FormField>
      </div>

      <div className="overflow-x-auto" tabIndex={0} aria-label={`${selectedCompany?.name ?? ''} ISO 文件權限表，可水平捲動`}>
        <table className="w-max min-w-full border-collapse text-left text-cell">
          <caption className="sr-only">{selectedCompany?.name} ISO 文件部門權限矩陣</caption>
          <thead className="bg-surface-header text-ink-muted">
            <tr>
              <th
                scope="col"
                className="sticky left-0 z-20 min-w-48 border-r border-b border-line-strong bg-surface-header px-4 py-3 text-table-header shadow-sticky-x"
              >
                ISO管理程序
              </th>
              <th scope="col" className="min-w-52 border-b border-line-strong px-4 py-3 text-table-header">
                狀態
              </th>
              {selectedData?.departments.map((department) => (
                <th
                  key={department.id}
                  scope="col"
                  className="min-w-36 border-b border-line-strong px-3 py-3 text-center text-table-header whitespace-nowrap"
                >
                  {department.name}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-line text-ink">
            {selectedData?.documents.map((document) => (
              <tr key={document.id} className="group hover:bg-surface-hover">
                <th
                  scope="row"
                  className="sticky left-0 z-10 border-r border-line bg-surface px-4 py-3 text-left shadow-sticky-x group-hover:bg-surface-hover"
                >
                  <span className="block font-mono text-code text-primary">{document.code}</span>
                  <span className="mt-0.5 block max-w-44 text-meta font-normal text-ink-muted">
                    {document.name}
                  </span>
                </th>
                <td className="px-4 py-3 align-middle">
                  <div className="flex min-w-44 flex-wrap gap-1.5">
                    <Badge variant={isErrorStatus(document.mainStatus) ? 'danger' : 'success'}>
                      {document.mainStatus}
                    </Badge>
                    <Badge variant={isErrorStatus(document.attachmentStatus) ? 'danger' : 'neutral'}>
                      {document.attachmentStatus}
                    </Badge>
                    <Badge variant={isPendingStatus(document.effectiveStatus) ? 'warning' : 'info'}>
                      {document.effectiveStatus}
                    </Badge>
                  </div>
                </td>
                {selectedData.departments.map((department) => {
                  const checked = document.departmentIds.includes(department.id)
                  return (
                    <td key={department.id} className="px-3 py-3 text-center align-middle">
                      <label className="inline-flex size-9 cursor-pointer items-center justify-center rounded-sm hover:bg-primary-subtle focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-primary">
                        <input
                          type="checkbox"
                          className="size-4 cursor-pointer rounded-xs border-line-strong accent-primary"
                          checked={checked}
                          onChange={() => togglePermission(document.id, department.id)}
                          aria-label={`${checked ? '取消' : '授予'}${department.name}查看 ${document.code} 的權限`}
                        />
                      </label>
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="border-t border-line px-4 py-3 text-fine text-ink-muted">
        此為 UI Kit 假資料；勾選變更僅保留於目前頁面的 React local state。
      </p>
    </section>
  )
}

export default IsoDocumentPermissionMatrix
