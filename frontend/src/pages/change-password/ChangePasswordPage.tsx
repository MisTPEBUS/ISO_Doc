import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";

import { ApiError } from "@/api/httpClient";
import { Alert, Button, FormField, Input } from "@/components/common";
import {
  useChangePassword,
  useCurrentUser,
  useLogout,
} from "@/features/auth/queries";
import {
  changePasswordSchema,
  type ChangePasswordFormValues,
} from "@/features/auth/schemas";

type FieldName = keyof ChangePasswordFormValues;
type FieldErrors = Partial<Record<FieldName, string>>;

const EMPTY_FORM: ChangePasswordFormValues = {
  currentPassword: "",
  newPassword: "",
  newPasswordConfirmation: "",
};

function firstMessage(messages: string[] | undefined): string | undefined {
  return messages?.[0];
}

interface PasswordFieldProps {
  id: FieldName;
  label: string;
  autoComplete: string;
  value: string;
  error?: string;
  hint?: string;
  visible: boolean;
  autoFocus?: boolean;
  onToggleVisible: () => void;
  onChange: (value: string) => void;
}

function PasswordField({
  id,
  label,
  autoComplete,
  value,
  error,
  hint,
  visible,
  autoFocus,
  onToggleVisible,
  onChange,
}: PasswordFieldProps) {
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <FormField label={label} htmlFor={id} error={error} hint={hint} required>
      <div className="relative">
        <Input
          id={id}
          name={id}
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          autoFocus={autoFocus}
          value={value}
          error={error !== undefined}
          aria-describedby={describedBy}
          className="pr-16"
          onChange={(event) => onChange(event.target.value)}
        />
        <button
          type="button"
          className="absolute inset-y-0 right-0 min-w-14 px-2 text-label font-medium text-primary hover:text-primary-hover"
          aria-label={visible ? "隱藏密碼" : "顯示密碼"}
          aria-pressed={visible}
          onClick={onToggleVisible}
        >
          {visible ? "隱藏" : "顯示"}
        </button>
      </div>
    </FormField>
  );
}

export function ChangePasswordPage() {
  const navigate = useNavigate();
  const currentUser = useCurrentUser();
  const changePassword = useChangePassword();
  const logout = useLogout();

  const [values, setValues] = useState<ChangePasswordFormValues>(EMPTY_FORM);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [succeeded, setSucceeded] = useState(false);
  const [visible, setVisible] = useState(false);

  const mustChangePassword = currentUser.data?.mustChangePassword ?? false;

  function updateField(field: FieldName, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
    setFieldErrors((current) => ({ ...current, [field]: undefined }));
    setFormError(undefined);
  }

  function returnToLogin() {
    // 密碼已變更：結束目前 session 並回登入頁，改用新密碼登入。
    // 先導頁再登出，避免 LoginGuard 夾帶 from=/change-password 造成登入後又跳回本頁。
    logout.mutate();
    navigate("/login", { replace: true });
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(undefined);

    const parsed = changePasswordSchema.safeParse(values);
    if (!parsed.success) {
      const flattened = parsed.error.flatten().fieldErrors;
      setFieldErrors({
        currentPassword: firstMessage(flattened.currentPassword),
        newPassword: firstMessage(flattened.newPassword),
        newPasswordConfirmation: firstMessage(flattened.newPasswordConfirmation),
      });
      return;
    }

    setFieldErrors({});
    changePassword.mutate(parsed.data, {
      onSuccess: () => {
        setValues(EMPTY_FORM);
        setSucceeded(true);
      },
      onError: (error) => {
        if (error instanceof ApiError) {
          // 密碼相關失敗一律 400（含「目前密碼錯誤」），不可導回登入頁。
          if (error.status === 400) {
            const errors = error.fieldErrors();
            const mapped: FieldErrors = {
              currentPassword: firstMessage(errors.currentPassword),
              newPassword: firstMessage(errors.newPassword),
              newPasswordConfirmation: firstMessage(
                errors.newPasswordConfirmation,
              ),
            };
            setFieldErrors(mapped);
            if (!Object.values(mapped).some(Boolean)) {
              setFormError(
                error.detail ?? "無法更新密碼，請重新確認輸入內容。",
              );
            }
            return;
          }

          setFormError(error.detail ?? "無法更新密碼，請稍後再試。");
          return;
        }

        setFormError("目前無法連線到系統，請確認網路後再試。");
      },
    });
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-canvas px-4 py-8 sm:px-8">
      <div className="w-full max-w-md">
        <div className="mb-8 flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
            ISO
          </span>
          <div>
            <p className="text-section-label font-semibold text-ink">
              ISO 文件管理系統
            </p>
            <p className="text-fine text-ink-muted">DOCUMENT CONTROL SYSTEM</p>
          </div>
        </div>

        <div className="border border-line-strong bg-surface p-6 sm:p-8">
          <header className="mb-8">
            <p className="mb-2 text-label font-medium text-primary">帳號安全</p>
            <h1 className="text-page-title text-ink">修改密碼</h1>
            <p className="mt-2 text-meta text-ink-muted">
              新密碼不可與目前密碼相同，變更後需以新密碼重新登入。
            </p>
          </header>

          {mustChangePassword && !succeeded && (
            <Alert variant="warning" title="請先變更密碼" className="mb-5">
              你目前使用的是系統配發的臨時密碼，請設定新密碼後再繼續使用系統。
            </Alert>
          )}

          {succeeded ? (
            <>
              <Alert variant="success" title="密碼已更新">
                請使用新密碼重新登入。
              </Alert>
              <Button className="mt-5 w-full" onClick={returnToLogin}>
                重新登入
              </Button>
            </>
          ) : (
            <>
              {formError && (
                <Alert variant="error" title="無法更新密碼" className="mb-5">
                  {formError}
                </Alert>
              )}

              <form className="space-y-5" noValidate onSubmit={handleSubmit}>
                <PasswordField
                  id="currentPassword"
                  label="目前密碼"
                  autoComplete="current-password"
                  autoFocus
                  value={values.currentPassword}
                  error={fieldErrors.currentPassword}
                  visible={visible}
                  onToggleVisible={() => setVisible((current) => !current)}
                  onChange={(value) => updateField("currentPassword", value)}
                />

                <PasswordField
                  id="newPassword"
                  label="新密碼"
                  autoComplete="new-password"
                  value={values.newPassword}
                  error={fieldErrors.newPassword}
                  visible={visible}
                  onToggleVisible={() => setVisible((current) => !current)}
                  onChange={(value) => updateField("newPassword", value)}
                />

                <PasswordField
                  id="newPasswordConfirmation"
                  label="確認新密碼"
                  autoComplete="new-password"
                  value={values.newPasswordConfirmation}
                  error={fieldErrors.newPasswordConfirmation}
                  visible={visible}
                  onToggleVisible={() => setVisible((current) => !current)}
                  onChange={(value) =>
                    updateField("newPasswordConfirmation", value)
                  }
                />

                <div className="flex gap-2 pt-2">
                  <Button
                    className="flex-1"
                    type="submit"
                    loading={changePassword.isPending}
                    loadingText="更新中"
                  >
                    更新密碼
                  </Button>
                  {!mustChangePassword && (
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={changePassword.isPending}
                      onClick={() => navigate(-1)}
                    >
                      取消
                    </Button>
                  )}
                </div>
              </form>
            </>
          )}
        </div>
      </div>
    </main>
  );
}

export default ChangePasswordPage;
