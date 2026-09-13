import { useRef, useState, type ChangeEvent } from 'react'

import { ApiError } from '@/api/httpClient'
import { Alert, Badge, Button, Input, Select, Table, type TableColumn } from '@/components/common'
import { useDepts } from '@/features/departments/queries'

import {
  MAX_BATCH_IMPORT_ROWS,
  parseUsersWorkbook,
  resolveDeptIds,
  toCreateUserRequest,
  type ParsedUserRow,
} from '../batchImport'
import { useBatchCreateUsers } from '../queries'
import {
  MANAGED_USER_ROLE,
  MANAGED_USER_ROLE_LABEL,
  isManagedUserRole,
  type ManagedUserRole,
} from '../types'

type BatchRowStatus = 'editing' | 'skipped' | 'success' | 'failed'

interface BatchRow extends ParsedUserRow {
  deptId: string | null
  role: ManagedUserRole
  status: BatchRowStatus
  resultMessage: string | null
}

export interface BatchSummary {
  total: number
  successCount: number
  failureCount: number
}

export interface UserBatchImportPanelProps {
  companyId: string
  isSystemAdmin: boolean
  onCancel: () => void
}

function formatServerErrors(errors: Record<string, string[]>): string {
  return Object.values(errors).flat().join('；')
}

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.detail ?? '無法匯入使用者資料。'
  }
  return '目前無法連線到系統，請稍後再試。'
}

export function UserBatchImportPanel({
  companyId,
  isSystemAdmin,
  onCancel,
}: UserBatchImportPanelProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [rows, setRows] = useState<BatchRow[]>([]);
  const [fileError, setFileError] = useState<string>();
  const [parsing, setParsing] = useState(false);
  const [summary, setSummary] = useState<BatchSummary>();
  const [submitted, setSubmitted] = useState(false);

  const departments = useDepts({ companyId, page: 1, pageSize: 100 }, true);
  const batchCreateUsers = useBatchCreateUsers();

  const departmentItems = departments.data?.items ?? [];
  const departmentNameById = new Map(
    departmentItems.map((department) => [department.id, department.name]),
  );

  const eligibleCount = rows.filter((row) => row.deptId !== null).length;
  const canSave =
    eligibleCount > 0 &&
    !submitted &&
    !batchCreateUsers.isPending &&
    !parsing;

  function triggerFileSelect() {
    fileInputRef.current?.click();
  }

  function updateRow(rowNumber: number, patch: Partial<BatchRow>) {
    setRows((current) =>
      current.map((row) =>
        row.rowNumber === rowNumber ? { ...row, ...patch } : row,
      ),
    );
  }

  function removeRow(rowNumber: number) {
    setRows((current) => current.filter((row) => row.rowNumber !== rowNumber));
  }

  async function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    setFileError(undefined);
    setSummary(undefined);
    setSubmitted(false);
    setParsing(true);
    try {
      const parsed = await parseUsersWorkbook(file);
      if (parsed.length === 0) {
        setFileError('檔案內沒有可匯入的資料列。');
        setRows([]);
        return;
      }
      if (parsed.length > MAX_BATCH_IMPORT_ROWS) {
        setFileError(
          `一次最多可匯入 ${MAX_BATCH_IMPORT_ROWS} 筆，這個檔案有 ${parsed.length} 筆，請拆分後再匯入。`,
        );
        setRows([]);
        return;
      }

      const resolved = resolveDeptIds(parsed, departmentItems);
      setRows(
        resolved.map((item) => ({
          ...item.parsed,
          deptId: item.deptId,
          role: MANAGED_USER_ROLE.User,
          status: 'editing',
          resultMessage: null,
        })),
      );
    } catch {
      setFileError('無法解析這個檔案，請確認格式為 .xlsx。');
      setRows([]);
    } finally {
      setParsing(false);
    }
  }

  function handleSave() {
    const eligible = rows.filter(
      (row): row is BatchRow & { deptId: string } => row.deptId !== null,
    );
    if (eligible.length === 0) return;

    setFileError(undefined);
    setRows((current) =>
      current.map((row) =>
        row.deptId === null
          ? { ...row, status: 'skipped', resultMessage: '尚未選擇部門，未送出。' }
          : row,
      ),
    );

    batchCreateUsers.mutate(
      {
        users: eligible.map((row) =>
          toCreateUserRequest(row, companyId, row.deptId, row.role),
        ),
      },
      {
        onSuccess: (response) => {
          const successByIndex = new Map(
            response.succeeded.map((item) => [item.index, item]),
          );
          const failedByIndex = new Map(
            response.failed.map((item) => [item.index, item]),
          );

          let eligibleIndex = 0;
          setRows((current) =>
            current.map((row) => {
              if (row.deptId === null) return row;

              eligibleIndex += 1;
              if (successByIndex.has(eligibleIndex)) {
                return { ...row, status: 'success', resultMessage: null };
              }
              const failure = failedByIndex.get(eligibleIndex);
              return {
                ...row,
                status: 'failed',
                resultMessage: failure
                  ? formatServerErrors(failure.errors)
                  : '未知錯誤。',
              };
            }),
          );

          setSummary({
            total: rows.length,
            successCount: response.successCount,
            failureCount: rows.length - response.successCount,
          });
          setSubmitted(true);
        },
        onError: (error) => setFileError(describeError(error)),
      },
    );
  }

  const columns: ReadonlyArray<TableColumn<BatchRow>> = [
    {
      key: 'rowNumber',
      header: '#',
      headerClassName: 'w-10 text-center',
      cellClassName: 'tabular text-ink-muted text-center',
      render: (row) => row.rowNumber,
    },
    {
      key: 'empno',
      header: '帳號',
      headerClassName: 'w-32',
      render: (row) =>
        submitted ? (
          <span className="tabular">{row.empno || '－'}</span>
        ) : (
          <Input
            value={row.empno}
            disabled={batchCreateUsers.isPending}
            onChange={(event) =>
              updateRow(row.rowNumber, { empno: event.target.value })
            }
          />
        ),
    },
    {
      key: 'name',
      header: '姓名',
      headerClassName: 'w-36',
      render: (row) =>
        submitted ? (
          row.name || '－'
        ) : (
          <Input
            value={row.name}
            disabled={batchCreateUsers.isPending}
            onChange={(event) =>
              updateRow(row.rowNumber, { name: event.target.value })
            }
          />
        ),
    },
    {
      key: 'email',
      header: '電子郵件',
      headerClassName: 'w-56',
      render: (row) =>
        submitted ? (
          <span className="text-meta text-ink-muted">{row.email || '－'}</span>
        ) : (
          <Input
            type="email"
            value={row.email}
            disabled={batchCreateUsers.isPending}
            onChange={(event) =>
              updateRow(row.rowNumber, { email: event.target.value })
            }
          />
        ),
    },
    {
      key: 'dept',
      header: '部門',
      headerClassName: 'w-44',
      render: (row) =>
        submitted ? (
          row.deptId !== null ? (
            (departmentNameById.get(row.deptId) ?? row.deptName)
          ) : (
            <span className="text-state-danger">未選擇部門</span>
          )
        ) : (
          <Select
            value={row.deptId ?? ''}
            error={row.deptId === null}
            disabled={batchCreateUsers.isPending || departments.isPending}
            onChange={(event) =>
              updateRow(row.rowNumber, { deptId: event.target.value || null })
            }
          >
            <option value="">請選擇部門</option>
            {departmentItems.map((department) => (
              <option key={department.id} value={department.id}>
                {department.name}
              </option>
            ))}
          </Select>
        ),
    },
    {
      key: 'role',
      header: '角色',
      headerClassName: 'w-32',
      render: (row) =>
        submitted ? (
          MANAGED_USER_ROLE_LABEL[row.role]
        ) : (
          <Select
            value={row.role}
            disabled={batchCreateUsers.isPending}
            onChange={(event) => {
              if (isManagedUserRole(event.target.value)) {
                updateRow(row.rowNumber, { role: event.target.value });
              }
            }}
          >
            <option value={MANAGED_USER_ROLE.User}>
              {MANAGED_USER_ROLE_LABEL.USER}
            </option>
            <option value={MANAGED_USER_ROLE.CompanyAdmin}>
              {MANAGED_USER_ROLE_LABEL.COMPANY_ADMIN}
            </option>
            {isSystemAdmin && (
              <option value={MANAGED_USER_ROLE.SystemAdmin}>
                {MANAGED_USER_ROLE_LABEL.SYSTEM_ADMIN}
              </option>
            )}
          </Select>
        ),
    },
    {
      key: 'password',
      header: '密碼',
      headerClassName: 'w-36',
      render: (row) =>
        submitted ? (
          <Badge variant={row.password ? 'info' : 'neutral'}>
            {row.password ? '已設定' : '未設定'}
          </Badge>
        ) : (
          <Input
            value={row.password}
            placeholder="留空表示未設定"
            disabled={batchCreateUsers.isPending}
            onChange={(event) =>
              updateRow(row.rowNumber, { password: event.target.value })
            }
          />
        ),
    },
    {
      key: 'status',
      header: submitted ? '狀態' : '操作',
      headerClassName: 'w-48',
      render: (row) => {
        if (!submitted) {
          return (
            <div className="space-y-0.5">
              <Button
                variant="ghost"
                size="sm"
                className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
                disabled={batchCreateUsers.isPending}
                onClick={() => removeRow(row.rowNumber)}
              >
                移除
              </Button>
              {row.deptId === null && (
                <p className="text-meta text-state-danger">請選擇部門</p>
              )}
            </div>
          );
        }
        if (row.status === 'success') {
          return <Badge variant="success">成功</Badge>;
        }
        if (row.status === 'failed' || row.status === 'skipped') {
          return (
            <div className="space-y-0.5">
              <Badge variant={row.status === 'failed' ? 'danger' : 'warning'}>
                {row.status === 'failed' ? '失敗' : '未送出'}
              </Badge>
              <p className="text-meta text-state-danger">
                {row.resultMessage}
              </p>
            </div>
          );
        }
        return <Badge variant="neutral">待送出</Badge>;
      },
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border border-line-strong bg-surface p-4">
        <div>
          <p className="text-label font-medium text-ink">批次匯入使用者</p>
          <p className="mt-1 text-meta text-ink-muted">
            Excel 欄位：帳號、姓名、電子郵件（選填）、部門、密碼（選填）。
            匯入後可直接調整欄位、移除資料列、改選部門或角色（預設為「使用者」），送出前資料不會被上傳。
            一次最多 {MAX_BATCH_IMPORT_ROWS} 筆。
          </p>
        </div>
        <div className="flex items-center gap-2">
          <input
            ref={fileInputRef}
            type="file"
            accept=".xlsx,.xls"
            hidden
            onChange={(event) => void handleFileChange(event)}
          />
          <Button
            variant="secondary"
            disabled={
              departments.isPending || batchCreateUsers.isPending || submitted
            }
            loading={parsing}
            loadingText="解析中"
            onClick={triggerFileSelect}
          >
            匯入 Excel
          </Button>
          <Button
            disabled={!canSave}
            loading={batchCreateUsers.isPending}
            loadingText="儲存中"
            onClick={handleSave}
          >
            儲存
          </Button>
          <Button variant="ghost" onClick={onCancel}>
            返回
          </Button>
        </div>
      </div>

      {fileError && (
        <Alert variant="error" title="無法匯入">
          {fileError}
        </Alert>
      )}

      {departments.isError && (
        <Alert variant="error" title="無法載入部門清單">
          請重新整理頁面後再試一次。
        </Alert>
      )}

      {summary && (
        <Alert
          variant={summary.failureCount === 0 ? 'success' : 'warning'}
          title="匯入結果"
        >
          共 {summary.total} 筆，成功 {summary.successCount} 筆，失敗{' '}
          {summary.failureCount} 筆。
        </Alert>
      )}

      <Table
        columns={columns}
        data={rows}
        loading={parsing}
        getRowKey={(row) => row.rowNumber}
        caption="批次匯入使用者預覽"
        emptyMessage="尚未匯入檔案，請點選「匯入 Excel」選擇檔案。"
      />
    </div>
  );
}

export default UserBatchImportPanel;
