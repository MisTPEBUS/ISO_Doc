import type { DocumentStatus } from "../types";

export interface StatusIndicatorProps {
  status: DocumentStatus;
}

const STATUS: Record<
  DocumentStatus,
  { label: string; dot: string; text: string }
> = {
  active: {
    label: "有效",
    dot: "bg-state-active",
    text: "text-state-active",
  },
  review: {
    label: "待生效",
    dot: "bg-state-review",
    text: "text-state-review",
  },
  expiring: {
    label: "即期換版",
    dot: "bg-state-expiring",
    text: "text-state-expiring",
  },
  obsolete: {
    label: "已作廢",
    dot: "bg-state-obsolete",
    text: "text-state-obsolete",
  },
};

export function StatusIndicator({ status }: StatusIndicatorProps) {
  const config = STATUS[status];

  return (
    <span className={`inline-flex items-center gap-1.5 text-label ${config.text}`}>
      <span
        className={`size-[6px] rounded-full ${config.dot}`}
        aria-hidden="true"
      />
      <span>{config.label}</span>
    </span>
  );
}
