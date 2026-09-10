import type { AvailableDocumentResponse } from './types'

export interface MockDocumentAttachment {
  attachmentId: string
  attachmentNo: string
  name: string
  hasFile: boolean
}

export const MOCK_AVAILABLE_DOCUMENTS: ReadonlyArray<AvailableDocumentResponse> = [
  ['QP-HR-001', '人力資源管理程序書', '3.1', '2026-09-01', 18],
  ['QP-OPS-003', '車輛營運安全管理程序', '5.0', '2026-08-15', 32],
  ['WI-MA-012', '車輛定期保養作業指導書', '2.4', '2026-07-20', 14],
  ['QP-CS-002', '乘客服務與申訴處理程序', '4.2', '2026-07-01', 21],
  ['QP-SA-005', '職業安全衛生管理程序', '6.0', '2026-06-10', 27],
  ['FM-HR-018', '教育訓練紀錄表', '1.3', '2026-05-01', 2],
  ['QP-IT-004', '資訊設備與帳號管理程序', '2.0', '2026-04-15', 16],
  ['WI-DC-001', '文件編碼與版本管制作業指導書', '3.2', '2026-03-01', 11],
  ['FM-OPS-021', '駕駛員行車前檢查表', '2.1', '2026-02-16', 1],
  ['QP-EM-002', '緊急事故應變管理程序', '4.0', '2026-01-05', 24],
  ['QP-PU-006', '採購與供應商評鑑管理程序', '2.2', '2025-12-01', 19],
  ['FM-CS-009', '乘客意見處理紀錄表', '1.5', '2025-11-10', null],
].map(([documentNo, name, version, effectiveDate, pageCount], index) => ({
  documentId: `00000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
  documentNo: String(documentNo),
  name: String(name),
  companyName: '首都客運股份有限公司',
  currentVersion: {
    versionId: `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
    version: String(version),
    effectiveDate: String(effectiveDate),
    pageCount: typeof pageCount === 'number' ? pageCount : null,
  },
}))

export const MOCK_ATTACHMENTS_BY_DOCUMENT: Readonly<
  Record<string, ReadonlyArray<MockDocumentAttachment>>
> = {
  '00000000-0000-4000-8000-000000000001': [
    {
      attachmentId: '20000000-0000-4000-8000-000000000001',
      attachmentNo: 'FM-HR-001',
      name: '請假申請表',
      hasFile: true,
    },
    {
      attachmentId: '20000000-0000-4000-8000-000000000002',
      attachmentNo: 'FM-HR-002',
      name: '加班申請表',
      hasFile: true,
    },
  ],
  '00000000-0000-4000-8000-000000000002': [
    {
      attachmentId: '20000000-0000-4000-8000-000000000003',
      attachmentNo: 'FM-OPS-003',
      name: '營運安全事件通報表',
      hasFile: true,
    },
  ],
  '00000000-0000-4000-8000-000000000004': [
    {
      attachmentId: '20000000-0000-4000-8000-000000000004',
      attachmentNo: 'FM-CS-002',
      name: '乘客申訴處理紀錄表',
      hasFile: true,
    },
    {
      attachmentId: '20000000-0000-4000-8000-000000000005',
      attachmentNo: 'FM-CS-003',
      name: '服務改善追蹤表',
      hasFile: false,
    },
  ],
  '00000000-0000-4000-8000-000000000005': [],
}
