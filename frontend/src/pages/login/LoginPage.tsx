import { useState, type FormEvent } from "react";
import {
  ArrowRight,
  Eye,
  EyeOff,
  FileCheck2,
  LockKeyhole,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import { Button, FormField, Input } from "@/components/common";
import { useLogin } from "@/features/auth/queries";
import { loginSchema, type LoginFormValues } from "@/features/auth/schemas";

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

  function updateField(field: keyof LoginFormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError(undefined);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
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
              ? "帳號或密碼不正確，請重新輸入。"
              : (error.detail ?? "登入失敗，請稍後再試。"),
          );
          return;
        }

        setFormError("目前無法連線到系統，請確認網路後再試。");
      },
    });
  }

  return (
    <main className="min-h-screen bg-canvas lg:grid lg:grid-cols-[minmax(0,1.08fr)_minmax(480px,0.92fr)]">
      <section
        className="relative hidden min-h-screen overflow-hidden bg-shell-900 bg-cover bg-center text-on-shell lg:flex lg:flex-col"
        aria-label="系統簡介"
        style={{
          backgroundImage: "url('/ISO/images/login-office-facade.webp')",
        }}
      >
        <div
          className="absolute inset-0 bg-linear-to-br from-shell-900/95 via-shell-900/75 to-shell-900/35"
          aria-hidden="true"
        />
        <div
          className="absolute inset-y-0 right-0 w-px bg-shell-700"
          aria-hidden="true"
        />

        <div className="relative flex items-center gap-3 p-12 xl:p-16">
          <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
            ISO
          </span>
          <div>
            <p className="text-section-label font-semibold">首都客運集團</p>
            <p className="mt-0.5 text-fine tracking-wider text-ink-faint">
              ISO DOCUMENT CONTROL SYSTEM
            </p>
          </div>
        </div>

        <div className="relative my-auto px-12 py-8 xl:px-16">
          <div className="flex items-center gap-2 text-label font-medium text-primary-on-shell">
            <FileCheck2 aria-hidden="true" size={16} strokeWidth={1.75} />
            <span>集團文件治理平台</span>
          </div>
          <h1 className="mt-4 max-w-2xl text-3xl leading-tight font-semibold tracking-tight xl:text-4xl">
            讓每一份營運制度文件，
            <br />
            都有正確版本與清楚依據。
          </h1>
          <p className="mt-6 max-w-xl text-cell leading-7 text-ink-faint">
            集中管理文件版本、部門權限與下載紀錄，讓每位同仁都能取得目前有效的
            ISO 文件。
          </p>

          <dl className="mt-12 grid max-w-2xl grid-cols-3 border-t border-shell-700 pt-6">
            <div className="pr-4">
              <dt className="text-label font-semibold text-on-shell">
                版本受控
              </dt>
              <dd className="mt-1 text-fine leading-5 text-ink-faint">
                辨識目前有效版本
              </dd>
            </div>
            <div className="border-l border-shell-700 px-4">
              <dt className="text-label font-semibold text-on-shell">
                權限分流
              </dt>
              <dd className="mt-1 text-fine leading-5 text-ink-faint">
                依部門開放存取
              </dd>
            </div>
            <div className="border-l border-shell-700 pl-4">
              <dt className="text-label font-semibold text-on-shell">
                軌跡可查
              </dt>
              <dd className="mt-1 text-fine leading-5 text-ink-faint">
                保留重要操作紀錄
              </dd>
            </div>
          </dl>
        </div>

        <div className="relative flex items-end justify-between gap-6 p-12 text-fine text-ink-faint xl:p-16">
          <p>僅供首都客運集團內部授權人員使用</p>
        </div>
      </section>

      <section className="relative flex min-h-screen min-w-0 items-center justify-center overflow-x-hidden bg-surface py-12 lg:px-12">
        <div
          className="absolute inset-x-0 top-0 h-1 bg-primary"
          aria-hidden="true"
        />

        <div className="mx-auto w-[calc(100%_-_3rem)] min-w-0 max-w-[420px] sm:w-[calc(100%_-_5rem)] lg:w-full">
          <div className="mb-12 flex items-center gap-3 lg:hidden">
            <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
              ISO
            </span>
            <div>
              <p className="text-section-label font-semibold text-ink">
                首都客運集團
              </p>
              <p className="mt-0.5 text-fine tracking-wider text-ink-muted">
                ISO DOCUMENT CONTROL SYSTEM
              </p>
            </div>
          </div>

          <header>
            <div className="flex items-center gap-2 text-label font-medium text-primary">
              <ShieldCheck aria-hidden="true" size={16} strokeWidth={1.75} />
              <span>安全登入</span>
            </div>
            <h2 className="mt-3 text-page-title text-ink">
              登入 ISO 文件管理系統
            </h2>
            <p className="mt-2 text-meta leading-6 text-ink-muted">
              請使用公司配發的員工帳號與密碼。
            </p>
          </header>

          {formError && (
            <div
              className="mt-6 border-l-2 border-state-danger bg-state-danger-subtle px-3 py-3 text-meta text-state-danger"
              role="alert"
            >
              {formError}
            </div>
          )}

          <form className="mt-8 space-y-6" noValidate onSubmit={handleSubmit}>
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
                  aria-describedby={
                    fieldErrors.empno ? "empno-error" : undefined
                  }
                  className="pl-10"
                  onChange={(event) => updateField("empno", event.target.value)}
                />
              </div>
            </FormField>

            <FormField
              label="密碼"
              htmlFor="password"
              error={fieldErrors.password}
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
                  aria-describedby={
                    fieldErrors.password ? "password-error" : undefined
                  }
                  className="px-10"
                  onChange={(event) =>
                    updateField("password", event.target.value)
                  }
                />
                <button
                  type="button"
                  className="absolute inset-y-0 right-0 flex w-10 cursor-pointer items-center justify-center rounded-sm text-ink-muted transition-colors hover:text-primary"
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
              loadingText="登入中"
            >
              <span>登入</span>
              <ArrowRight aria-hidden="true" size={17} strokeWidth={1.75} />
            </Button>
          </form>

          <div className="mt-8 border-t border-line pt-6">
            <div className="flex items-start gap-2 text-meta leading-6 text-ink-muted">
              <ShieldCheck
                className="mt-1 shrink-0 text-state-active"
                aria-hidden="true"
                size={16}
                strokeWidth={1.75}
              />
              <p>此為內部系統，連線與重要操作皆依公司資訊安全規範留存紀錄。</p>
            </div>
            <p className="mt-4 text-meta text-ink-muted">
              若無法登入，請聯絡系統管理員確認帳號狀態。
            </p>
          </div>
        </div>
      </section>
    </main>
  );
}

export default LoginPage;
