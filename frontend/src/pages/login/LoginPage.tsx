import { useState, type FormEvent } from "react";
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
              ? "員工編號或密碼不正確，請重新輸入。"
              : (error.detail ?? "登入失敗，請稍後再試。"),
          );
          return;
        }

        setFormError("目前無法連線到系統，請確認網路後再試。");
      },
    });
  }

  return (
    <main className="grid min-h-screen bg-canvas lg:grid-cols-[minmax(320px,42%)_1fr]">
      <section
        className="relative hidden overflow-hidden bg-shell-900 px-12 py-10 text-on-shell lg:flex lg:flex-col"
        aria-label="系統簡介"
      >
        <div
          className="absolute -right-24 -top-24 size-72 rounded-full border border-shell-700"
          aria-hidden="true"
        />
        <div
          className="absolute -right-8 -top-8 size-48 rounded-full border border-shell-700"
          aria-hidden="true"
        />

        <div className="relative flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
            ISO
          </span>
          <div>
            <p className="text-section-label font-semibold">ISO 文件管理系統</p>
            <p className="text-fine text-ink-faint">DOCUMENT CONTROL SYSTEM</p>
          </div>
        </div>

        <div className="relative my-auto max-w-md">
          <p className="mb-3 text-label font-medium text-primary-on-shell">
            企業文件治理平台
          </p>
          <h1 className="text-page-title">
            讓每一份制度文件，都保持正確、可控、可追溯。
          </h1>
          <p className="mt-4 text-cell leading-7 text-ink-faint">
            集中管理文件版本、部門權限與下載紀錄，讓同仁隨時取得目前有效的文件。
          </p>

          <dl className="mt-10 grid gap-5 border-t border-shell-700 pt-6">
            <div>
              <dt className="text-label font-medium text-on-shell">
                單一有效版本
              </dt>
              <dd className="mt-1 text-meta text-ink-faint">
                清楚辨識目前發布版本，降低誤用舊文件的風險。
              </dd>
            </div>
            <div>
              <dt className="text-label font-medium text-on-shell">
                依部門授權
              </dt>
              <dd className="mt-1 text-meta text-ink-faint">
                只顯示使用者所屬部門可存取的文件。
              </dd>
            </div>
            <div>
              <dt className="text-label font-medium text-on-shell">
                完整操作軌跡
              </dt>
              <dd className="mt-1 text-meta text-ink-faint">
                重要維護與下載行為皆保留稽核紀錄。
              </dd>
            </div>
          </dl>
        </div>

        <p className="relative text-fine text-ink-faint">
          僅供公司內部授權人員使用
        </p>
      </section>

      <section className="flex min-h-screen items-center justify-center px-4 py-8 sm:px-8">
        <div className="w-full max-w-md">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
              ISO
            </span>
            <div>
              <p className="text-section-label font-semibold text-ink">
                ISO 文件管理系統
              </p>
              <p className="text-fine text-ink-muted">
                DOCUMENT CONTROL SYSTEM
              </p>
            </div>
          </div>

          <div className="border border-line-strong bg-surface p-6 sm:p-8">
            <header className="mb-8">
              <p className="mb-2 text-label font-medium text-primary">
                帳號登入
              </p>
              <h2 className="text-page-title text-ink">歡迎回來</h2>
              <p className="mt-2 text-meta text-ink-muted">
                請使用公司配發的員工帳號登入系統。
              </p>
            </header>

            {formError && (
              <div
                className="mb-5 border border-state-danger bg-state-danger-subtle px-3 py-2.5 text-meta text-state-danger"
                role="alert"
              >
                {formError}
              </div>
            )}

            <form className="space-y-5" noValidate onSubmit={handleSubmit}>
              <FormField
                label="員工編號"
                htmlFor="empno"
                error={fieldErrors.empno}
                required
              >
                <Input
                  id="empno"
                  name="empno"
                  autoComplete="username"
                  autoFocus
                  value={values.empno}
                  error={fieldErrors.empno !== undefined}
                  aria-describedby={
                    fieldErrors.empno ? "empno-error" : undefined
                  }
                  onChange={(event) => updateField("empno", event.target.value)}
                />
              </FormField>

              <FormField
                label="密碼"
                htmlFor="password"
                error={fieldErrors.password}
                required
              >
                <div className="relative">
                  <Input
                    id="password"
                    name="password"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    value={values.password}
                    error={fieldErrors.password !== undefined}
                    aria-describedby={
                      fieldErrors.password ? "password-error" : undefined
                    }
                    className="pr-16"
                    onChange={(event) =>
                      updateField("password", event.target.value)
                    }
                  />
                  <button
                    type="button"
                    className="absolute inset-y-0 right-0 min-w-14 px-2 text-label font-medium text-primary hover:text-primary-hover"
                    aria-label={showPassword ? "隱藏密碼" : "顯示密碼"}
                    aria-pressed={showPassword}
                    onClick={() => setShowPassword((current) => !current)}
                  >
                    {showPassword ? "隱藏" : "顯示"}
                  </button>
                </div>
              </FormField>

              <Button
                className="mt-2 w-full"
                type="submit"
                size="md"
                loading={loginMutation.isPending}
                loadingText="登入中"
              >
                登入
              </Button>
            </form>
          </div>

          <p className="mt-5 text-center text-fine text-ink-muted">
            若無法登入，請聯絡系統管理員確認帳號狀態。
          </p>
        </div>
      </section>
    </main>
  );
}

export default LoginPage;
