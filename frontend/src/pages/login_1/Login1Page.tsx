import axios from "axios";
import {
  ArrowRight,
  Eye,
  EyeOff,
  LockKeyhole,
  TriangleAlert,
  UserRound,
} from "lucide-react";
import { useState, type FormEvent, type KeyboardEvent } from "react";
import { useLocation, useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import { Button, FormField, Input } from "@/components/common";
import { env } from "@/config/env";
import { useLogin } from "@/features/auth/queries";
import { loginSchema, type LoginFormValues } from "@/features/auth/schemas";
import { AUTH_FORM_TYPOGRAPHY } from "@/features/auth/ui";

import { EnvironmentBadge } from "../login/components/EnvironmentBadge";
import { SecurityNotice } from "../login/components/SecurityNotice";
import { SystemAnnouncement } from "../login/components/SystemAnnouncement";
import { SystemVersion } from "../login/components/SystemVersion";
import { LoginHeroArt } from "./components/LoginHeroArt";

type FieldErrors = Partial<Record<keyof LoginFormValues, string>>;

const SYSTEM_NAME = "首都集團 ISO 文件管理系統";

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0];
}

function requestedPath(state: unknown): string | undefined {
  if (typeof state !== "object" || state === null || !("from" in state)) {
    return undefined;
  }

  return typeof state.from === "string" ? state.from : undefined;
}

export function Login1Page() {
  const navigate = useNavigate();
  const location = useLocation();
  const loginMutation = useLogin();
  const [values, setValues] = useState<LoginFormValues>({
    empno: "",
    password: "",
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [showPassword, setShowPassword] = useState(false);
  const [capsLockOn, setCapsLockOn] = useState(false);

  function updateField(field: keyof LoginFormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError(undefined);
  }

  function syncCapsLock(event: KeyboardEvent<HTMLInputElement>) {
    setCapsLockOn(event.getModifierState("CapsLock"));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (loginMutation.isPending) {
      return;
    }

    setFormError(undefined);

    const parsed = loginSchema.safeParse(values);
    if (!parsed.success) {
      const flattened = parsed.error.flatten().fieldErrors;
      setFieldErrors({
        empno: firstMessage(flattened.empno),
        password: firstMessage(flattened.password),
      });
      return;
    }

    setFieldErrors({});
    loginMutation.mutate(parsed.data, {
      onSuccess: () => {
        const destination = requestedPath(location.state) ?? "/";
        navigate(destination, { replace: true });
      },
      onError: (error) => {
        if (error instanceof ApiError) {
          const errors = error.fieldErrors();
          setFieldErrors({
            empno: firstMessage(errors.empno),
            password: firstMessage(errors.password),
          });
          setFormError(
            error.status === 401
              ? "員工帳號或密碼錯誤，請重新確認。"
              : "系統目前無法完成登入，請稍後再試。",
          );
          return;
        }

        if (axios.isAxiosError(error) && error.response === undefined) {
          setFormError("目前無法連線至系統，請確認網路連線後再試。");
          return;
        }

        setFormError("系統目前無法完成登入，請稍後再試。");
      },
    });
  }

  return (
    <main
      className={`login-1 ${AUTH_FORM_TYPOGRAPHY} flex min-h-dvh flex-col bg-login-page`}
    >
      <header className="border-b-4 border-login-brand-deep bg-login-brand text-login-brand-text">
        <div className="mx-auto flex h-16 w-full max-w-[1120px] items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex min-w-0 items-center gap-3">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
              ISO
            </span>
            <div className="min-w-0">
              <p className="truncate text-section-label font-semibold">
                {SYSTEM_NAME}
              </p>
              <p
                className="mt-0.5 hidden text-fine tracking-wide text-login-brand-muted md:block"
                lang="en"
              >
                ISO DOCUMENT CONTROL SYSTEM
              </p>
            </div>
          </div>
          <p className="hidden shrink-0 text-fine text-login-brand-muted md:block">
            僅供首都集團內部授權人員使用
          </p>
        </div>
      </header>

      <div className="flex flex-1 items-center py-4 sm:py-5">
        <div className="mx-auto w-full max-w-[1120px] px-4 sm:px-6">
          {/* Mobile stacks; tablet keeps two columns (36/64); desktop is 42/58. */}
          <div className="grid grid-cols-1 overflow-hidden rounded-lg border border-login-card-border bg-surface shadow-float md:grid-cols-[36%_64%] xl:grid-cols-[42%_58%]">
            {/* Decorative artwork only; no text sits on the photo (doc 13). */}
            {/* Mobile: 16:7 strip capped at 180px so the form stays in view. */}
            <div
              className="relative aspect-[16/7] max-h-[180px] overflow-hidden md:aspect-auto md:h-auto md:max-h-none"
              aria-hidden="true"
            >
              <LoginHeroArt />
            </div>

            {/* Same colour as the artwork's right chevron so the two panels join. */}
            <section className="flex flex-col justify-center bg-login-help px-6 py-7 md:px-8 md:py-8 lg:px-10">
              <div className="mx-auto w-full max-w-[440px]">
                <header>
                  <EnvironmentBadge
                    environment={env.appEnvironment}
                    variant="subtle"
                  />
                  <h1 className="mt-4 text-[22px] leading-tight font-bold text-ink md:text-2xl">
                    登入系統
                  </h1>
                  <p className="mt-2 text-[15px] leading-[1.7] text-ink-muted">
                    使用公司員工帳號登入，存取您所屬公司與部門授權的 ISO 文件。
                  </p>
                </header>

                <SystemAnnouncement message="" />

                {formError && (
                  <div
                    className="mt-5 border-l-2 border-state-danger bg-state-danger-subtle px-3 py-3 text-meta text-state-danger"
                    role="alert"
                  >
                    {formError}
                  </div>
                )}

                <form
                  className="mt-6 flex flex-col gap-[18px] md:gap-5"
                  noValidate
                  onSubmit={handleSubmit}
                >
                  <FormField
                    label="員工帳號"
                    htmlFor="empno"
                    error={fieldErrors.empno}
                    required
                  >
                    <div className="relative">
                      <UserRound
                        className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-ink-muted"
                        aria-hidden="true"
                        size={16}
                        strokeWidth={1.75}
                      />
                      <Input
                        id="empno"
                        name="empno"
                        autoComplete="username"
                        autoFocus
                        placeholder="請輸入員工帳號"
                        value={values.empno}
                        error={fieldErrors.empno !== undefined}
                        disabled={loginMutation.isPending}
                        aria-describedby={
                          fieldErrors.empno ? "empno-error" : undefined
                        }
                        className="pl-10"
                        onChange={(event) =>
                          updateField("empno", event.target.value)
                        }
                      />
                    </div>
                  </FormField>

                  <FormField
                    label="密碼"
                    htmlFor="password"
                    error={fieldErrors.password}
                    hint={
                      capsLockOn ? (
                        <span
                          className="inline-flex items-center gap-1.5 text-state-expiring"
                          aria-live="polite"
                        >
                          <TriangleAlert
                            aria-hidden="true"
                            size={15}
                            strokeWidth={1.75}
                          />
                          Caps Lock 已開啟
                        </span>
                      ) : undefined
                    }
                    required
                  >
                    <div className="relative">
                      <LockKeyhole
                        className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-ink-muted"
                        aria-hidden="true"
                        size={16}
                        strokeWidth={1.75}
                      />
                      <Input
                        id="password"
                        name="password"
                        type={showPassword ? "text" : "password"}
                        autoComplete="current-password"
                        placeholder="請輸入密碼"
                        value={values.password}
                        error={fieldErrors.password !== undefined}
                        disabled={loginMutation.isPending}
                        aria-describedby={
                          fieldErrors.password
                            ? "password-error"
                            : capsLockOn
                              ? "password-hint"
                              : undefined
                        }
                        className="px-10"
                        onBlur={() => setCapsLockOn(false)}
                        onKeyDown={syncCapsLock}
                        onKeyUp={syncCapsLock}
                        onChange={(event) =>
                          updateField("password", event.target.value)
                        }
                      />
                      <button
                        type="button"
                        className="absolute inset-y-0 right-0 flex w-10 cursor-pointer items-center justify-center rounded-sm text-ink-muted transition-colors hover:text-primary disabled:cursor-not-allowed disabled:text-ink-disabled"
                        disabled={loginMutation.isPending}
                        aria-label={showPassword ? "隱藏密碼" : "顯示密碼"}
                        aria-pressed={showPassword}
                        onClick={() => setShowPassword((current) => !current)}
                      >
                        {showPassword ? (
                          <EyeOff
                            aria-hidden="true"
                            size={18}
                            strokeWidth={1.75}
                          />
                        ) : (
                          <Eye
                            aria-hidden="true"
                            size={18}
                            strokeWidth={1.75}
                          />
                        )}
                      </button>
                    </div>
                  </FormField>

                  <Button
                    className="mt-0.5 w-full text-base! font-semibold! md:mt-1"
                    type="submit"
                    size="md"
                    loading={loginMutation.isPending}
                    loadingText="登入中..."
                  >
                    <span>登入</span>
                    <ArrowRight
                      aria-hidden="true"
                      size={17}
                      strokeWidth={1.75}
                    />
                  </Button>
                </form>

                <div className="mt-5 rounded-sm border-l-[3px] border-line-strong bg-canvas px-3.5 py-3 text-meta leading-6">
                  <p className="font-medium text-ink">無法登入？</p>
                  <p className="text-ink-muted">
                    請洽資訊中心協助確認帳號與系統權限。
                  </p>
                </div>
              </div>
            </section>
          </div>

          <footer className="mt-4">
            <div className="text-center text-fine leading-5 text-ink-muted md:hidden">
              <p className="font-medium text-ink">公司內部資訊系統</p>
              <p className="tabular mt-0.5">Version {env.appVersion}</p>
            </div>
            <div className="hidden md:flex md:items-start md:justify-between md:gap-6">
              <SecurityNotice />
              <div className="shrink-0">
                <SystemVersion />
              </div>
            </div>
          </footer>
        </div>
      </div>
    </main>
  );
}

export default Login1Page;
