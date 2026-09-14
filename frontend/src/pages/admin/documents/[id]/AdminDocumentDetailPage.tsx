import { useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import {
  Alert,
  Button,
  FormField,
  Input,
  Modal,
  Select,
  Spinner,
  Textarea,
} from "@/components/common";
import { AttachmentBatchImportPanel } from "@/features/admin-documents/components/AttachmentBatchImportPanel";
import {
  createAttachmentVersionFormSchema,
  type AttachmentVersionFormValues,
} from "@/features/admin-documents/attachmentSchemas";
import {
  useAdminDocument,
  useAttachmentDetail,
  useCreateAttachmentVersion,
  useCreateVersion,
  useDeleteAttachment,
} from "@/features/admin-documents/queries";
import {
  createVersionFormSchema,
  todayUtc,
  type VersionFormValues,
} from "@/features/admin-documents/versionSchemas";
import {
  DOCUMENT_VERSION_STATUS,
  type Attachment,
  type DocumentVersionStatus,
} from "@/features/admin-documents/types";
import { useCurrentUser } from "@/features/auth/queries";
import { USER_ROLE } from "@/features/auth/types";
import { useCompanies } from "@/features/companies/queries";
import { useDownloadAttachment } from "@/features/documents/queries";

type VersionStatusPresentation = {
  label: string;
  dotClassName: string;
  textClassName: string;
};

const VERSION_STATUS: Record<DocumentVersionStatus, VersionStatusPresentation> = {
  [DOCUMENT_VERSION_STATUS.Draft]: {
    label: "草稿",
    dotClassName: "bg-ink-muted",
    textClassName: "text-ink-muted",
  },
  [DOCUMENT_VERSION_STATUS.Published]: {
    label: "已發布",
    dotClassName: "bg-state-active",
    textClassName: "text-state-active",
  },
  [DOCUMENT_VERSION_STATUS.Obsolete]: {
    label: "已作廢",
    dotClassName: "bg-state-obsolete",
    textClassName: "text-state-obsolete",
  },
};

const UNKNOWN_VERSION_STATUS: VersionStatusPresentation = {
  label: "未知狀態",
  dotClassName: "bg-ink-muted",
  textClassName: "text-ink-muted",
};

function getVersionStatusPresentation(status: unknown): VersionStatusPresentation {
  if (typeof status !== "string") return UNKNOWN_VERSION_STATUS;

  const normalizedStatus = status.toUpperCase();
  if (normalizedStatus in VERSION_STATUS) {
    return VERSION_STATUS[normalizedStatus as DocumentVersionStatus];
  }

  return UNKNOWN_VERSION_STATUS;
}

type VersionFieldErrors = Partial<Record<keyof VersionFormValues, string>>;

function nextMinorVersion(currentVersion: string | null | undefined): string {
  const normalized = currentVersion?.trim();
  if (!normalized) return "1.0";

  const match = /^([1-9]\d*)(?:\.(\d+))?$/.exec(normalized);
  if (match === null) return "1.0";

  const major = Number(match[1]);
  const minor = Number(match[2] ?? "0");
  if (!Number.isSafeInteger(major) || !Number.isSafeInteger(minor))
    return "1.0";

  return `${major}.${minor + 1}`;
}

function emptyVersionForm(version = "1.0"): VersionFormValues {
  return {
    version,
    effectiveDate: todayUtc(),
    pageCount: "",
    memo: "",
    file: null,
  };
}

function emptyAttachmentVersionForm(): AttachmentVersionFormValues {
  return { changeType: "MINOR", effectiveDate: "", file: null };
}

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0];
}

function apiFieldMessage<TField extends string>(
  errors: Record<string, string[]>,
  field: TField,
): string | undefined {
  const entry = Object.entries(errors).find(
    ([key]) => key.toLowerCase() === field.toLowerCase(),
  );
  return firstMessage(entry?.[1]);
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("zh-TW", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Asia/Taipei",
  }).format(new Date(value));
}

function versionErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return "目前無法連線到系統，請稍後再試。";
  }

  if (error.status === 404) {
    return "找不到這份文件，請返回清單確認文件是否仍存在。";
  }

  if (error.status === 409) {
    const message = `${error.title} ${error.detail ?? ""}`.toLowerCase();
    if (message.includes("停用") || message.includes("inactive")) {
      return "文件已停用，無法新增版本。";
    }
    if (message.includes("版本號") || message.includes("version")) {
      return error.detail ?? "此文件已存在相同的版本號。";
    }
    return "其他管理員可能同時發布版本，請重新整理文件後再試。";
  }

  return error.detail ?? "無法更新文件版本，請稍後再試。";
}

function attachmentVersionErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return "目前無法連線到系統，請稍後再試。";
  }

  if (error.status === 404) {
    return "找不到這個附件，請重新整理頁面後再試。";
  }

  if (error.status === 409) {
    const message = `${error.title} ${error.detail ?? ""}`.toLowerCase();
    if (message.includes("停用") || message.includes("inactive")) {
      return "附件已停用，無法新增版本。";
    }
    if (message.includes("版本號") || message.includes("version")) {
      return error.detail ?? "此附件已存在相同的版本號。";
    }
    return "其他管理員可能同時發布版本，請重新整理後再試。";
  }

  return error.detail ?? "無法更新附件版本，請稍後再試。";
}

function attachmentErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof ApiError)) {
    return "目前無法連線到系統，請稍後再試。";
  }

  return error.detail ?? fallback;
}

export function AdminDocumentDetailPage() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const currentUser = useCurrentUser();
  const isSystemAdmin = currentUser.data?.role === USER_ROLE.SystemAdmin;
  const document = useAdminDocument(id);
  const companies = useCompanies({ page: 1, pageSize: 100 }, isSystemAdmin);
  const createVersion = useCreateVersion();
  const [versionModalOpen, setVersionModalOpen] = useState(false);
  const [versionFormKey, setVersionFormKey] = useState(0);
  const [versionValues, setVersionValues] =
    useState<VersionFormValues>(emptyVersionForm);
  const [versionFieldErrors, setVersionFieldErrors] =
    useState<VersionFieldErrors>({});
  const [versionFormError, setVersionFormError] = useState<string>();

  const deleteAttachment = useDeleteAttachment();
  const createAttachmentVersion = useCreateAttachmentVersion();
  const downloadAttachment = useDownloadAttachment();

  const [addAttachmentOpen, setAddAttachmentOpen] = useState(false);
  const [attachmentDownloadError, setAttachmentDownloadError] =
    useState<string>();

  const [deleteAttachmentTarget, setDeleteAttachmentTarget] =
    useState<Attachment>();
  const [deleteAttachmentError, setDeleteAttachmentError] = useState<string>();

  const [versionAttachment, setVersionAttachment] = useState<Attachment>();
  const attachmentDetail = useAttachmentDetail(
    id,
    versionAttachment?.attachmentId,
  );
  const [attachmentVersionFormKey, setAttachmentVersionFormKey] = useState(0);
  const [attachmentVersionValues, setAttachmentVersionValues] =
    useState<AttachmentVersionFormValues>(emptyAttachmentVersionForm);
  const [attachmentVersionFieldErrors, setAttachmentVersionFieldErrors] =
    useState<Partial<Record<keyof AttachmentVersionFormValues, string>>>({});
  const [attachmentVersionFormError, setAttachmentVersionFormError] =
    useState<string>();

  function confirmDeleteAttachment() {
    if (id === undefined || deleteAttachmentTarget === undefined) return;

    setDeleteAttachmentError(undefined);
    deleteAttachment.mutate(
      { documentId: id, attachmentId: deleteAttachmentTarget.attachmentId },
      {
        onSuccess: () => {
          setDeleteAttachmentTarget(undefined);
        },
        onError: (error) => {
          setDeleteAttachmentError(
            attachmentErrorMessage(error, "無法刪除附件，請稍後再試。"),
          );
        },
      },
    );
  }

  function openAttachmentVersionModal(attachment: Attachment) {
    setVersionAttachment(attachment);
    setAttachmentVersionValues(emptyAttachmentVersionForm());
    setAttachmentVersionFieldErrors({});
    setAttachmentVersionFormError(undefined);
    setAttachmentVersionFormKey((current) => current + 1);
  }

  function handleAttachmentDownload(attachment: Attachment) {
    const currentVersion = detail.attachments.find(
      (item) => item.attachmentId === attachment.attachmentId,
    )?.currentVersion;
    if (currentVersion?.hasFile !== true) return;

    setAttachmentDownloadError(undefined);
    downloadAttachment.mutate(
      {
        attachmentId: attachment.attachmentId,
        versionId: currentVersion.versionId,
        fallbackFileName: attachment.name,
      },
      {
        onError: (error) => {
          setAttachmentDownloadError(
            attachmentErrorMessage(error, "附件下載失敗，請稍後再試。"),
          );
        },
      },
    );
  }

  function closeAttachmentVersionModal() {
    if (!createAttachmentVersion.isPending) setVersionAttachment(undefined);
  }

  function updateAttachmentVersionField<
    TField extends Exclude<keyof AttachmentVersionFormValues, "file">,
  >(
    field: TField,
    value: AttachmentVersionFormValues[TField],
  ) {
    setAttachmentVersionValues((current) => ({ ...current, [field]: value }));
    setAttachmentVersionFieldErrors((current) => ({
      ...current,
      [field]: undefined,
    }));
    setAttachmentVersionFormError(undefined);
  }

  function handleAttachmentVersionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAttachmentVersionFormError(undefined);

    const parsed = createAttachmentVersionFormSchema.safeParse(
      attachmentVersionValues,
    );
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors;
      setAttachmentVersionFieldErrors({
        changeType: firstMessage(errors.changeType),
        effectiveDate: firstMessage(errors.effectiveDate),
        file: firstMessage(errors.file),
      });
      return;
    }

    if (id === undefined || versionAttachment === undefined) {
      setAttachmentVersionFormError("找不到這個附件，請重新整理頁面後再試。");
      return;
    }

    createAttachmentVersion.mutate(
      {
        documentId: id,
        attachmentId: versionAttachment.attachmentId,
        input: parsed.data,
      },
      {
        onSuccess: () => {
          setVersionAttachment(undefined);
        },
        onError: (error) => {
          if (error instanceof ApiError && error.status === 400) {
            const errors = error.fieldErrors();
            setAttachmentVersionFieldErrors({
              changeType: apiFieldMessage(errors, "changeType"),
              effectiveDate: apiFieldMessage(errors, "effectiveDate"),
              file: apiFieldMessage(errors, "file"),
            });
            setAttachmentVersionFormError(
              Object.keys(errors).length === 0
                ? (error.detail ?? "欄位或檔案內容不正確，請檢查後重試。")
                : undefined,
            );
            return;
          }

          setAttachmentVersionFormError(attachmentVersionErrorMessage(error));
        },
      },
    );
  }

  function openVersionModal() {
    setVersionValues(
      emptyVersionForm(nextMinorVersion(document.data?.versions[0]?.version)),
    );
    setVersionFieldErrors({});
    setVersionFormError(undefined);
    setVersionFormKey((current) => current + 1);
    setVersionModalOpen(true);
  }

  function closeVersionModal() {
    if (!createVersion.isPending) setVersionModalOpen(false);
  }

  function updateVersionField(
    field: Exclude<keyof VersionFormValues, "file">,
    value: string,
  ) {
    setVersionValues((current) => ({ ...current, [field]: value }));
    setVersionFieldErrors((current) => ({ ...current, [field]: undefined }));
    setVersionFormError(undefined);
  }

  function handleVersionSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setVersionFormError(undefined);

    const parsed = createVersionFormSchema.safeParse(versionValues);
    if (!parsed.success) {
      const errors = parsed.error.flatten().fieldErrors;
      setVersionFieldErrors({
        version: firstMessage(errors.version),
        effectiveDate: firstMessage(errors.effectiveDate),
        pageCount: firstMessage(errors.pageCount),
        memo: firstMessage(errors.memo),
        file: firstMessage(errors.file),
      });
      return;
    }

    if (id === undefined) {
      setVersionFormError("找不到這份文件，請返回清單後重試。");
      return;
    }

    createVersion.mutate(
      { documentId: id, input: parsed.data },
      {
        onSuccess: () => {
          setVersionModalOpen(false);
        },
        onError: (error) => {
          if (error instanceof ApiError && error.status === 400) {
            const errors = error.fieldErrors();
            setVersionFieldErrors({
              version: apiFieldMessage(errors, "version"),
              effectiveDate: apiFieldMessage(errors, "effectiveDate"),
              pageCount: apiFieldMessage(errors, "pageCount"),
              memo: apiFieldMessage(errors, "memo"),
              file: apiFieldMessage(errors, "file"),
            });
            setVersionFormError(
              Object.keys(errors).length === 0
                ? (error.detail ?? "欄位或 PDF 檔案內容不正確，請檢查後重試。")
                : undefined,
            );
            return;
          }

          setVersionFormError(versionErrorMessage(error));
        },
      },
    );
  }

  if (document.isPending) {
    return (
      <section className="flex min-h-64 items-center justify-center border border-line-strong bg-surface">
        <div
          className="flex items-center gap-2 text-meta text-ink-muted"
          role="status"
        >
          <Spinner decorative />
          文件載入中
        </div>
      </section>
    );
  }

  if (document.isError || document.data === undefined) {
    const message =
      document.error instanceof ApiError
        ? (document.error.detail ?? "找不到指定的文件。")
        : "目前無法載入文件詳情，請稍後再試。";

    return (
      <section>
        <Alert variant="error" title="無法載入文件詳情">
          {message}
        </Alert>
        <Button
          className="mt-4"
          variant="secondary"
          onClick={() => navigate("/admin/documents")}
        >
          返回文件清單
        </Button>
      </section>
    );
  }

  const detail = document.data;
  const companyName = isSystemAdmin
    ? companies.data?.items.find((company) => company.id === detail.companyId)
        ?.name
    : currentUser.data?.companyName;
  const downloadableVersion =
    detail.currentVersion?.status === DOCUMENT_VERSION_STATUS.Published &&
    detail.currentVersion.hasFile
      ? detail.currentVersion
      : undefined;

  return (
    <section>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-page-title text-ink">{detail.name}</h1>
        </div>
        <div className="flex items-center gap-2">
          {detail.isActive && (
            <Button onClick={openVersionModal}>更新版本</Button>
          )}
        </div>
      </div>

      <div className="grid border border-line-strong bg-surface md:grid-cols-2 xl:grid-cols-4">
        <div className="border-b border-line p-4 md:border-r xl:border-b-0">
          <p className="text-label text-ink-muted">公司</p>
          <p className="mt-1 text-cell text-ink">
            {companyName ?? detail.companyId}
          </p>
        </div>
        <div className="border-b border-line p-4 xl:border-r xl:border-b-0">
          <p className="text-label text-ink-muted">文件編號</p>
          {downloadableVersion ? (
            <a
              className="mt-1 inline-block rounded-xs font-mono text-code font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
              href={`/api/documents/${detail.id}/versions/${downloadableVersion.versionId}/download`}
              title={`下載 ${detail.documentNo} 主文版本 ${downloadableVersion.version}`}
            >
              {detail.documentNo}
            </a>
          ) : (
            <p className="mt-1 font-mono text-code text-ink">
              {detail.documentNo}
            </p>
          )}
        </div>
        <div className="border-b border-line p-4 md:border-r md:border-b-0">
          <p className="text-label text-ink-muted">建立時間</p>
          <p className="mt-1 text-meta text-ink tabular">
            {formatDateTime(detail.createdAt)}
          </p>
        </div>
        <div className="p-4">
          <p className="text-label text-ink-muted">最後更新</p>
          <p className="mt-1 text-meta text-ink tabular">
            {formatDateTime(detail.updatedAt)}
          </p>
        </div>
      </div>

      <section
        className="mt-4 border border-line-strong bg-surface"
        aria-labelledby="attachments-title"
      >
        {addAttachmentOpen && id !== undefined ? (
          <AttachmentBatchImportPanel
            documentId={id}
            documentNo={detail.documentNo}
            documentName={detail.name}
            onCancel={() => setAddAttachmentOpen(false)}
            onImported={() => {}}
          />
        ) : (
          <>
            <div className="flex items-center justify-between gap-4 border-b border-line px-4 py-3">
              <div>
                <h2
                  id="attachments-title"
                  className="text-section-label text-ink"
                >
                  附件
                </h2>
                <p className="mt-1 text-meta text-ink-muted">
                  管理此文件的附件身份與版本檔案。
                </p>
              </div>
              {detail.isActive && (
                <Button
                  variant="secondary"
                  onClick={() => setAddAttachmentOpen(true)}
                >
                  新增附件
                </Button>
              )}
            </div>

            {attachmentDownloadError && (
              <div className="p-4">
                <Alert variant="error" title="無法下載附件">
                  {attachmentDownloadError}
                </Alert>
              </div>
            )}

            {detail.attachments.length === 0 ? (
              <div className="p-12 text-center">
                <p className="text-cell font-medium text-ink">
                  尚未建立任何附件
                </p>
                <p className="mt-1 text-meta text-ink-muted">
                  {detail.isActive
                    ? "點選「新增附件」建立第一筆附件身份。"
                    : "文件已停用，無法新增附件。"}
                </p>
              </div>
            ) : (
              <ol className="divide-y divide-line">
                {detail.attachments.map((attachment) => {
                  const canDownload = attachment.currentVersion?.hasFile === true;
                  const isDownloading =
                    downloadAttachment.isPending &&
                    downloadAttachment.variables?.attachmentId ===
                      attachment.attachmentId;

                  return (
                    <li
                      key={attachment.attachmentId}
                      className="grid gap-3 px-4 py-3 md:grid-cols-[1fr_1fr_auto] md:items-center"
                    >
                      <div>
                        <p className="text-label text-ink-muted">編號</p>
                        <p className="font-mono text-code text-ink">
                          {attachment.attachmentNo}
                        </p>
                      </div>
                      <div>
                        <p className="text-label text-ink-muted">名稱</p>
                        {canDownload ? (
                          <button
                            type="button"
                            className="rounded-xs text-left text-cell font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:cursor-wait disabled:opacity-60"
                            disabled={isDownloading}
                            title={`下載 ${attachment.name}`}
                            onClick={() =>
                              handleAttachmentDownload(attachment)
                            }
                          >
                            {isDownloading ? "下載中…" : attachment.name}
                          </button>
                        ) : (
                          <p className="text-cell text-ink">
                            {attachment.name}
                          </p>
                        )}
                      </div>
                      <div className="flex items-center gap-2 justify-self-start md:justify-self-end">
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => openAttachmentVersionModal(attachment)}
                        >
                          版本管理
                        </Button>
                        {detail.isActive && (
                          <Button
                            size="sm"
                            variant="danger"
                            onClick={() => {
                              setDeleteAttachmentError(undefined);
                              setDeleteAttachmentTarget(attachment);
                            }}
                          >
                            刪除
                          </Button>
                        )}
                      </div>
                    </li>
                  );
                })}
              </ol>
            )}
          </>
        )}
      </section>

      <section
        className="mt-4 border border-line-strong bg-surface"
        aria-labelledby="version-history-title"
      >
        <div className="border-b border-line px-4 py-3">
          <h2
            id="version-history-title"
            className="text-section-label text-ink"
          >
            版本歷程
          </h2>
          <p className="mt-1 text-meta text-ink-muted">
            依版本由新至舊顯示發布與作廢紀錄。
          </p>
        </div>

        {detail.versions.length === 0 ? (
          <div className="p-12 text-center">
            <p className="text-cell font-medium text-ink">尚未建立任何版本</p>
            <p className="mt-1 text-meta text-ink-muted">
              {detail.isActive
                ? "點選「新增版本」上傳第一份 PDF。"
                : "文件已停用，無法新增版本。"}
            </p>
          </div>
        ) : (
          <ol className="divide-y divide-line">
            {detail.versions.map((version) => {
              const status = getVersionStatusPresentation(version.status);
              return (
                <li
                  key={version.version}
                  className="grid gap-3 px-4 py-3 md:grid-cols-[8rem_1fr_1fr] md:items-center"
                >
                  <div className="flex items-center gap-2">
                    <span
                      className={`size-1.5 shrink-0 rounded-full ${status.dotClassName}`}
                      aria-hidden="true"
                    />
                    <span
                      className={`text-label font-medium ${status.textClassName}`}
                    >
                      {status.label}
                    </span>
                  </div>
                  <div>
                    <p className="text-label text-ink-muted">版本</p>
                    <p className="font-mono text-revision text-ink">
                      {version.version}
                    </p>
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <p className="text-label text-ink-muted">生效日期</p>
                      <p className="text-meta text-ink tabular">
                        {version.effectiveDate ?? "－"}
                      </p>
                    </div>
                    <div>
                      <p className="text-label text-ink-muted">失效日期</p>
                      <p className="text-meta text-ink tabular">
                        {version.expiredDate ?? "－"}
                      </p>
                    </div>
                  </div>
                </li>
              );
            })}
          </ol>
        )}
      </section>

      <Modal
        open={deleteAttachmentTarget !== undefined}
        onClose={() => {
          if (!deleteAttachment.isPending) setDeleteAttachmentTarget(undefined);
        }}
        title="刪除附件"
        size="sm"
        closeOnBackdrop={!deleteAttachment.isPending}
        closeOnEscape={!deleteAttachment.isPending}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={deleteAttachment.isPending}
              onClick={() => setDeleteAttachmentTarget(undefined)}
            >
              取消
            </Button>
            <Button
              variant="danger"
              loading={deleteAttachment.isPending}
              loadingText="刪除中"
              onClick={confirmDeleteAttachment}
            >
              確認刪除
            </Button>
          </>
        }
      >
        {deleteAttachmentError && (
          <Alert className="mb-4" variant="error" title="無法刪除">
            {deleteAttachmentError}
          </Alert>
        )}
        確定要刪除附件「
        <strong className="font-semibold">
          {deleteAttachmentTarget?.attachmentNo} {deleteAttachmentTarget?.name}
        </strong>
        」嗎？
      </Modal>

      <Modal
        open={versionAttachment !== undefined}
        onClose={closeAttachmentVersionModal}
        title="附件版本管理"
        description={
          versionAttachment
            ? `${versionAttachment.attachmentNo}｜${versionAttachment.name}`
            : undefined
        }
        size="md"
        closeOnBackdrop={!createAttachmentVersion.isPending}
        closeOnEscape={!createAttachmentVersion.isPending}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={createAttachmentVersion.isPending}
              onClick={closeAttachmentVersionModal}
            >
              關閉
            </Button>
            <Button
              type="submit"
              form="create-attachment-version-form"
              loading={createAttachmentVersion.isPending}
              loadingText="上傳中"
            >
              上傳並發布
            </Button>
          </>
        }
      >
        <div className="mb-4">
          <h3 className="mb-2 text-label font-medium text-ink-muted">
            版本歷程
          </h3>
          {attachmentDetail.isPending ? (
            <div className="flex items-center gap-2 text-meta text-ink-muted">
              <Spinner decorative />
              版本載入中
            </div>
          ) : attachmentDetail.data === undefined ||
            attachmentDetail.data.versions.length === 0 ? (
            <p className="text-meta text-ink-muted">尚未上傳任何版本。</p>
          ) : (
            <ol className="divide-y divide-line border border-line">
              {attachmentDetail.data.versions.map((version) => {
                const status = getVersionStatusPresentation(version.status);
                return (
                  <li
                    key={version.version}
                    className="flex items-center justify-between gap-3 px-3 py-2"
                  >
                    <div className="flex items-center gap-2">
                      <span
                        className={`size-1.5 shrink-0 rounded-full ${status.dotClassName}`}
                        aria-hidden="true"
                      />
                      <span className="font-mono text-revision text-ink">
                        {version.version}
                      </span>
                      <span
                        className={`text-label font-medium ${status.textClassName}`}
                      >
                        {status.label}
                      </span>
                    </div>
                    <span className="text-meta text-ink-muted tabular">
                      {version.effectiveDate ?? "－"}
                      {version.expiredDate ? ` ～ ${version.expiredDate}` : ""}
                    </span>
                  </li>
                );
              })}
            </ol>
          )}
        </div>

        <form
          key={attachmentVersionFormKey}
          id="create-attachment-version-form"
          className="space-y-4"
          onSubmit={handleAttachmentVersionSubmit}
        >
          {attachmentVersionFormError && (
            <Alert variant="error" title="無法新增版本">
              {attachmentVersionFormError}
            </Alert>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              label="改版類型"
              htmlFor="attachment-version-change-type"
              error={attachmentVersionFieldErrors.changeType}
              hint="首版固定為 1.0；之後依大改或小改自動編版。"
              required
            >
              <Select
                id="attachment-version-change-type"
                value={attachmentVersionValues.changeType}
                error={attachmentVersionFieldErrors.changeType !== undefined}
                onChange={(event) =>
                  updateAttachmentVersionField(
                    "changeType",
                    event.target.value as "MAJOR" | "MINOR",
                  )
                }
              >
                <option value="MINOR">小改（次版號 +1）</option>
                <option value="MAJOR">大改（主版號 +1）</option>
              </Select>
            </FormField>

            <FormField
              label="生效日期"
              htmlFor="attachment-version-effective-date"
              error={attachmentVersionFieldErrors.effectiveDate}
              hint="選填；空白時以 UTC 今日立即生效。"
            >
              <Input
                id="attachment-version-effective-date"
                type="date"
                min={todayUtc()}
                value={attachmentVersionValues.effectiveDate}
                error={attachmentVersionFieldErrors.effectiveDate !== undefined}
                onChange={(event) =>
                  updateAttachmentVersionField(
                    "effectiveDate",
                    event.target.value,
                  )
                }
              />
            </FormField>
          </div>

          <FormField
            label="附件檔案"
            htmlFor="attachment-version-file"
            error={attachmentVersionFieldErrors.file}
            hint="支援 jpg、png、pdf、doc(x)、xls(x)、odt、ods。"
            required
          >
            <Input
              id="attachment-version-file"
              type="file"
              accept=".jpg,.jpeg,.png,.pdf,.doc,.docx,.xls,.xlsx,.odt,.ods"
              error={attachmentVersionFieldErrors.file !== undefined}
              onChange={(event) => {
                setAttachmentVersionValues((current) => ({
                  ...current,
                  file: event.target.files?.[0] ?? null,
                }));
                setAttachmentVersionFieldErrors((current) => ({
                  ...current,
                  file: undefined,
                }));
                setAttachmentVersionFormError(undefined);
              }}
            />
          </FormField>
        </form>
      </Modal>

      <Modal
        open={versionModalOpen}
        onClose={closeVersionModal}
        title="更新文件版本"
        description={`${detail.documentNo}｜${detail.name}`}
        size="md"
        closeOnBackdrop={!createVersion.isPending}
        closeOnEscape={!createVersion.isPending}
        footer={
          <>
            <Button
              variant="secondary"
              disabled={createVersion.isPending}
              onClick={closeVersionModal}
            >
              取消
            </Button>
            <Button
              type="submit"
              form="create-version-form"
              loading={createVersion.isPending}
              loadingText="上傳中"
            >
              上傳並發布
            </Button>
          </>
        }
      >
        <form
          key={versionFormKey}
          id="create-version-form"
          className="space-y-4"
          onSubmit={handleVersionSubmit}
        >
          {versionFormError && (
            <Alert variant="error" title="無法新增版本">
              {versionFormError}
            </Alert>
          )}

          <Alert variant="info" title="發布方式">
            新版本上傳後會立即發布；目前的已發布版本會自動轉為已作廢。
          </Alert>

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              label="版本號"
              htmlFor="version-number"
              error={versionFieldErrors.version}
              hint="例如 輸入 2 會儲存為 2.0。"
              required
            >
              <Input
                id="version-number"
                type="text"
                inputMode="decimal"
                autoComplete="off"
                placeholder="例如 2.1"
                value={versionValues.version}
                error={versionFieldErrors.version !== undefined}
                aria-describedby={
                  versionFieldErrors.version
                    ? "version-number-error"
                    : "version-number-hint"
                }
                onChange={(event) =>
                  updateVersionField("version", event.target.value)
                }
              />
            </FormField>

            <FormField
              label="生效日期"
              htmlFor="version-effective-date"
              error={versionFieldErrors.effectiveDate}
              required
            >
              <Input
                id="version-effective-date"
                type="date"
                min={todayUtc()}
                value={versionValues.effectiveDate}
                error={versionFieldErrors.effectiveDate !== undefined}
                aria-describedby={
                  versionFieldErrors.effectiveDate
                    ? "version-effective-date-error"
                    : undefined
                }
                onChange={(event) =>
                  updateVersionField("effectiveDate", event.target.value)
                }
              />
            </FormField>
          </div>

          <FormField
            label="頁數"
            htmlFor="version-page-count"
            error={versionFieldErrors.pageCount}
            hint="選填；請輸入大於 0 的整數。"
          >
            <Input
              id="version-page-count"
              type="number"
              min="1"
              step="1"
              inputMode="numeric"
              value={versionValues.pageCount}
              error={versionFieldErrors.pageCount !== undefined}
              aria-describedby={
                versionFieldErrors.pageCount
                  ? "version-page-count-error"
                  : "version-page-count-hint"
              }
              onChange={(event) =>
                updateVersionField("pageCount", event.target.value)
              }
            />
          </FormField>

          <FormField
            label="備註"
            htmlFor="version-memo"
            error={versionFieldErrors.memo}
            hint="選填。"
          >
            <Textarea
              id="version-memo"
              value={versionValues.memo}
              error={versionFieldErrors.memo !== undefined}
              aria-describedby={
                versionFieldErrors.memo
                  ? "version-memo-error"
                  : "version-memo-hint"
              }
              onChange={(event) =>
                updateVersionField("memo", event.target.value)
              }
            />
          </FormField>

          <FormField
            label="PDF 檔案"
            htmlFor="version-file"
            error={versionFieldErrors.file}
            required
          >
            <Input
              id="version-file"
              type="file"
              accept=".pdf,application/pdf"
              error={versionFieldErrors.file !== undefined}
              aria-describedby={
                versionFieldErrors.file
                  ? "version-file-error"
                  : "version-file-hint"
              }
              onChange={(event) => {
                setVersionValues((current) => ({
                  ...current,
                  file: event.target.files?.[0] ?? null,
                }));
                setVersionFieldErrors((current) => ({
                  ...current,
                  file: undefined,
                }));
                setVersionFormError(undefined);
              }}
            />
          </FormField>
        </form>
      </Modal>
    </section>
  );
}

export default AdminDocumentDetailPage;
