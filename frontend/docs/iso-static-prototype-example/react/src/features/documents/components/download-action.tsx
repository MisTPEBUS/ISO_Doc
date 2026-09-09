import type { IsoDocument } from "../types";

export interface DownloadActionProps {
  document: IsoDocument;
  isPending?: boolean;
  onDownload: (id: string) => Promise<void>;
}

export function DownloadAction({
  document,
  isPending = false,
  onDownload,
}: DownloadActionProps) {
  if (!document.canDownload || document.status === "obsolete") {
    return <span className="text-ink-disabled">－</span>;
  }

  return (
    <button
      type="button"
      className="h-control-sm rounded-sm px-2 text-control text-primary hover:bg-primary-subtle disabled:cursor-not-allowed disabled:text-ink-disabled"
      disabled={isPending}
      onClick={() => void onDownload(document.id)}
    >
      {isPending ? "下載中" : "下載"}
    </button>
  );
}
