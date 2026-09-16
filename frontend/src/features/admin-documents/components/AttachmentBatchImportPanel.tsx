import { Trash2, Upload } from "lucide-react";
import { useRef, useState, type ChangeEvent, type DragEvent } from "react";

import { ApiError } from "@/api/httpClient";
import {
  Alert,
  Badge,
  Button,
  FormField,
  Input,
  Table,
  type TableColumn,
} from "@/components/common";

import {
  draftAttachmentFromFile,
  fileIdentity,
  hasAllowedAttachmentExtension,
  type DraftAttachmentFile,
} from "../attachmentFileImport";
import {
  ALLOWED_ATTACHMENT_EXTENSIONS,
  createAttachmentFormSchema,
} from "../attachmentSchemas";
import { useCreateAttachment, useCreateAttachmentVersion } from "../queries";

type BatchRowStatus = "editing" | "success" | "failed";

interface BatchRow extends DraftAttachmentFile {
  status: BatchRowStatus;
  resultMessage: string | null;
}

function todayUtc(): string {
  return new Date().toISOString().slice(0, 10);
}

function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.detail ?? fallback;
  }
  return "目前無法連線到系統，請稍後再試。";
}

export interface AttachmentBatchImportPanelProps {
  documentId: string;
  documentNo: string;
  documentName: string;
  onCancel: () => void;
  onImported: (successCount: number) => void;
}

export function AttachmentBatchImportPanel({
  documentId,
  documentNo,
  documentName,
  onCancel,
  onImported,
}: AttachmentBatchImportPanelProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [rows, setRows] = useState<BatchRow[]>([]);
  const [effectiveDate, setEffectiveDate] = useState(todayUtc());
  const [isDragging, setIsDragging] = useState(false);
  const [fileError, setFileError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const createAttachment = useCreateAttachment();
  const createAttachmentVersion = useCreateAttachmentVersion();

  const canSave =
    rows.length > 0 && !submitted && !submitting && effectiveDate >= todayUtc();

  function triggerFileSelect() {
    fileInputRef.current?.click();
  }

  function addFiles(selectedFiles: File[]) {
    if (selectedFiles.length === 0) return;

    const accepted = selectedFiles.filter((file) =>
      hasAllowedAttachmentExtension(file.name),
    );
    const rejectedCount = selectedFiles.length - accepted.length;

    setRows((current) => {
      const existing = new Set(current.map((row) => fileIdentity(row.file)));
      const additions = accepted
        .filter((file) => !existing.has(fileIdentity(file)))
        .map((file) => ({
          ...draftAttachmentFromFile(file),
          status: "editing" as const,
          resultMessage: null,
        }));
      return [...current, ...additions];
    });
    setFileError(
      rejectedCount > 0
        ? `已略過 ${rejectedCount} 個不支援的檔案類型。`
        : undefined,
    );
  }

  function handleFileInputChange(event: ChangeEvent<HTMLInputElement>) {
    addFiles(Array.from(event.target.files ?? []));
    event.target.value = "";
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    addFiles(Array.from(event.dataTransfer.files));
  }

  function updateRow(
    rowId: string,
    patch: Partial<Pick<BatchRow, "attachmentNo" | "name">>,
  ) {
    setRows((current) =>
      current.map((row) => (row.id === rowId ? { ...row, ...patch } : row)),
    );
  }

  function removeRow(rowId: string) {
    setRows((current) => current.filter((row) => row.id !== rowId));
  }

  async function handleSave() {
    if (!canSave) return;

    setFileError(undefined);
    setSubmitting(true);

    const targets = rows;
    const nextRows = new Map<string, BatchRow>();
    let successCount = 0;

    for (const row of targets) {
      const parsed = createAttachmentFormSchema.safeParse({
        attachmentNo: row.attachmentNo,
        name: row.name,
      });
      if (!parsed.success) {
        const errors = parsed.error.flatten().fieldErrors;
        nextRows.set(row.id, {
          ...row,
          status: "failed",
          resultMessage:
            errors.attachmentNo?.[0] ?? errors.name?.[0] ?? "欄位格式不正確。",
        });
        continue;
      }

      try {
        const attachment = await createAttachment.mutateAsync({
          documentId,
          request: parsed.data,
        });
        await createAttachmentVersion.mutateAsync({
          documentId,
          attachmentId: attachment.attachmentId,
          input: { changeType: "MINOR", effectiveDate, file: row.file },
        });
        successCount += 1;
        nextRows.set(row.id, {
          ...row,
          status: "success",
          resultMessage: null,
        });
      } catch (error) {
        nextRows.set(row.id, {
          ...row,
          status: "failed",
          resultMessage: describeError(error, "建立或上傳失敗，請檢查後重試。"),
        });
      }
    }

    setRows(targets.map((row) => nextRows.get(row.id) ?? row));
    setSubmitting(false);
    setSubmitted(true);
    onImported(successCount);
  }

  const columns: ReadonlyArray<TableColumn<BatchRow>> = [
    {
      key: "file",
      header: "檔案",
      headerClassName: "min-w-56",
      render: (row) => (
        <div className="min-w-0">
          <p className="truncate font-medium text-ink" title={row.file.name}>
            {row.file.name}
          </p>
        </div>
      ),
    },
    {
      key: "attachmentNo",
      header: "附件編號",
      headerClassName: "w-48",
      render: (row) =>
        submitted ? (
          <span className="font-mono tabular">{row.attachmentNo || "－"}</span>
        ) : (
          <Input
            className="font-mono"
            value={row.attachmentNo}
            disabled={submitting}
            onChange={(event) =>
              updateRow(row.id, { attachmentNo: event.target.value })
            }
          />
        ),
    },
    {
      key: "name",
      header: "附件名稱",
      headerClassName: "min-w-48",
      render: (row) =>
        submitted ? (
          row.name || "－"
        ) : (
          <Input
            value={row.name}
            disabled={submitting}
            onChange={(event) =>
              updateRow(row.id, { name: event.target.value })
            }
          />
        ),
    },
    {
      key: "status",
      header: submitted ? "狀態" : "操作",
      headerClassName: "w-56",
      render: (row) => {
        if (!submitted) {
          return (
            <Button
              variant="ghost"
              size="sm"
              className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
              disabled={submitting}
              onClick={() => removeRow(row.id)}
            >
              <Trash2 className="size-4" aria-hidden="true" />
              移除
            </Button>
          );
        }
        if (row.status === "success") {
          return <Badge variant="success">成功</Badge>;
        }
        return (
          <div className="space-y-0.5">
            <Badge variant="danger">失敗</Badge>
            <p className="text-meta text-state-danger">{row.resultMessage}</p>
          </div>
        );
      },
    },
  ];

  return (
    <div className="space-y-4 p-4">
      <input
        ref={fileInputRef}
        type="file"
        multiple
        hidden
        accept={ALLOWED_ATTACHMENT_EXTENSIONS.join(",")}
        onChange={handleFileInputChange}
      />

      <div className="flex flex-wrap items-center justify-between gap-3 border border-line-strong bg-surface p-4">
        <div>
          <p className="text-label font-medium text-ink">批次新增附件</p>
          <p className="mt-1 text-meta text-ink-muted">
            {documentNo}｜{documentName}
            。解析後請確認檔案內容後附件編號與名稱送出。
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <FormField
            label="生效日期"
            htmlFor="attachment-batch-effective-date"
            className="w-40"
          >
            <Input
              id="attachment-batch-effective-date"
              type="date"
              min={todayUtc()}
              value={effectiveDate}
              disabled={submitting || submitted}
              onChange={(event) => setEffectiveDate(event.target.value)}
            />
          </FormField>
          <Button
            variant="secondary"
            disabled={submitting || submitted}
            onClick={triggerFileSelect}
          >
            選擇檔案
          </Button>
          <Button
            disabled={!canSave}
            loading={submitting}
            loadingText="建立並上傳中"
            onClick={() => void handleSave()}
          >
            建立並上傳
          </Button>
          <Button variant="ghost" onClick={onCancel}>
            返回
          </Button>
        </div>
      </div>

      <div
        role="button"
        tabIndex={0}
        aria-label="拖曳或點擊選擇附件檔案"
        className={`grid min-h-32 cursor-pointer place-items-center rounded-sm border-2 border-dashed px-6 py-6 text-center transition-colors ${
          isDragging
            ? "border-primary bg-primary-subtle"
            : "border-line-strong bg-canvas hover:border-primary hover:bg-primary-subtle"
        }`}
        onClick={triggerFileSelect}
        onKeyDown={(event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            triggerFileSelect();
          }
        }}
        onDragEnter={(event) => {
          event.preventDefault();
          setIsDragging(true);
        }}
        onDragOver={(event) => {
          event.preventDefault();
          setIsDragging(true);
        }}
        onDragLeave={(event) => {
          event.preventDefault();
          setIsDragging(false);
        }}
        onDrop={handleDrop}
      >
        <div>
          <span className="mx-auto mb-2 grid size-10 place-items-center rounded-md bg-primary-subtle text-primary">
            <Upload className="size-5" aria-hidden="true" />
          </span>
          <p className="font-semibold text-ink">
            拖曳多個檔案到這裡，或點擊選擇檔案
          </p>
          <p className="mt-1 text-fine text-ink-muted">
            支援 {ALLOWED_ATTACHMENT_EXTENSIONS.join("、")}
          </p>
        </div>
      </div>

      {fileError && (
        <Alert variant="error" title="無法建立附件">
          {fileError}
        </Alert>
      )}

      {submitted && (
        <Alert
          variant={
            rows.every((row) => row.status === "success")
              ? "success"
              : "warning"
          }
          title="匯入結果"
        >
          共 {rows.length} 筆，成功{" "}
          {rows.filter((row) => row.status === "success").length} 筆，失敗{" "}
          {rows.filter((row) => row.status === "failed").length} 筆。
        </Alert>
      )}

      <Table
        columns={columns}
        data={rows}
        getRowKey={(row) => row.id}
        caption="批次新增附件預覽"
        emptyMessage="尚未加入檔案，請拖曳或點擊選擇檔案。"
      />
    </div>
  );
}

export default AttachmentBatchImportPanel;
