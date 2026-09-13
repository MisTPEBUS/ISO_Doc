import { useState, type ReactNode } from 'react'
import {
  Alert,
  AppHeader,
  Badge,
  Button,
  FormField,
  Input,
  Modal,
  Pagination,
  Select,
  SidebarNav,
  Spinner,
  Table,
  Textarea,
  type TableColumn,
  type SidebarNavGroup,
} from '../components/common'
import { IsoDocumentPermissionMatrix } from '../components/uikit/IsoDocumentPermissionMatrix'
import { IsoDocumentInlineEntryPreview } from '../components/uikit/IsoDocumentInlineEntryPreview'

interface PreviewSectionProps {
  title: string
  description?: string
  children: ReactNode
}

function PreviewSection({
  title,
  description,
  children,
}: PreviewSectionProps) {
  return (
    <section className="rounded-md border border-slate-200 bg-white p-5">
      <div className="mb-4 border-b border-slate-200 pb-3">
        <h2 className="text-base font-semibold text-slate-900">{title}</h2>
        {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
      </div>
      {children}
    </section>
  )
}

interface DocumentRow {
  id: string
  documentNo: string
  name: string
  version: string
  status: 'DRAFT' | 'PUBLISHED' | 'OBSOLETE'
  effectiveDate: string | null
}

interface AttachmentRow {
  id: string
  attachmentNo: string
  name: string
  hasFile: boolean
}

const documents: ReadonlyArray<DocumentRow> = [
  {
    id: 'document-1',
    documentNo: 'HR-GN-01',
    name: '人事管理辦法',
    version: '2.1',
    status: 'PUBLISHED',
    effectiveDate: '2026-08-01',
  },
  {
    id: 'document-2',
    documentNo: 'QA-PD-03',
    name: '文件管制作業程序',
    version: '3.0',
    status: 'DRAFT',
    effectiveDate: null,
  },
  {
    id: 'document-3',
    documentNo: 'IT-IS-02',
    name: '資訊安全管理規範',
    version: '1.4',
    status: 'OBSOLETE',
    effectiveDate: '2025-01-15',
  },
]

const statusBadge: Record<
  DocumentRow['status'],
  { label: string; variant: 'warning' | 'success' | 'neutral' }
> = {
  DRAFT: { label: '草稿', variant: 'warning' },
  PUBLISHED: { label: '已發布', variant: 'success' },
  OBSOLETE: { label: '已作廢', variant: 'neutral' },
}

const attachmentsByDocumentId: Readonly<Record<string, ReadonlyArray<AttachmentRow>>> = {
  'document-1': [
    {
      id: 'attachment-1',
      attachmentNo: 'HR-GN-01-01',
      name: '請假申請表',
      hasFile: true,
    },
    {
      id: 'attachment-2',
      attachmentNo: 'HR-GN-01-02',
      name: '加班申請表',
      hasFile: true,
    },
  ],
  'document-2': [
    {
      id: 'attachment-3',
      attachmentNo: 'QA-PD-03-01',
      name: '文件發行申請單',
      hasFile: false,
    },
  ],
}

const attachmentColumns: ReadonlyArray<TableColumn<AttachmentRow>> = [
  {
    key: 'status',
    header: '檔案狀態',
    render: (row) => (
      <Badge variant={row.hasFile ? 'success' : 'warning'}>
        {row.hasFile ? '可下載' : '待補檔'}
      </Badge>
    ),
  },
  {
    key: 'attachmentNo',
    header: '附件編號',
    render: (row) => <span className="font-mono text-slate-900">{row.attachmentNo}</span>,
  },
  {
    key: 'name',
    header: '附件名稱',
    render: (row) => row.name,
  },
]

const columns: ReadonlyArray<TableColumn<DocumentRow>> = [
  {
    key: 'status',
    header: '狀態',
    render: (row) => (
      <Badge variant={statusBadge[row.status].variant}>
        {statusBadge[row.status].label}
      </Badge>
    ),
  },
  {
    key: 'documentNo',
    header: '文件編號',
    render: (row, _rowIndex, context) => (
      <button
        type="button"
        className="inline-flex items-center gap-2 rounded-xs font-mono font-medium text-blue-700 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
        aria-expanded={context.expanded}
        aria-label={`${context.expanded ? '收合' : '展開'} ${row.documentNo} 附件清單`}
        onClick={context.toggleExpansion}
      >
        <svg
          viewBox="0 0 20 20"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.75"
          className={`size-4 transition-transform ${context.expanded ? 'rotate-90' : ''}`}
          aria-hidden="true"
        >
          <path d="m8 6 4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {row.documentNo}
      </button>
    ),
  },
  {
    key: 'name',
    header: '文件名稱',
    render: (row) => (
      <button
        type="button"
        className="font-medium text-blue-700 hover:underline focus-visible:rounded-xs focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
      >
        {row.name}
      </button>
    ),
  },
  {
    key: 'version',
    header: '版本',
    render: (row) => row.version,
  },
  {
    key: 'effectiveDate',
    header: '生效日期',
    render: (row) => row.effectiveDate ?? '—',
  },
]

const sidebarGroups: ReadonlyArray<SidebarNavGroup> = [
  {
    key: 'settings',
    label: '基礎設定',
    items: [
      { key: 'departments', label: '部門維護', href: '#departments', icon: '部' },
      { key: 'users', label: '使用者維護', href: '#users', icon: '人' },
    ],
  },
  {
    key: 'documents',
    label: '文件管理',
    items: [
      { key: 'documents', label: 'ISO 文件維護', href: '#documents', icon: '文' },
      { key: 'permissions', label: '權限維護', href: '#permissions', icon: '權' },
      { key: 'backup', label: 'ISO 文件備份', href: '#backup', icon: '備' },
    ],
  },
  {
    key: 'system',
    label: '系統',
    items: [{ key: 'about', label: '關於', href: '#about', icon: 'i' }],
  },
]

export default function ComponentPreview() {
  const [page, setPage] = useState(3)
  const [modalOpen, setModalOpen] = useState(false)
  const [expandedDocumentIds, setExpandedDocumentIds] = useState<ReadonlySet<string>>(
    () => new Set(['document-1']),
  )

  const toggleDocument = (document: DocumentRow) => {
    setExpandedDocumentIds((current) => {
      const next = new Set(current)
      if (next.has(document.id)) {
        next.delete(document.id)
      } else {
        next.add(document.id)
      }
      return next
    })
  }

  return (
    <main className="min-h-screen bg-slate-50 px-5 py-8 text-left text-slate-900">
      <div className="mx-auto max-w-6xl space-y-5">
        <header>
          <p className="text-sm font-medium text-blue-700">ISO 文件管理系統</p>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight text-slate-950">
            Common UI Kit Preview
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            暫時性元件展示頁，不掛入正式路由。
          </p>
        </header>

        <PreviewSection
          title="Application header"
          description="依靜態 prototype 呈現品牌、登入者資訊、模式切換、修改密碼與登出操作。"
        >
          <div className="overflow-hidden rounded-md border border-slate-300">
            <AppHeader
              companyName="首都客運"
              departmentName="資訊部"
              userName="王小明"
              modeLink={{ label: '前台查閱', href: '#' }}
              changePasswordHref="#"
              onLogout={() => undefined}
              sticky={false}
            />
          </div>
        </PreviewSection>

        <PreviewSection title="Button" description="四種語意 variant、兩種尺寸與處理中狀態。">
          <div className="flex flex-wrap items-center gap-2">
            <Button>主要操作</Button>
            <Button variant="secondary">次要操作</Button>
            <Button variant="danger">刪除</Button>
            <Button variant="ghost">取消</Button>
            <Button size="sm">小按鈕</Button>
            <Button loading loadingText="儲存中">
              儲存
            </Button>
            <Button disabled>停用狀態</Button>
          </div>
        </PreviewSection>

        <PreviewSection title="Form controls" description="控制項共用高度、圓角、focus ring 與錯誤邊框。">
          <div className="grid gap-4 md:grid-cols-2">
            <FormField
              label="文件名稱"
              htmlFor="preview-document-name"
              hint="請輸入正式文件名稱。"
              required
            >
              <Input
                id="preview-document-name"
                placeholder="例如：人事管理辦法"
                aria-describedby="preview-document-name-hint"
              />
            </FormField>
            <FormField
              label="文件編號"
              htmlFor="preview-document-no"
              error="文件編號已存在。"
              required
            >
              <Input
                id="preview-document-no"
                defaultValue="HR-GN-01"
                error
                aria-describedby="preview-document-no-error"
              />
            </FormField>
            <FormField label="文件狀態" htmlFor="preview-status">
              <Select id="preview-status" defaultValue="PUBLISHED">
                <option value="DRAFT">草稿</option>
                <option value="PUBLISHED">已發布</option>
                <option value="OBSOLETE">已作廢</option>
              </Select>
            </FormField>
            <FormField label="備註" htmlFor="preview-memo">
              <Textarea id="preview-memo" placeholder="選填" />
            </FormField>
            <FormField label="停用控制項" htmlFor="preview-disabled">
              <Input id="preview-disabled" defaultValue="不可編輯" disabled />
            </FormField>
            <FormField label="錯誤 Select" htmlFor="preview-select-error" error="請選擇公司。">
              <Select
                id="preview-select-error"
                defaultValue=""
                error
                aria-describedby="preview-select-error-error"
              >
                <option value="" disabled>
                  請選擇
                </option>
                <option value="company-a">公司 A</option>
              </Select>
            </FormField>
            <FormField label="停用 Select" htmlFor="preview-select-disabled">
              <Select id="preview-select-disabled" defaultValue="company-a" disabled>
                <option value="company-a">公司 A</option>
              </Select>
            </FormField>
            <FormField label="錯誤 Textarea" htmlFor="preview-textarea-error" error="備註不可超過限制長度。">
              <Textarea
                id="preview-textarea-error"
                defaultValue="需要修正的備註內容"
                error
                aria-describedby="preview-textarea-error-error"
              />
            </FormField>
          </div>
        </PreviewSection>

        <PreviewSection title="Select" description="原生選單語意搭配一致的 chevron、focus、error 與 disabled 外觀。">
          <div className="grid max-w-2xl gap-4 md:grid-cols-3">
            <Select defaultValue="all" aria-label="一般選單">
              <option value="all">全部狀態</option>
              <option value="published">已發布</option>
            </Select>
            <Select defaultValue="" error aria-label="錯誤選單">
              <option value="" disabled>
                請選擇公司
              </option>
            </Select>
            <Select defaultValue="locked" disabled aria-label="停用選單">
              <option value="locked">不可變更</option>
            </Select>
          </div>
        </PreviewSection>

        <PreviewSection
          title="Sidebar navigation"
          description="依靜態 prototype：深色導覽軌、群組標籤、active 狀態，窄螢幕自動收合為 icon rail。"
        >
          <div className="h-96 overflow-hidden rounded-md border border-slate-300 bg-slate-100">
            <SidebarNav groups={sidebarGroups} activeHref="#documents" />
          </div>
        </PreviewSection>

        <PreviewSection title="Badge" description="狀態以文字與顏色雙重表達，不單獨依賴顏色。">
          <div className="flex flex-wrap gap-2">
            <Badge>一般</Badge>
            <Badge variant="info">資訊</Badge>
            <Badge variant="success">PUBLISHED／已發布</Badge>
            <Badge variant="warning">DRAFT／草稿</Badge>
            <Badge variant="neutral">OBSOLETE／已作廢</Badge>
            <Badge variant="danger">停用</Badge>
          </div>
        </PreviewSection>

        <PreviewSection title="Alert" description="ProblemDetailsAlert 可在後續直接包裝此元件。">
          <div className="space-y-2">
            <Alert title="提示">變更會在儲存後生效。</Alert>
            <Alert variant="success" title="儲存成功">
              文件基本資料已更新。
            </Alert>
            <Alert variant="warning" title="請注意">
              尚未上傳 PDF，版本將保持草稿狀態。
            </Alert>
            <Alert variant="error" title="無法儲存">
              請檢查標示為錯誤的欄位。
            </Alert>
          </div>
        </PreviewSection>

        <PreviewSection title="Spinner" description="可獨立使用，也由 Button loading 狀態共用。">
          <div className="flex items-center gap-5 text-blue-600">
            <Spinner size="sm" label="載入小型內容" />
            <Spinner label="載入內容" />
            <Spinner size="lg" label="載入頁面" />
          </div>
        </PreviewSection>

        <PreviewSection
          title="Table — expandable rows"
          description="點擊主文件 row 可展開或收合附件清單；附件仍使用相同的 Table 元件呈現。"
        >
          <Table
            columns={columns}
            data={documents}
            getRowKey={(row) => row.id}
            caption="ISO 文件範例"
            expansion={{
              isExpanded: (row) => expandedDocumentIds.has(row.id),
              onToggle: toggleDocument,
              toggleOnRowClick: true,
              render: (row) => (
                <div>
                  <div className="mb-2 flex items-center justify-between gap-3">
                    <h3 className="text-sm font-semibold text-slate-800">
                      {row.documentNo} 附件清單
                    </h3>
                    <span className="text-xs text-slate-500">
                      共 {(attachmentsByDocumentId[row.id] ?? []).length} 筆
                    </span>
                  </div>
                  <Table
                    columns={attachmentColumns}
                    data={attachmentsByDocumentId[row.id] ?? []}
                    getRowKey={(attachment) => attachment.id}
                    emptyMessage="此版本沒有附件"
                    caption={`${row.documentNo} 附件清單`}
                  />
                </div>
              ),
            }}
          />
          <Pagination
            page={page}
            pageSize={10}
            totalCount={86}
            onPageChange={setPage}
            className="mt-2"
          />
        </PreviewSection>

        <PreviewSection
          title="ISO 文件登錄資料匯入"
          description="參考 inline entry prototype：Excel 拖放解析、欄位對應，以及可直接編輯、新增與刪除的文件資料表。"
        >
          <IsoDocumentInlineEntryPreview />
        </PreviewSection>

        <PreviewSection
          title="ISO 文件權限矩陣"
          description="公司切換、動態部門欄位、sticky 主文欄與 local state 勾選範例。"
        >
          <IsoDocumentPermissionMatrix />
        </PreviewSection>

        <div className="grid gap-5 lg:grid-cols-2">
          <PreviewSection title="Table — loading">
            <Table columns={columns} data={[]} loading skeletonRows={3} caption="載入文件" />
          </PreviewSection>
          <PreviewSection title="Table — empty">
            <Table
              columns={columns}
              data={[]}
              emptyMessage="查無符合條件的 ISO 文件"
              caption="空的文件清單"
            />
          </PreviewSection>
        </div>

        <PreviewSection title="Modal / Dialog" description="支援遮罩、ESC、原生 focus trap 與受控開關。">
          <Button onClick={() => setModalOpen(true)}>開啟重設密碼 Dialog</Button>
          <Modal
            open={modalOpen}
            onClose={() => setModalOpen(false)}
            title="重設使用者密碼"
            description="系統會產生一組暫時密碼。"
            footer={
              <>
                <Button variant="ghost" onClick={() => setModalOpen(false)}>
                  取消
                </Button>
                <Button onClick={() => setModalOpen(false)}>確認重設</Button>
              </>
            }
          >
            <Alert variant="warning">
              使用者下次登入時必須變更暫時密碼。
            </Alert>
          </Modal>
        </PreviewSection>
      </div>
    </main>
  )
}
