import type { ButtonHTMLAttributes } from "react";

export type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
export type ButtonSize = "md" | "sm";

export interface ButtonProps
  extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className"> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  isPending?: boolean;
}

const VARIANT: Record<ButtonVariant, string> = {
  primary:
    "bg-primary text-on-primary hover:bg-primary-hover active:bg-primary-hover",
  secondary:
    "border border-line bg-surface text-ink hover:bg-surface-header",
  ghost:
    "bg-transparent text-ink-muted hover:bg-surface-header hover:text-primary",
  danger:
    "bg-state-danger text-on-primary hover:brightness-95",
};

const SIZE: Record<ButtonSize, string> = {
  md: "h-control px-3 text-control",
  sm: "h-control-sm px-2 text-control",
};

export function Button({
  variant = "secondary",
  size = "md",
  isPending = false,
  disabled,
  children,
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      disabled={disabled || isPending}
      className={[
        "rounded-sm font-medium disabled:cursor-not-allowed disabled:bg-surface-header disabled:text-ink-disabled",
        VARIANT[variant],
        SIZE[size],
      ].join(" ")}
    >
      {children}
    </button>
  );
}
