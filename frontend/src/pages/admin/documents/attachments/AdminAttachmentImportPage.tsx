import {
  ChevronRight,
  FileArchive,
  FileStack,
  FolderOpen,
  Paperclip,
  Search,
  Trash2,
  Upload,
} from "lucide-react";
import { useRef, useState, type ChangeEvent, type DragEvent } from "react";
import { useNavigate } from "react-router-dom";

import { Alert, Badge, Button, Input, Select } from "@/components/common";
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

const ROLE_LABELS: Record<FileRole, string> = {
  [FILE_ROLE.Main]: "主文",
  [FILE_ROLE.Attachment]: "附件",
  [FILE_ROLE.MainCandidate]: "疑似主文",
  [FILE_ROLE.Unresolved]: "未判斷",
};

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
    return <Badge variant="danger">缺主文</Badge>;
  return <Badge variant="warning">待確認</Badge>;
}

export function AdminAttachmentImportPage() {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement | null>(null);
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

  const groups = attachmentFileGroups(files);
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
  const warningCount = files.filter(
    (file) =>
      file.parseStatus !== PARSE_STATUS.Ok ||
      file.role === FILE_ROLE.MainCandidate ||
      file.role === FILE_ROLE.Unresolved,
  ).length;
  const missingMainCount = groups.filter(
    (group) => group.status === GROUP_STATUS.MissingMain,
  ).length;

  function addFiles(selectedFiles: File[]) {
    if (selectedFiles.length === 0) return;

    const existing = new Set(files.map(fileIdentity));
    const additions = selectedFiles
      .map(parseAttachmentFile)
      .filter((file) => !existing.has(fileIdentity(file)));
    const nextFiles = resolveMainCandidates([...files, ...additions]);
    const nextGroups = attachmentFileGroups(nextFiles);
    setFiles(nextFiles);
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
    addFiles(Array.from(event.dataTransfer.files));
  }

  function updateFile(id: string, patch: Partial<ParsedAttachmentFile>) {
    setFiles((current) =>
      resolveMainCandidates(
        current.map((file) =>
          file.id === id ? recalculateFile({ ...file, ...patch }) : file,
        ),
      ),
    );
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

  return (
    <section>
      <input
        ref={fileInputRef}
        type="file"
        multiple
        hidden
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
        onChange={handleFileChange}
      />

      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="mb-1 text-label font-medium text-primary">文件管理</p>
          <h1 className="text-page-title text-ink">批次新增附件</h1>
        </div>
        <Button
          variant="secondary"
          onClick={() => navigate("/admin/documents")}
        >
          返回 ISO 文件維護
        </Button>
      </div>

      <Alert
        className="mb-4"
        variant="info"
        title=" 選取檔案或資料夾，自動辨識主文歸屬與附件編號，再人工確認匯入資料。"
      ></Alert>

      <section className="border border-line-strong bg-surface">
        <div
          role="button"
          tabIndex={0}
          aria-label="拖曳檔案到這裡"
          className={`m-4 grid min-h-48 cursor-pointer place-items-center rounded-sm border-2 border-dashed px-6 py-8 text-center transition-colors ${
            isDragging
              ? "border-primary bg-primary-subtle"
              : "border-line-strong bg-canvas hover:border-primary hover:bg-primary-subtle"
          }`}
          onClick={() => fileInputRef.current?.click()}
          onKeyDown={(event) => {
            if (event.key === "Enter" || event.key === " ") {
              event.preventDefault();
              fileInputRef.current?.click();
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
            <span className="mx-auto mb-3 grid size-12 place-items-center rounded-md bg-primary-subtle text-primary">
              <Upload className="size-6" aria-hidden="true" />
            </span>
            <p className="text-section-label text-ink">拖曳多個檔案到這裡</p>
            <p className="mt-1 text-meta text-ink-muted">
              資料夾請使用下方「選擇資料夾」，以保留相對路徑及主文群組。
            </p>
            <div className="mt-4 flex flex-wrap justify-center gap-2">
              <Button
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
                onClick={(event) => {
                  event.stopPropagation();
                  folderInputRef.current?.click();
                }}
              >
                <FolderOpen className="size-4" aria-hidden="true" />
                選擇資料夾
              </Button>
            </div>
            <p className="mt-4 text-fine text-ink-faint">
              範例：GA-P-01文件與紀錄管制程序.pdf、GA-P-01-01B文件登記表.xls
            </p>
          </div>
        </div>

        {message && (
          <p className="px-4 pb-4 text-meta text-ink-muted">{message}</p>
        )}

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line px-4 py-3">
          <div className="flex flex-wrap gap-2">
            <Badge variant="neutral">主文群組 {groups.length}</Badge>
            <Badge variant="neutral">檔案 {files.length}</Badge>
            <Badge variant="success">主文 {mainCount}</Badge>
            <Badge variant="info">附件 {attachmentCount}</Badge>
            <Badge variant="warning">待確認 {warningCount}</Badge>
            <Badge variant="danger">缺主文 {missingMainCount}</Badge>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              variant="secondary"
              size="sm"
              disabled={files.length === 0}
              onClick={() =>
                setOpenGroupKeys(new Set(groups.map((group) => group.key)))
              }
            >
              全部展開
            </Button>
            <Button
              variant="secondary"
              size="sm"
              disabled={files.length === 0}
              onClick={() => setOpenGroupKeys(new Set())}
            >
              全部收合
            </Button>
            <Button
              size="sm"
              disabled={files.length === 0}
              onClick={exportJson}
            >
              匯出 JSON
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
              disabled={files.length === 0}
              onClick={() => {
                setFiles([]);
                setOpenGroupKeys(new Set());
                setMessage("資料已清除。");
              }}
            >
              清除
            </Button>
          </div>
        </div>

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
              placeholder="搜尋主文編號、檔名、附件名稱"
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
            <option value={GROUP_STATUS.MissingMain}>缺主文</option>
            <option value={GROUP_STATUS.Warning}>有待確認</option>
          </Select>
        </div>

        <div className="space-y-3 p-4">
          {visibleGroups.length === 0 ? (
            <div className="grid min-h-40 place-items-center text-center">
              <div>
                <Paperclip
                  className="mx-auto size-8 text-ink-faint"
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
                          {group.documentCode || "未辨識主文"}
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
                      <Badge variant="success">主文 {groupMainCount}</Badge>
                      <Badge variant="neutral">
                        附件 {groupAttachmentCount}
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
                          主文編號
                        </label>
                        <Input
                          id={`group-code-${group.key}`}
                          className="w-48 font-mono"
                          value={group.documentCode}
                          placeholder="GA-P-01"
                          onChange={(event) =>
                            updateGroupDocumentCode(group, event.target.value)
                          }
                        />
                        <Badge variant="neutral">
                          共 {group.files.length} 個檔案
                        </Badge>
                      </div>

                      <div className="overflow-x-auto">
                        <table className="w-full min-w-[76rem] border-collapse text-cell">
                          <thead>
                            <tr className="h-table-header border-b border-line bg-surface-header text-left text-table-header text-ink-muted">
                              <th className="w-12 px-3">#</th>
                              <th className="min-w-72 px-3">原始檔名／路徑</th>
                              <th className="w-36 px-3">類型</th>
                              <th className="w-40 px-3">主文編號</th>
                              <th className="w-44 px-3">附件編號</th>
                              <th className="min-w-56 px-3">顯示名稱</th>
                              <th className="w-20 px-3">副檔名</th>
                              <th className="w-24 px-3">大小</th>
                              <th className="w-28 px-3">狀態</th>
                              <th className="w-24 px-3">操作</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-line">
                            {group.files.map((file, index) => (
                              <tr
                                key={file.id}
                                className="hover:bg-surface-hover"
                              >
                                <td className="px-3 py-2 text-meta text-ink-muted tabular">
                                  {index + 1}
                                </td>
                                <td className="max-w-80 px-3 py-2">
                                  <p
                                    className="truncate font-medium text-ink"
                                    title={file.file.name}
                                  >
                                    {file.file.name}
                                  </p>
                                  <p
                                    className="mt-0.5 truncate text-fine text-ink-faint"
                                    title={file.relativePath}
                                  >
                                    {file.relativePath}
                                  </p>
                                </td>
                                <td className="px-3 py-2">
                                  <Select
                                    value={file.role}
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
                                    aria-label={`${file.file.name} 主文編號`}
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
                                    disabled={file.role === FILE_ROLE.Main}
                                    aria-label={`${file.file.name} 附件編號`}
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
                                  {file.parseStatus === PARSE_STATUS.Ok ? (
                                    <Badge variant="success">已辨識</Badge>
                                  ) : file.role === FILE_ROLE.MainCandidate ? (
                                    <Badge variant="info">疑似主文</Badge>
                                  ) : (
                                    <Badge variant="warning">待確認</Badge>
                                  )}
                                </td>
                                <td className="px-3 py-2">
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    className="text-state-danger hover:bg-state-danger-subtle hover:text-state-danger"
                                    aria-label={`移除 ${file.file.name}`}
                                    onClick={() =>
                                      setFiles((current) =>
                                        resolveMainCandidates(
                                          current.filter(
                                            (item) => item.id !== file.id,
                                          ),
                                        ),
                                      )
                                    }
                                  >
                                    <Trash2
                                      className="size-4"
                                      aria-hidden="true"
                                    />
                                    移除
                                  </Button>
                                </td>
                              </tr>
                            ))}
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

        <div className="flex items-start gap-2 border-t border-line px-4 py-3 text-meta text-ink-muted">
          <FileArchive className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>
            判斷規則依 IFOLDER
            範例：含附件尾碼者辨識為附件；主文編號檔名辨識為疑似主文；資料夾內無法辨識編號的檔案歸入該主文並標記待確認。ZIP
            不會自動解壓。
          </p>
        </div>
      </section>
    </section>
  );
}

export default AdminAttachmentImportPage;
