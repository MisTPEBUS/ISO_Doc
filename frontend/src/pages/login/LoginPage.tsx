import axios from "axios";
import {
  ArrowRight,
  Eye,
  EyeOff,
  FileCheck2,
  History,
  LockKeyhole,
  Paperclip,
  ShieldCheck,
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

import { EnvironmentBadge } from "./components/EnvironmentBadge";
import { SecurityNotice } from "./components/SecurityNotice";
import { SystemAnnouncement } from "./components/SystemAnnouncement";
import { SystemVersion } from "./components/SystemVersion";

type FieldErrors = Partial<Record<keyof LoginFormValues, string>>;

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0];
}

function requestedPath(state: unknown): string | undefined {
  if (typeof state !== "object" || state === null || !("from" in state)) {
    return undefined;
  }

  return typeof state.from === "string" ? state.from : undefined;
}

export function LoginPage() {
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
      className={`${AUTH_FORM_TYPOGRAPHY} h-dvh min-h-0 overflow-hidden bg-canvas lg:grid lg:grid-cols-[minmax(0,56%)_minmax(0,44%)]`}
    >
      <section
        className="relative hidden h-full min-h-0 overflow-hidden bg-shell-900 text-on-shell lg:flex lg:flex-col"
        aria-label="系統簡介"
      >
        <img
          className="absolute inset-0 size-full object-cover object-center"
          src="/ISO/images/login-office-facade.webp"
          alt=""
          aria-hidden="true"
        />
        <div className="absolute inset-0 bg-shell-900/80" aria-hidden="true" />
        <div
          className="absolute inset-y-0 right-0 w-px bg-shell-700"
          aria-hidden="true"
        />

        <div className="relative flex items-center gap-3 px-12 py-10 xl:px-16">
          <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
            ISO
          </span>
          <div>
            <p className="text-section-label font-semibold">首都客運集團</p>
            <p className="mt-0.5 text-fine tracking-wide text-ink-faint">
              ISO DOCUMENT CONTROL SYSTEM
            </p>
          </div>
        </div>

        <div className="relative my-auto max-w-3xl px-12 py-8 xl:px-16">
          <p className="text-label font-medium text-primary-on-shell">
            ISO 文件管理平台
          </p>
          <h1 className="mt-3 max-w-xl text-2xl leading-tight font-semibold tracking-tight xl:text-3xl">
            讓每一份制度文件，
            <br />
            都有正確版本、明確權限與完整紀錄。
          </h1>
          <p className="mt-4 max-w-xl text-cell leading-7 text-ink-faint">
            集中管理集團 ISO 文件、版本與附件，
            <br />
            確保同仁隨時取得目前有效且經授權的文件。
          </p>

          <dl className="mt-10 grid max-w-2xl grid-cols-2 gap-x-8 gap-y-6 border-t border-shell-700 pt-6">
            <div className="flex items-start gap-3">
              <FileCheck2
                className="mt-0.5 shrink-0 text-primary-on-shell"
                aria-hidden="true"
                size={18}
                strokeWidth={1.75}
              />
              <div>
                <dt className="text-label font-semibold text-on-shell">
                  版本控管
                </dt>
                <dd className="mt-1 text-fine leading-5 text-ink-faint">
                  即時取得目前有效版本
                </dd>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <ShieldCheck
                className="mt-0.5 shrink-0 text-primary-on-shell"
                aria-hidden="true"
                size={18}
                strokeWidth={1.75}
              />
              <div>
                <dt className="text-label font-semibold text-on-shell">
                  權限管理
                </dt>
                <dd className="mt-1 text-fine leading-5 text-ink-faint">
                  依公司與部門提供授權文件
                </dd>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <Paperclip
                className="mt-0.5 shrink-0 text-primary-on-shell "
                aria-hidden="true"
                size={18}
                strokeWidth={1.75}
              />
              <div>
                <dt className="text-label font-semibold text-on-shell">
                  附件管理
                </dt>
                <dd className="mt-1 text-fine leading-5 text-ink-faint">
                  主文件、附件與版本統一管理
                </dd>
              </div>
            </div>
            <div className="flex items-start gap-3">
              <History
                className="mt-0.5 shrink-0 text-primary-on-shell"
                aria-hidden="true"
                size={18}
                strokeWidth={1.75}
              />
              <div>
                <dt className="text-label font-semibold text-on-shell">
                  操作追蹤
                </dt>
                <dd className="mt-1 text-fine leading-5 text-ink-faint">
                  文件異動與下載紀錄完整留存
                </dd>
              </div>
            </div>
          </dl>
        </div>

        <p className="relative px-12 py-10 text-fine text-ink-faint xl:px-16">
          僅供首都客運集團內部授權人員使用
        </p>
      </section>

      <section className="h-full min-h-0 min-w-0 overflow-hidden bg-surface">
        <div className="mx-auto flex h-full min-h-0 w-[calc(100%_-_3rem)] max-w-[480px] flex-col py-2 sm:w-[calc(100%_-_5rem)] sm:py-4 lg:w-[calc(100%_-_4rem)] lg:py-6 xl:w-full">
          <div className="flex items-center gap-3 lg:hidden">
            <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
              ISO
            </span>
            <div>
              <p className="text-section-label font-semibold text-ink">
                首都客運集團
              </p>
              <p className="mt-0.5 text-fine tracking-wide text-ink-muted">
                ISO DOCUMENT SYSTEM
              </p>
            </div>
          </div>

          <div className="my-auto py-1 sm:py-4 lg:py-5">
            <header>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2 text-label font-medium text-primary">
                  <ShieldCheck
                    aria-hidden="true"
                    size={16}
                    strokeWidth={1.75}
                  />
                  <span>安全登入</span>
                </div>
                <EnvironmentBadge environment={env.appEnvironment} />
              </div>
              <h2 className="mt-3 text-page-title font-semibold text-ink sm:mt-4">
                登入 ISO 文件管理系統
              </h2>
              <p className="mt-2 text-meta leading-6 text-ink-muted">
                使用公司員工帳號登入，存取您所屬公司與部門授權的 ISO 文件。
              </p>
            </header>

            <SystemAnnouncement message="" />

            {formError && (
              <div
                className="mt-6 border-l-2 border-state-danger bg-state-danger-subtle px-3 py-3 text-meta text-state-danger"
                role="alert"
              >
                {formError}
              </div>
            )}

            <form
              className="mt-5 space-y-2 sm:mt-6"
              noValidate
              onSubmit={handleSubmit}
            >
              <FormField
                label="員工帳號"
                htmlFor="empno"
                error={fieldErrors.empno}
                required
                className="min-h-[88px] sm:min-h-24"
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
                className="min-h-[88px] sm:min-h-24"
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
                      <EyeOff aria-hidden="true" size={18} strokeWidth={1.75} />
                    ) : (
                      <Eye aria-hidden="true" size={18} strokeWidth={1.75} />
                    )}
                  </button>
                </div>
              </FormField>

              <Button
                className="w-full"
                type="submit"
                size="md"
                loading={loginMutation.isPending}
                loadingText="登入中..."
              >
                <span>登入</span>
                <ArrowRight aria-hidden="true" size={17} strokeWidth={1.75} />
              </Button>
            </form>

            <div className="mt-4 text-center text-meta leading-6">
              <p className="font-medium text-ink">無法登入？</p>
              <p className="text-ink-muted">
                請洽資訊中心協助確認帳號與系統權限。
              </p>
            </div>

            <div className="mt-4 border-t border-line pt-4">
              <SecurityNotice />
            </div>
          </div>

          <SystemVersion />
        </div>
      </section>
    </main>
  );
}

export default LoginPage;
