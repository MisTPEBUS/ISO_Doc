import type { AppEnvironment } from "@/config/env";

interface EnvironmentStyle {
  label: string;
  englishLabel: string;
  className: string;
  /** Softer colours for the compact `subtle` variant. */
  subtleClassName: string;
}

const ENVIRONMENT_STYLES: Record<AppEnvironment, EnvironmentStyle> = {
  development: {
    label: "開發環境",
    englishLabel: "Development",
    className: "border-line-strong bg-surface-header text-ink-muted",
    subtleClassName: "border-line bg-surface-header text-ink-muted",
  },
  uat: {
    label: "驗收環境",
    englishLabel: "UAT",
    className:
      "border-line-strong bg-state-expiring-subtle text-state-expiring",
    subtleClassName: "border-line bg-state-expiring-subtle text-state-expiring",
  },
  production: {
    label: "測試環境",
    englishLabel: "Development",
    className: "border-line-strong bg-primary-subtle text-primary",
    subtleClassName:
      "border-login-badge-border bg-primary-subtle text-primary-hover",
  },
};

const VARIANT_CLASSES = {
  default: "h-7 px-2 text-fine",
  subtle: "px-2 py-1 text-[13px] leading-[1.2]",
} as const;

export interface EnvironmentBadgeProps {
  environment: AppEnvironment;
  /** `subtle` is the compact badge used by /login_1. */
  variant?: keyof typeof VARIANT_CLASSES;
}

export function EnvironmentBadge({
  environment,
  variant = "default",
}: EnvironmentBadgeProps) {
  const style = ENVIRONMENT_STYLES[environment];
  const colorClassName =
    variant === "subtle" ? style.subtleClassName : style.className;

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-md border font-medium ${VARIANT_CLASSES[variant]} ${colorClassName}`}
    >
      <span>{style.label}</span>
      <span aria-hidden="true">·</span>
      <span lang="en">{style.englishLabel}</span>
    </span>
  );
}
