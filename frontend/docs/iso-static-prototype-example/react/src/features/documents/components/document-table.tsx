import { useState } from "react";
import type { IsoDocument } from "../types";
import { DownloadAction } from "./download-action";
import { StatusIndicator } from "./status-indicator";

export interface DocumentTableProps {
  rows: IsoDocument[];
  onDownload: (id: string) => Promise<void>;
}

function formatRevision(value: number): string {
  return `V${String(value).padStart(2, "0")}`;
}

export function DocumentTable({
  rows,
  onDownload,
}: DocumentTableProps) {
  const [pendingId, setPendingId] = useState<string | null>(null);

  const handleDownload = async (id: string) => {
    try {
      setPendingId(id);
      await onDownload(id);
    } finally {
      setPendingId(null);
    }
  };

  if (rows.length === 0) {
    return (
      <div className="p-12 text-center text-cell text-ink-muted">
        <strong className="mb-1 block font-medium text-ink">
          沒有符合條件的文件
        </strong>
        <span>請調整查詢條件或清除篩選。</span>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto bg-surface">
      <table className="w-full min-w-[1180px] border-collapse">
        <thead>
          <tr className="h-table-header bg-surface-header text-left text-table-header text-ink-muted">
            <th className="min-w-24 px-3">生效狀態</th>
            <th className="min-w-[132px] px-3">文件編號</th>
            <th className="min-w-[280px] px-3">名稱</th>
            <th className="min-w-16 px-3 text-right">頁數</th>
            <th className="min-w-16 px-3 text-right">版本</th>
            <th className="min-w-28 px-3 text-right">發行日期</th>
            <th className="min-w-28 px-3 text-right">生效日期</th>
            <th className="min-w-28 px-3">公司別</th>
            <th className="min-w-36 px-3">備註</th>
            <th className="min-w-20 px-3">表單及附件</th>
          </tr>
        </thead>

        <tbody>
          {rows.map((document, index) => (
            <tr
              key={document.id}
              className={[
                "h-row border-b border-line text-cell hover:bg-surface-hover",
                index % 2 === 1 ? "bg-surface-zebra" : "",
                document.status === "expiring"
                  ? "bg-state-expiring-subtle"
                  : "",
              ].join(" ")}
            >
              <td className="px-3">
                <StatusIndicator status={document.status} />
              </td>
              <td className="px-3 font-mono text-code tabular">
                {document.code}
              </td>
              <td
                className={[
                  "px-3 font-medium",
                  document.status === "obsolete"
                    ? "text-ink-muted line-through"
                    : "",
                ].join(" ")}
              >
                {document.title}
              </td>
              <td className="px-3 text-right tabular">{document.pages}</td>
              <td className="px-3 text-right font-mono text-revision tabular">
                {formatRevision(document.revision)}
              </td>
              <td className="px-3 text-right text-meta text-ink-muted tabular">
                {document.issuedDate ?? "－"}
              </td>
              <td className="px-3 text-right text-meta text-ink-muted tabular">
                {document.effectiveDate ?? "－"}
              </td>
              <td className="px-3 text-meta text-ink-muted">
                {document.companyName}
              </td>
              <td className="px-3 text-meta text-ink-muted">
                {document.note ?? "－"}
              </td>
              <td className="px-3">
                <DownloadAction
                  document={document}
                  isPending={pendingId === document.id}
                  onDownload={handleDownload}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
