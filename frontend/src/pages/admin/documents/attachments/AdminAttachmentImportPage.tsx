import {
  CheckCircle2,
  ChevronRight,
  Circle,
  FileStack,
  FolderOpen,
  LoaderCircle,
  MinusCircle,
  Paperclip,
  Search,
  Sparkles,
  Trash2,
  Upload,
  XCircle,
} from "lucide-react";
import {
  Fragment,
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type DragEvent,
} from "react";
import { useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import { buildAiImportCommitFormData } from "@/api/formData";
import { Alert, Badge, Button, Input, Select } from "@/components/common";
import { computeFileChecksum } from "@/features/admin-attachments/checksum";
import {
  attachmentFileGroups,
  FILE_ROLE,
  formatFileSize,
  GROUP_STATUS,
  parseAttachmentFile,
  PARSE_STATUS,
  recalculateFile,
  resolveMainCandidates,
  toExportedFiles,
  type AttachmentFileGroup,
  type FileRole,
  type GroupStatusFilter,
  type ParsedAttachmentFile,
} from "@/features/admin-attachments/folderImport";
import {
  useAnalyzeImport,
  useCommitImport,
} from "@/features/admin-attachments/queries";
import type { AnalyzeImportResponse } from "@/features/admin-attachments/types";
import { useCurrentUser } from "@/features/auth/queries";
import { useCompanyDeptOptions } from "@/features/departments/queries";
import { useCompanyIsoCategoryOptions } from "@/features/iso-categories/queries";

const ROLE_LABELS: Record<FileRole, string> = {
  [FILE_ROLE.Main]: "ISO管理程序",
  [FILE_ROLE.Attachment]: "表單及附件",
  [FILE_ROLE.MainCandidate]: "疑似ISO管理程序",
  [FILE_ROLE.Unresolved]: "未判斷",
};

const IMPORT_STATUS = {
  Running: "RUNNING",
  Completed: "COMPLETED",
} as const;

type ImportRunStatus = (typeof IMPORT_STATUS)[keyof typeof IMPORT_STATUS];

interface ImportProgress {
  status: ImportRunStatus;
  processedCount: number;
  totalCount: number;
  currentGroupLabel: string | null;
  processingFileIds: ReadonlySet<string>;
  completedFileIds: ReadonlySet<string>;
  failedFileIds: ReadonlySet<string>;
  skippedFileIds: ReadonlySet<string>;
  fileMessages: ReadonlyMap<string, string>;
}

interface CommitSummary {
  documents: { total: number; success: number; failed: number };
  attachments: {
    total: number;
    success: number;
    skipped: number;
    failed: number;
  };
}

interface AnalysisHint {
  suggestedVersion: string | null;
  effectiveDate: string | null;
  predictedAction: string;
}

interface DocumentMetadataSelection {
  isoCategoryId: string;
  deptId: string;
}

function fileIdentity(file: ParsedAttachmentFile): string {
  return `${file.relativePath}|${file.file.size}|${file.file.lastModified}`;
}

function isFileRole(value: string): value is FileRole {
  return Object.values(FILE_ROLE).some((role) => role === value);
}

function isGroupStatusFilter(value: string): value is GroupStatusFilter {
  return Object.values(GROUP_STATUS).some((status) => status === value);
}

function statusBadge(group: AttachmentFileGroup) {
  if (group.status === GROUP_STATUS.Ok)
    return <Badge variant="success">完整</Badge>;
  if (group.status === GROUP_STATUS.MissingMain)
    return <Badge variant="danger">缺ISO管理程序</Badge>;
  return <Badge variant="warning">待確認</Badge>;
}

function duplicateMainFileIds(
  groups: readonly AttachmentFileGroup[],
): ReadonlySet<string> {
  const ids = new Set<string>();
  for (const group of groups) {
    const mains = group.files.filter((file) => file.role === FILE_ROLE.Main);
    if (mains.length <= 1) continue;
    for (const file of mains) ids.add(file.id);
  }
  return ids;
}

function describeError(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.detail ?? fallback;
  }
  return "目前無法連線到系統，請稍後再試。";
}

function formatFieldErrors(errors: Record<string, string[]>): string {
  return Object.values(errors).flat().join("；");
}

function describeSkipReason(reason: string): string {
  if (reason === "UNCHANGED") return "內容與現有版本相同，未建立新版本。";
  if (reason === "PARENT_DOCUMENT_FAILED")
    return "對應的ISO管理程序未成功建立，已略過。";
  return reason;
}

function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

function buildAnalysisHints(
  analysis: AnalyzeImportResponse | undefined,
): Map<string, AnalysisHint> {
  const hints = new Map<string, AnalysisHint>();
  if (analysis === undefined) return hints;

  for (const document of analysis.documents) {
    if (document.mainFile) {
      hints.set(document.mainFile.relativePath, {
        suggestedVersion: document.suggestedVersion,
        effectiveDate: document.effectiveDate,
        predictedAction: document.predictedAction,
      });
    }
    for (const attachment of document.attachments) {
      hints.set(attachment.relativePath, {
        suggestedVersion: attachment.suggestedVersion,
        effectiveDate: attachment.effectiveDate,
        predictedAction: attachment.predictedAction,
      });
    }
  }

  return hints;
}

function applyAnalysisToFiles(
  files: ParsedAttachmentFile[],
  response: AnalyzeImportResponse,
): ParsedAttachmentFile[] {
  const updates = new Map<string, Partial<ParsedAttachmentFile>>();

  for (const document of response.documents) {
    if (document.mainFile) {
      updates.set(document.mainFile.relativePath, {
        role: FILE_ROLE.Main,
        documentCode: document.documentNo,
        displayName: document.name,
        attachmentCode: "",
      });
    }
    for (const attachment of document.attachments) {
      updates.set(attachment.relativePath, {
        role: FILE_ROLE.Attachment,
        documentCode: document.documentNo,
        attachmentCode: attachment.attachmentNo ?? "",
        displayName: attachment.name,
      });
    }
  }

  for (const unresolved of response.unresolved) {
    if (!updates.has(unresolved.relativePath)) {
      updates.set(unresolved.relativePath, { role: FILE_ROLE.Unresolved });
    }
  }

  return files.map((file) => {
    const patch = updates.get(file.relativePath);
    return patch ? recalculateFile({ ...file, ...patch }) : file;
  });
}

export function AdminAttachmentImportPage() {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement | null>(null);
  const submissionRunRef = useRef(0);
  const [files, setFiles] = useState<ParsedAttachmentFile[]>([]);
  const [openGroupKeys, setOpenGroupKeys] = useState<ReadonlySet<string>>(
    () => new Set(),
  );
  const [keyword, setKeyword] = useState("");
  const [statusFilter, setStatusFilter] = useState<GroupStatusFilter>(
    GROUP_STATUS.All,
  );
  const [isDragging, setIsDragging] = useState(false);
  const [message, setMessage] = useState<string>();
  const [analysis, setAnalysis] = useState<AnalyzeImportResponse>();
  const [metadataByMainId, setMetadataByMainId] = useState<
    Record<string, DocumentMetadataSelection>
  >({});
  const [importProgress, setImportProgress] = useState<ImportProgress>();
  const [summary, setSummary] = useState<CommitSummary>();
  const [submitError, setSubmitError] = useState<string>();

  const currentUser = useCurrentUser();
  const companyId = currentUser.data?.companyId;
  const analyzeImport = useAnalyzeImport();
  const commitImport = useCommitImport();
  const isoCategories = useCompanyIsoCategoryOptions(companyId);
  const depts = useCompanyDeptOptions(companyId);

  useEffect(
    () => () => {
      submissionRunRef.current += 1;
    },
    [],
  );

  const confirmedMissingMainCodes = new Set(
    analysis?.documents
      .filter(
        (document) =>
          document.mainFile === null &&
          document.attachments.some(
            (attachment) =>
              attachment.predictedAction === "NEW_ATTACHMENT" ||
              attachment.predictedAction === "NEW_VERSION",
          ),
      )
      .map((document) => document.documentNo) ?? [],
  );
  const groups = attachmentFileGroups(files, confirmedMissingMainCodes);
  const analysisHints = buildAnalysisHints(analysis);
  const normalizedKeyword = keyword.trim().toLowerCase();
  const visibleGroups = groups.filter((group) => {
    if (statusFilter !== GROUP_STATUS.All && group.status !== statusFilter)
      return false;
    if (normalizedKeyword === "") return true;
    return [
      group.documentCode,
      group.sourceFolder,
      ...group.files.flatMap((file) => [
        file.file.name,
        file.relativePath,
        file.displayName,
        file.attachmentCode,
      ]),
    ]
      .join(" ")
      .toLowerCase()
      .includes(normalizedKeyword);
  });

  const mainCount = files.filter((file) => file.role === FILE_ROLE.Main).length;
  const attachmentCount = files.filter(
    (file) => file.role === FILE_ROLE.Attachment,
  ).length;
  const duplicateMainIds = duplicateMainFileIds(groups);
  const completeGroupCount = groups.filter(
    (group) => group.status === GROUP_STATUS.Ok,
  ).length;
  const warningCount = groups.filter(
    (group) => group.status === GROUP_STATUS.Warning,
  ).length;
  const missingMainCount = groups.filter(
    (group) => group.status === GROUP_STATUS.MissingMain,
  ).length;
  const isAnalyzing = analyzeImport.isPending;
  const isSubmitting = importProgress?.status === IMPORT_STATUS.Running;
  const isBusy = isSubmitting || isAnalyzing;
  const canSubmit = files.length > 0 && completeGroupCount === groups.length;
  const progressPercentage =
    importProgress === undefined
      ? 0
      : Math.round(
          (importProgress.processedCount / importProgress.totalCount) * 100,
        );

  function resetProgress() {
    submissionRunRef.current += 1;
    setImportProgress(undefined);
    setSubmitError(undefined);
    setSummary(undefined);
    setAnalysis(undefined);
    setStatusFilter((current) =>
      current === GROUP_STATUS.MissingMain ? GROUP_STATUS.All : current,
    );
  }

  function updateDocumentMetadata(
    mainFileId: string,
    field: keyof DocumentMetadataSelection,
    value: string,
  ) {
    submissionRunRef.current += 1;
    setImportProgress(undefined);
    setSubmitError(undefined);
    setSummary(undefined);
    setMetadataByMainId((current) => ({
      ...current,
      [mainFileId]: {
        isoCategoryId: current[mainFileId]?.isoCategoryId ?? "",
        deptId: current[mainFileId]?.deptId ?? "",
        [field]: value,
      },
    }));
  }

  function addFiles(selectedFiles: File[]) {
    if (selectedFiles.length === 0 || isBusy) return;

    const existing = new Set(files.map(fileIdentity));
    const additions = selectedFiles
      .map(parseAttachmentFile)
      .filter((file) => !existing.has(fileIdentity(file)));
    const nextFiles = resolveMainCandidates([...files, ...additions]);
    const nextGroups = attachmentFileGroups(nextFiles);
    setFiles(nextFiles);
    resetProgress();
    setOpenGroupKeys(new Set(nextGroups.map((group) => group.key)));
    setMessage(
      additions.length === 0
        ? "選取的檔案皆已存在，未重複加入。"
        : `已加入 ${additions.length} 個檔案，請確認自動辨識結果。`,
    );
  }

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    addFiles(Array.from(event.target.files ?? []));
    event.target.value = "";
  }

  function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDragging(false);
    if (isBusy) return;
    addFiles(Array.from(event.dataTransfer.files));
  }

  function updateFile(id: string, patch: Partial<ParsedAttachmentFile>) {
    if (isBusy) return;
    resetProgress();
    setFiles((current) => {
      const targetGroup = attachmentFileGroups(current).find((group) =>
        group.files.some((file) => file.id === id),
      );
      const targetGroupIds = new Set(
        targetGroup?.files.map((file) => file.id) ?? [],
      );
      const nextFiles = current.map((file) => {
        if (file.id === id) {
          return recalculateFile({ ...file, ...patch });
        }
        if (
          patch.role === FILE_ROLE.Main &&
          targetGroupIds.has(file.id) &&
          file.role === FILE_ROLE.Main
        ) {
          return recalculateFile({
            ...file,
            role: FILE_ROLE.Attachment,
            attachmentCode: "",
            attachmentSequence: "",
          });
        }
        return file;
      });

      return resolveMainCandidates(nextFiles);
    });
  }

  function updateAttachmentCode(file: ParsedAttachmentFile, value: string) {
    const attachmentCode = value.trim().toUpperCase();
    const match = attachmentCode.match(/^([A-Z]+-[A-Z]+-\d+)-(\d+[A-Z]?)$/i);
    updateFile(file.id, {
      attachmentCode,
      attachmentSequence: match?.[2]?.toUpperCase() ?? file.attachmentSequence,
      documentCode: match?.[1]?.toUpperCase() ?? file.documentCode,
    });
  }

  function updateGroupDocumentCode(group: AttachmentFileGroup, value: string) {
    if (isBusy) return;
    resetProgress();
    const documentCode = value.trim().toUpperCase();
    const ids = new Set(group.files.map((file) => file.id));
    const nextFiles = files.map((file) => {
      if (!ids.has(file.id)) return file;
      const attachmentCode =
        file.attachmentSequence === ""
          ? file.attachmentCode
          : `${documentCode}-${file.attachmentSequence}`;
      return recalculateFile({ ...file, documentCode, attachmentCode });
    });
    const resolved = resolveMainCandidates(nextFiles);
    setFiles(resolved);
    setOpenGroupKeys(
      new Set(attachmentFileGroups(resolved).map((nextGroup) => nextGroup.key)),
    );
  }

  function toggleGroup(key: string) {
    setOpenGroupKeys((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function exportJson() {
    const blob = new Blob([JSON.stringify(toExportedFiles(files), null, 2)], {
      type: "application/json;charset=utf-8",
    });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "iso-attachment-import.json";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  async function runAiAnalysis() {
    if (files.length === 0 || isBusy) return;
    if (companyId === undefined) {
      setMessage("找不到目前使用者的公司資訊，請重新登入後再試。");
      return;
    }

    resetProgress();
    setMessage(undefined);

    try {
      const descriptors = await Promise.all(
        files.map(async (file) => ({
          originalFileName: file.file.name,
          relativePath: file.relativePath,
          size: file.file.size,
          role: file.role,
          documentNo: file.documentCode,
          attachmentNo: file.attachmentCode,
          displayName: file.displayName,
          extension: file.extension,
          parseStatus: file.parseStatus,
          checksum: await computeFileChecksum(file.file),
        })),
      );

      const response = await analyzeImport.mutateAsync({
        companyId,
        files: descriptors,
      });
      setAnalysis(response);
      setFiles((current) =>
        resolveMainCandidates(applyAnalysisToFiles(current, response)),
      );
      setMessage(
        response.unresolved.length === 0
          ? `AI 分析完成，共比對 ${response.documents.length} 個ISO管理程序群組。`
          : `AI 分析完成，共比對 ${response.documents.length} 個ISO管理程序群組，${response.unresolved.length} 個檔案仍待人工指定。`,
      );
    } catch (error) {
      alert(error);
      setMessage(describeError(error, "無法完成 AI 分析。"));
    }
  }

  async function runCommit() {
    if (!canSubmit || isBusy) return;
    if (companyId === undefined) {
      setMessage("找不到目前使用者的公司資訊，請重新登入後再試。");
      return;
    }

    const queue = groups;
    const runId = submissionRunRef.current + 1;
    submissionRunRef.current = runId;
    const completedFileIds = new Set<string>();
    const failedFileIds = new Set<string>();
    const skippedFileIds = new Set<string>();
    const fileMessages = new Map<string, string>();
    setMessage(undefined);
    setSubmitError(undefined);
    setSummary(undefined);

    const documentTotals = { total: 0, success: 0, failed: 0 };
    const attachmentTotals = { total: 0, success: 0, skipped: 0, failed: 0 };

    try {
      for (let index = 0; index < queue.length; index += 1) {
        const group = queue[index];
        if (group === undefined || submissionRunRef.current !== runId) return;

        setImportProgress({
          status: IMPORT_STATUS.Running,
          processedCount: index,
          totalCount: queue.length,
          currentGroupLabel:
            group.documentCode || group.sourceFolder || "未命名群組",
          processingFileIds: new Set(group.files.map((file) => file.id)),
          completedFileIds: new Set(completedFileIds),
          failedFileIds: new Set(failedFileIds),
          skippedFileIds: new Set(skippedFileIds),
          fileMessages: new Map(fileMessages),
        });

        const mainFile = group.files.find(
          (file) => file.role === FILE_ROLE.Main,
        );
        const attachmentFiles = group.files.filter(
          (file) => file.role === FILE_ROLE.Attachment,
        );
        const mainHint = mainFile
          ? analysisHints.get(mainFile.relativePath)
          : undefined;
        const metadata = mainFile ? metadataByMainId[mainFile.id] : undefined;

        const formData = buildAiImportCommitFormData({
          companyId,
          analysisId: analysis?.analysisId,
          documents: [
            {
              documentNo: group.documentCode,
              name: mainFile?.displayName || group.documentCode,
              isoCategoryId: metadata?.isoCategoryId || undefined,
              deptId: metadata?.deptId || undefined,
              version: mainHint?.suggestedVersion ?? "1.0",
              effectiveDate: mainHint?.effectiveDate ?? todayIsoDate(),
              mainFile: mainFile?.file,
              attachments: attachmentFiles.map((file) => {
                const hint = analysisHints.get(file.relativePath);
                return {
                  attachmentNo: file.attachmentCode,
                  name: file.displayName,
                  version: hint?.suggestedVersion ?? "1.0",
                  effectiveDate: hint?.effectiveDate ?? undefined,
                  file: file.file,
                };
              }),
            },
          ],
        });

        try {
          const response = await commitImport.mutateAsync(formData);
          if (submissionRunRef.current !== runId) return;

          documentTotals.total += response.documents.total;
          documentTotals.success += response.documents.successCount;
          documentTotals.failed += response.documents.failureCount;
          attachmentTotals.total += response.attachments.total;
          attachmentTotals.success += response.attachments.successCount;
          attachmentTotals.skipped += response.attachments.skippedCount;
          attachmentTotals.failed += response.attachments.failureCount;

          const documentFailure = response.documents.failed[0];
          const failedByNo = new Map(
            response.attachments.failed.map((item) => [
              item.attachmentNo,
              item,
            ]),
          );
          const skippedByNo = new Map(
            response.attachments.skipped.map((item) => [
              item.attachmentNo,
              item,
            ]),
          );

          for (const file of group.files) {
            if (file.role === FILE_ROLE.Main) {
              if (documentFailure) {
                failedFileIds.add(file.id);
                fileMessages.set(
                  file.id,
                  formatFieldErrors(documentFailure.errors),
                );
              } else {
                completedFileIds.add(file.id);
              }
              continue;
            }

            const failure = failedByNo.get(file.attachmentCode);
            const skipped = skippedByNo.get(file.attachmentCode);
            if (failure) {
              failedFileIds.add(file.id);
              fileMessages.set(file.id, formatFieldErrors(failure.errors));
            } else if (skipped) {
              skippedFileIds.add(file.id);
              fileMessages.set(file.id, describeSkipReason(skipped.reason));
            } else {
              completedFileIds.add(file.id);
            }
          }
        } catch (error) {
          documentTotals.total += 1;
          documentTotals.failed += 1;
          const groupErrorMessage = describeError(
            error,
            "無法完成這個ISO管理程序群組的儲存。",
          );
          for (const file of group.files) {
            failedFileIds.add(file.id);
            fileMessages.set(file.id, groupErrorMessage);
          }
        }

        setImportProgress({
          status: IMPORT_STATUS.Running,
          processedCount: index + 1,
          totalCount: queue.length,
          currentGroupLabel: null,
          processingFileIds: new Set(),
          completedFileIds: new Set(completedFileIds),
          failedFileIds: new Set(failedFileIds),
          skippedFileIds: new Set(skippedFileIds),
          fileMessages: new Map(fileMessages),
        });
      }

      setImportProgress({
        status: IMPORT_STATUS.Completed,
        processedCount: queue.length,
        totalCount: queue.length,
        currentGroupLabel: null,
        processingFileIds: new Set(),
        completedFileIds: new Set(completedFileIds),
        failedFileIds: new Set(failedFileIds),
        skippedFileIds: new Set(skippedFileIds),
        fileMessages: new Map(fileMessages),
      });
      setSummary({
        documents: documentTotals,
        attachments: attachmentTotals,
      });
    } catch {
      setImportProgress(undefined);
      setSubmitError("無法完成儲存，請重新執行。");
    }
  }

  return (
    <section className="pb-72 sm:pb-56 lg:pb-40">
      <input
        ref={fileInputRef}
        type="file"
        multiple
        hidden
        disabled={isBusy}
        onChange={handleFileChange}
      />
      <input
        ref={(element) => {
          folderInputRef.current = element;
          element?.setAttribute("webkitdirectory", "");
          element?.setAttribute("directory", "");
        }}
        type="file"
        multiple
        hidden
        disabled={isBusy}
        onChange={handleFileChange}
      />

      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="mb-1 text-label font-medium text-primary">文件管理</p>
          <h1 className="text-page-title text-ink">批次新增表單及附件</h1>
        </div>
        <Button
          variant="secondary"
          disabled={isBusy}
          onClick={() => navigate("/admin/documents")}
        >
          返回 ISO 文件維護
        </Button>
      </div>

      <Alert className="mb-4" variant="info" title="AI 輔助批次匯入">
        選取檔案或資料夾後確認辨識結果；「AI
        分析」會呼叫後端修正待確認項目並取得建議版號，「儲存」會依ISO管理程序群組逐批寫入資料庫與檔案。
      </Alert>

      {(isoCategories.isError || depts.isError) && (
        <Alert className="mb-4" variant="warning" title="部分選項無法載入">
          品質系統或發行部門清單載入失敗；未指定時新文件會留空，既有文件保留原值。
        </Alert>
      )}

      <section className="border border-line-strong bg-surface">
        <div
          role="button"
          tabIndex={isBusy ? -1 : 0}
          aria-disabled={isBusy}
          aria-label="拖曳檔案到這裡"
          className={`m-4 grid min-h-48 place-items-center rounded-sm border-2 border-dashed px-6 py-8 text-center transition-colors ${
            isBusy
              ? "cursor-not-allowed border-line bg-surface-header text-ink-disabled"
              : isDragging
                ? "cursor-pointer border-primary bg-primary-subtle"
                : "cursor-pointer border-line-strong bg-canvas hover:border-primary hover:bg-primary-subtle"
          }`}
          onClick={() => {
            if (!isBusy) fileInputRef.current?.click();
          }}
          onKeyDown={(event) => {
            if (!isBusy && (event.key === "Enter" || event.key === " ")) {
              event.preventDefault();
              fileInputRef.current?.click();
            }
          }}
          onDragEnter={(event) => {
            event.preventDefault();
            if (!isBusy) setIsDragging(true);
          }}
          onDragOver={(event) => {
            event.preventDefault();
            if (!isBusy) setIsDragging(true);
          }}
          onDragLeave={(event) => {
            event.preventDefault();
            setIsDragging(false);
          }}
          onDrop={handleDrop}
        >
          <div>
            <span className="mx-auto mb-3 grid size-12 place-items-center rounded-md bg-primary-subtle text-primary">
              <Upload className="size-6" aria-hidden="true" />
            </span>
            <p className="text-section-label text-ink">拖曳多個檔案到這裡</p>
            <p className="mt-1 text-meta text-ink-muted">
              資料夾請使用下方「選擇資料夾」，以保留相對路徑及ISO管理程序群組。
            </p>
            <div className="mt-4 flex flex-wrap justify-center gap-2">
              <Button
                variant="secondary"
                disabled={isBusy}
                onClick={(event) => {
                  event.stopPropagation();
                  fileInputRef.current?.click();
                }}
              >
                <FileStack className="size-4" aria-hidden="true" />
                選擇檔案
              </Button>
              <Button
                variant="secondary"
                disabled={isBusy}
                onClick={(event) => {
                  event.stopPropagation();
                  folderInputRef.current?.click();
                }}
              >
                <FolderOpen className="size-4" aria-hidden="true" />
                選擇資料夾
              </Button>
            </div>
            <p className="mt-4 text-fine text-ink-muted">
              範例：GA-P-01文件與紀錄管制程序.pdf、GA-P-01-01B文件登記表.xls
            </p>
          </div>
        </div>

        {message && (
          <p className="px-4 pb-4 text-meta text-ink-muted">{message}</p>
        )}

        <div className="fixed right-0 bottom-0 left-[var(--admin-sidebar-width)] z-40 border-t border-line-strong bg-surface">
          <div className="flex max-h-[40dvh] flex-wrap items-center justify-between gap-3 overflow-y-auto px-4 py-3 lg:px-6">
            <div className="flex flex-wrap gap-2">
              <Badge variant="neutral">ISO管理程序群組 {groups.length}</Badge>
              <Badge variant="neutral">檔案 {files.length}</Badge>
              <Badge variant="success">完整 {completeGroupCount}</Badge>
              <Badge variant="success">ISO管理程序 {mainCount}</Badge>
              <Badge variant="info">表單及附件 {attachmentCount}</Badge>
              <Badge variant="warning">待確認 {warningCount}</Badge>
              {missingMainCount > 0 && (
                <Badge variant="danger">缺ISO管理程序 {missingMainCount}</Badge>
              )}
            </div>
            <div className="flex flex-wrap items-center justify-end gap-2">
              <div
                className="flex flex-wrap gap-1"
                role="group"
                aria-label="檢視與資料工具"
              >
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={files.length === 0 || isBusy}
                  onClick={() =>
                    setOpenGroupKeys(new Set(groups.map((group) => group.key)))
                  }
                >
                  全部展開
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={files.length === 0 || isBusy}
                  onClick={() => setOpenGroupKeys(new Set())}
                >
                  全部收合
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={files.length === 0 || isBusy}
                  onClick={exportJson}
                >
                  匯出 JSON
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={files.length === 0 || isBusy}
                  onClick={() => {
                    resetProgress();
                    setFiles([]);
                    setMetadataByMainId({});
                    setOpenGroupKeys(new Set());
                    setMessage("資料已清除。");
                  }}
                >
                  清除
                </Button>
              </div>
              <span
                className="hidden h-8 w-px bg-line sm:block"
                aria-hidden="true"
              />
              <div
                className="flex flex-wrap gap-2"
                role="group"
                aria-label="分析與送出"
              >
                <Button
                  variant="secondary"
                  size="sm"
                  loading={isAnalyzing}
                  loadingText="AI 分析中"
                  disabled={files.length === 0 || isSubmitting}
                  onClick={() => void runAiAnalysis()}
                >
                  <Sparkles
                    className="size-4 text-primary"
                    aria-hidden="true"
                  />
                  AI 分析
                </Button>
                <Button
                  size="sm"
                  loading={isSubmitting}
                  loadingText={`處理中 ${importProgress?.processedCount ?? 0}/${groups.length}`}
                  disabled={!canSubmit || isAnalyzing}
                  onClick={() => void runCommit()}
                >
                  {importProgress?.status === IMPORT_STATUS.Completed
                    ? "重新儲存"
                    : "儲存"}
                </Button>
              </div>
            </div>
          </div>
        </div>

        {!canSubmit && files.length > 0 && !isBusy && (
          <p
            className="border-t border-line px-4 py-2 text-meta text-state-danger"
            role="status"
          >
            {missingMainCount > 0
              ? "尚有待確認或缺少ISO管理程序的資料，請完成修正後再儲存。"
              : "尚有待確認的資料，請完成修正後再儲存。"}
          </p>
        )}

        {importProgress !== undefined && (
          <section
            className="border-t border-line bg-surface px-4 py-3"
            aria-labelledby="import-progress-title"
          >
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2
                  id="import-progress-title"
                  className="text-section-label text-ink"
                >
                  儲存進度
                </h2>
                <p className="mt-1 text-meta text-ink-muted" aria-live="polite">
                  {importProgress.status === IMPORT_STATUS.Completed
                    ? `已處理 ${importProgress.totalCount} 個ISO管理程序群組。`
                    : `正在處理ISO管理程序群組 ${importProgress.processedCount + 1}/${importProgress.totalCount}：${importProgress.currentGroupLabel ?? "準備下一個群組"}`}
                </p>
              </div>
              <span className="text-meta text-ink-muted tabular">
                {importProgress.processedCount}/{importProgress.totalCount}（
                {progressPercentage}%）
              </span>
            </div>
            <div
              className="mt-3 h-2 overflow-hidden rounded-xs bg-line"
              role="progressbar"
              aria-label="儲存進度"
              aria-valuemin={0}
              aria-valuemax={importProgress.totalCount}
              aria-valuenow={importProgress.processedCount}
              aria-valuetext={`${importProgress.processedCount} / ${importProgress.totalCount}`}
            >
              <div
                className={`h-full transition-[width] duration-150 ease-out motion-reduce:transition-none ${importProgress.status === IMPORT_STATUS.Completed ? "bg-state-active" : "bg-primary"}`}
                style={{ width: `${progressPercentage}%` }}
              />
            </div>
          </section>
        )}

        {importProgress?.status === IMPORT_STATUS.Completed && summary && (
          <Alert
            className="mx-4 mt-4"
            variant={
              summary.documents.failed === 0 && summary.attachments.failed === 0
                ? "success"
                : "warning"
            }
            title="儲存完成"
          >
            ISO管理程序：共 {summary.documents.total} 筆，成功{" "}
            {summary.documents.success} 筆，失敗 {summary.documents.failed}{" "}
            筆。表單及附件：共 {summary.attachments.total} 筆，成功{" "}
            {summary.attachments.success} 筆，跳過 {summary.attachments.skipped}{" "}
            筆，失敗 {summary.attachments.failed} 筆。
          </Alert>
        )}

        {submitError !== undefined && (
          <Alert className="mx-4 mt-4" variant="error" title="儲存失敗">
            {submitError}
          </Alert>
        )}

        <div className="grid gap-3 border-t border-line bg-surface-header p-4 md:grid-cols-[minmax(16rem,1fr)_14rem]">
          <label className="relative block">
            <Search
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-ink-muted"
              aria-hidden="true"
            />
            <Input
              type="search"
              className="pl-9"
              value={keyword}
              placeholder="搜尋ISO管理程序編號、檔名、表單及附件名稱"
              aria-label="搜尋解析結果"
              onChange={(event) => setKeyword(event.target.value)}
            />
          </label>
          <Select
            value={statusFilter}
            aria-label="篩選群組狀態"
            onChange={(event) => {
              if (isGroupStatusFilter(event.target.value)) {
                setStatusFilter(event.target.value);
              }
            }}
          >
            <option value={GROUP_STATUS.All}>全部群組</option>
            <option value={GROUP_STATUS.Ok}>完整</option>
            {missingMainCount > 0 && (
              <option value={GROUP_STATUS.MissingMain}>缺ISO管理程序</option>
            )}
            <option value={GROUP_STATUS.Warning}>有待確認</option>
          </Select>
        </div>

        <div className="space-y-3 p-4">
          {visibleGroups.length === 0 ? (
            <div className="grid min-h-40 place-items-center text-center">
              <div>
                <Paperclip
                  className="mx-auto size-8 text-ink-muted"
                  aria-hidden="true"
                />
                <p className="mt-2 text-cell font-medium text-ink">
                  {files.length === 0
                    ? "尚未加入檔案"
                    : "沒有符合目前條件的資料"}
                </p>
                <p className="mt-1 text-meta text-ink-muted">
                  {files.length === 0
                    ? "可拖曳多個檔案，或選擇整個資料夾。"
                    : "請調整搜尋文字或群組狀態。"}
                </p>
              </div>
            </div>
          ) : (
            visibleGroups.map((group) => {
              const open = openGroupKeys.has(group.key);
              const mainFile = group.files.find(
                (file) => file.role === FILE_ROLE.Main,
              );
              const metadata = mainFile
                ? metadataByMainId[mainFile.id]
                : undefined;
              const groupMainCount = group.files.filter(
                (file) => file.role === FILE_ROLE.Main,
              ).length;
              const groupAttachmentCount = group.files.filter(
                (file) => file.role === FILE_ROLE.Attachment,
              ).length;
              const groupWarningCount = group.files.filter(
                (file) => file.parseStatus !== PARSE_STATUS.Ok,
              ).length;

              return (
                <section
                  key={group.key}
                  className="overflow-hidden rounded-sm border border-line-strong bg-surface"
                >
                  <button
                    type="button"
                    className="flex w-full items-start justify-between gap-4 bg-surface-header px-4 py-3 text-left hover:bg-surface-hover"
                    aria-expanded={open}
                    onClick={() => toggleGroup(group.key)}
                  >
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <ChevronRight
                          className={`size-4 text-ink-muted transition-transform ${open ? "rotate-90" : ""}`}
                          aria-hidden="true"
                        />
                        <code className="rounded-xs bg-primary-subtle px-2 py-1 font-mono text-code text-primary">
                          {group.documentCode || "未辨識ISO管理程序"}
                        </code>
                        {statusBadge(group)}
                      </div>
                      <p className="mt-1 text-meta text-ink-muted">
                        {group.sourceFolder
                          ? `來源資料夾：${group.sourceFolder}`
                          : "來源：散落檔案"}
                      </p>
                    </div>
                    <div className="flex flex-wrap justify-end gap-2">
                      <Badge variant="success">
                        ISO管理程序 {groupMainCount}
                      </Badge>
                      <Badge variant="neutral">
                        表單及附件 {groupAttachmentCount}
                      </Badge>
                      {groupWarningCount > 0 && (
                        <Badge variant="warning">
                          待確認 {groupWarningCount}
                        </Badge>
                      )}
                    </div>
                  </button>

                  {open && (
                    <div className="border-t border-line">
                      <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
                        <label
                          className="text-label font-medium text-ink"
                          htmlFor={`group-code-${group.key}`}
                        >
                          ISO管理程序編號
                        </label>
                        <Input
                          id={`group-code-${group.key}`}
                          className="w-48 font-mono"
                          value={group.documentCode}
                          placeholder="GA-P-01"
                          disabled={isBusy}
                          onChange={(event) =>
                            updateGroupDocumentCode(group, event.target.value)
                          }
                        />
                        <Badge variant="neutral">
                          共 {group.files.length} 個檔案
                        </Badge>
                        {mainFile && (
                          <>
                            <label
                              className="text-label font-medium text-ink"
                              htmlFor={`group-category-${mainFile.id}`}
                            >
                              品質系統
                            </label>
                            <Select
                              id={`group-category-${mainFile.id}`}
                              className="w-52"
                              value={metadata?.isoCategoryId ?? ""}
                              disabled={isBusy || isoCategories.isPending || isoCategories.isError}
                              onChange={(event) =>
                                updateDocumentMetadata(
                                  mainFile.id,
                                  "isoCategoryId",
                                  event.target.value,
                                )
                              }
                            >
                              <option value="">不指定（既有文件沿用）</option>
                              {isoCategories.data?.map((category) => (
                                <option key={category.id} value={category.id}>
                                  {category.name}
                                </option>
                              ))}
                            </Select>
                            <label
                              className="text-label font-medium text-ink"
                              htmlFor={`group-dept-${mainFile.id}`}
                            >
                              發行部門
                            </label>
                            <Select
                              id={`group-dept-${mainFile.id}`}
                              className="w-52"
                              value={metadata?.deptId ?? ""}
                              disabled={isBusy || depts.isPending || depts.isError}
                              onChange={(event) =>
                                updateDocumentMetadata(
                                  mainFile.id,
                                  "deptId",
                                  event.target.value,
                                )
                              }
                            >
                              <option value="">不指定（既有文件沿用）</option>
                              {depts.data?.map((dept) => (
                                <option key={dept.id} value={dept.id}>
                                  {dept.name}
                                </option>
                              ))}
                            </Select>
                          </>
                        )}
                      </div>

                      <div className="overflow-x-auto">
                        <table className="w-full min-w-[66rem] border-collapse text-cell">
                          <thead>
                            <tr className="h-table-header border-b border-line bg-surface-header text-left text-table-header text-ink-muted">
                              <th className="w-12 px-3">#</th>
                              <th className="w-36 px-3">類型</th>
                              <th className="w-40 px-3">ISO管理程序編號</th>
                              <th className="w-44 px-3">表單及附件編號</th>
                              <th className="min-w-56 px-3">顯示名稱</th>
                              <th className="w-20 px-3">副檔名</th>
                              <th className="w-24 px-3">大小</th>
                              <th className="w-28 px-3">狀態</th>
                              <th className="w-32 px-3">送出進度</th>
                              <th className="w-24 px-3">操作</th>
                            </tr>
                          </thead>
                          <tbody>
                            {group.files.map((file, index) => {
                              const rowIsProcessing =
                                importProgress?.processingFileIds.has(
                                  file.id,
                                ) === true;

                              return (
                                <Fragment key={file.id}>
                                  <tr
                                    className={
                                      rowIsProcessing
                                        ? "bg-primary-subtle hover:bg-surface-hover"
                                        : "hover:bg-surface-hover"
                                    }
                                  >
                                    <td className="px-3 py-2 text-meta text-ink-muted tabular">
                                      {index + 1}
                                    </td>
                                    <td className="px-3 py-2">
                                      <Select
                                        value={file.role}
                                        disabled={isBusy}
                                        aria-label={`${file.file.name} 類型`}
                                        onChange={(event) => {
                                          if (isFileRole(event.target.value)) {
                                            updateFile(file.id, {
                                              role: event.target.value,
                                            });
                                          }
                                        }}
                                      >
                                        {Object.entries(ROLE_LABELS).map(
                                          ([value, label]) => (
                                            <option key={value} value={value}>
                                              {label}
                                            </option>
                                          ),
                                        )}
                                      </Select>
                                    </td>
                                    <td className="px-3 py-2">
                                      <Input
                                        className="font-mono"
                                        value={file.documentCode}
                                        disabled={isBusy}
                                        aria-label={`${file.file.name} ISO管理程序編號`}
                                        onChange={(event) =>
                                          updateFile(file.id, {
                                            documentCode:
                                              event.target.value.toUpperCase(),
                                          })
                                        }
                                      />
                                    </td>
                                    <td className="px-3 py-2">
                                      <Input
                                        className="font-mono"
                                        value={file.attachmentCode}
                                        disabled={
                                          isBusy || file.role === FILE_ROLE.Main
                                        }
                                        aria-label={`${file.file.name} 表單及附件編號`}
                                        onChange={(event) =>
                                          updateAttachmentCode(
                                            file,
                                            event.target.value,
                                          )
                                        }
                                      />
                                    </td>
                                    <td className="px-3 py-2">
                                      <Input
                                        value={file.displayName}
                                        disabled={isBusy}
                                        aria-label={`${file.file.name} 顯示名稱`}
                                        onChange={(event) =>
                                          updateFile(file.id, {
                                            displayName: event.target.value,
                                          })
                                        }
                                      />
                                    </td>
                                    <td className="px-3 py-2 font-mono text-code text-ink-muted">
                                      {file.extension || "－"}
                                    </td>
                                    <td className="px-3 py-2 text-meta text-ink-muted tabular">
                                      {formatFileSize(file.file.size)}
                                    </td>
                                    <td className="px-3 py-2">
                                      {duplicateMainIds.has(file.id) ? (
                                        <Badge variant="warning">
                                          ISO管理程序重複
                                        </Badge>
                                      ) : file.parseStatus ===
                                        PARSE_STATUS.Ok ? (
                                        <Badge variant="success">已辨識</Badge>
                                      ) : file.role ===
                                        FILE_ROLE.MainCandidate ? (
                                        <Badge variant="info">
                                          疑似ISO管理程序
                                        </Badge>
                                      ) : (
                                        <Badge variant="warning">待確認</Badge>
                                      )}
                                    </td>
                                    <td className="px-3 py-2">
                                      {importProgress?.failedFileIds.has(
                                        file.id,
                                      ) ? (
                                        <div className="space-y-0.5">
                                          <span className="inline-flex items-center gap-1.5 text-label text-state-danger">
                                            <XCircle
                                              className="size-4"
                                              aria-hidden="true"
                                            />
                                            失敗
                                          </span>
                                          {importProgress.fileMessages.get(
                                            file.id,
                                          ) && (
                                            <p className="text-fine text-state-danger">
                                              {importProgress.fileMessages.get(
                                                file.id,
                                              )}
                                            </p>
                                          )}
                                        </div>
                                      ) : importProgress?.skippedFileIds.has(
                                          file.id,
                                        ) ? (
                                        <div className="space-y-0.5">
                                          <span className="inline-flex items-center gap-1.5 text-label text-ink-muted">
                                            <MinusCircle
                                              className="size-4"
                                              aria-hidden="true"
                                            />
                                            已跳過
                                          </span>
                                          {importProgress.fileMessages.get(
                                            file.id,
                                          ) && (
                                            <p className="text-fine text-ink-muted">
                                              {importProgress.fileMessages.get(
                                                file.id,
                                              )}
                                            </p>
                                          )}
                                        </div>
                                      ) : importProgress?.completedFileIds.has(
                                          file.id,
                                        ) ? (
                                        <span className="inline-flex items-center gap-1.5 text-label text-state-active">
                                          <CheckCircle2
                                            className="size-4"
                                            aria-hidden="true"
                                          />
                                          已完成
                                        </span>
                                      ) : importProgress?.processingFileIds.has(
                                          file.id,
                                        ) ? (
                                        <span className="inline-flex items-center gap-1.5 text-label text-primary">
                                          <LoaderCircle
                                            className="size-4 animate-spin motion-reduce:animate-none"
                                            aria-hidden="true"
                                          />
                                          處理中
                                        </span>
                                      ) : importProgress !== undefined ? (
                                        <span className="inline-flex items-center gap-1.5 text-label text-ink-muted">
                                          <Circle
                                            className="size-4"
                                            aria-hidden="true"
                                          />
                                          等待處理
                                        </span>
                                      ) : (
                                        <span className="text-meta text-ink-muted">
                                          尚未送出
                                        </span>
                                      )}
                                    </td>
                                    <td className="px-3 py-2">
                                      <Button
                                        variant="ghost"
                                        size="sm"
                                        aria-label={`移除 ${file.file.name}`}
                                        disabled={isBusy}
                                        onClick={() => {
                                          resetProgress();
                                          setFiles((current) =>
                                            resolveMainCandidates(
                                              current.filter(
                                                (item) => item.id !== file.id,
                                              ),
                                            ),
                                          );
                                        }}
                                      >
                                        <Trash2
                                          className="size-4"
                                          aria-hidden="true"
                                        />
                                        移除
                                      </Button>
                                    </td>
                                  </tr>
                                  <tr
                                    className={`border-b border-line ${rowIsProcessing ? "bg-primary-subtle" : "bg-surface-zebra"}`}
                                  >
                                    <td colSpan={10} className="px-3 pt-0 pb-2">
                                      <div className="flex min-w-0 flex-wrap items-center gap-x-4 gap-y-1 border-l-2 border-line-strong pl-3 text-fine text-ink-muted">
                                        <span className="min-w-0">
                                          <span className="font-medium text-ink">
                                            原始檔名：
                                          </span>
                                          <span title={file.file.name}>
                                            {file.file.name}
                                          </span>
                                        </span>
                                        <span className="min-w-0 break-all">
                                          <span className="font-medium text-ink">
                                            路徑：
                                          </span>
                                          <code title={file.relativePath}>
                                            {file.relativePath}
                                          </code>
                                        </span>
                                      </div>
                                    </td>
                                  </tr>
                                </Fragment>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}
                </section>
              );
            })
          )}
        </div>
      </section>
    </section>
  );
}

export default AdminAttachmentImportPage;
