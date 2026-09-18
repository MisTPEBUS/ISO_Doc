import type { AppEnvironment } from "@/config/env";

interface EnvironmentStyle {
  label: string;
  englishLabel: string;
  className: string;
}

const ENVIRONMENT_STYLES: Record<AppEnvironment, EnvironmentStyle> = {
  development: {
    label: "開發環境",
    englishLabel: "Development",
    className: "border-line-strong bg-surface-header text-ink-muted",
  },
  uat: {
    label: "驗收環境",
    englishLabel: "UAT",
    className:
      "border-line-strong bg-state-expiring-subtle text-state-expiring",
  },
  production: {
    label: "測試環境",
    englishLabel: "Development",
    className: "border-line-strong bg-primary-subtle text-primary",
  },
};

export interface EnvironmentBadgeProps {
  environment: AppEnvironment;
}

export function EnvironmentBadge({ environment }: EnvironmentBadgeProps) {
  const style = ENVIRONMENT_STYLES[environment];

  return (
    <span
      className={`inline-flex h-7 items-center gap-1.5 rounded-md border px-2 text-fine font-medium ${style.className}`}
    >
      <span>{style.label}</span>
      <span aria-hidden="true">·</span>
      <span lang="en">{style.englishLabel}</span>
    </span>
  );
}
