import axios from "axios";
import { Eye, EyeOff, LockKeyhole, TriangleAlert } from "lucide-react";
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
import { AUTH_FORM_TYPOGRAPHY } from "@/features/auth/ui";

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
  placeholder: string;
  value: string;
  error?: string;
  visible: boolean;
  capsLockOn: boolean;
  disabled: boolean;
  autoFocus?: boolean;
  onToggleVisible: () => void;
  onCapsLockChange: (enabled: boolean) => void;
  onChange: (value: string) => void;
}

function PasswordField({
  id,
  label,
  autoComplete,
  placeholder,
  value,
  error,
  visible,
  capsLockOn,
  disabled,
  autoFocus,
  onToggleVisible,
  onCapsLockChange,
  onChange,
}: PasswordFieldProps) {
  const describedBy = error
    ? `${id}-error`
    : capsLockOn
      ? `${id}-hint`
      : undefined;

  return (
    <FormField
      label={label}
      htmlFor={id}
      error={error}
      hint={
        capsLockOn ? (
          <span
            className="inline-flex items-center gap-1.5 text-state-expiring"
            aria-live="polite"
          >
            <TriangleAlert aria-hidden="true" size={15} strokeWidth={1.75} />
            Caps Lock 已開啟
          </span>
        ) : undefined
      }
      required
      className="min-h-24"
    >
      <div className="relative">
        <LockKeyhole
          className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-ink-muted"
          aria-hidden="true"
          size={16}
          strokeWidth={1.75}
        />
        <Input
          id={id}
          name={id}
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          autoFocus={autoFocus}
          placeholder={placeholder}
          value={value}
          error={error !== undefined}
          disabled={disabled}
          aria-describedby={describedBy}
          className="px-10"
          onBlur={() => onCapsLockChange(false)}
          onKeyDown={(event) =>
            onCapsLockChange(event.getModifierState("CapsLock"))
          }
          onKeyUp={(event) =>
            onCapsLockChange(event.getModifierState("CapsLock"))
          }
          onChange={(event) => onChange(event.target.value)}
        />
        <button
          type="button"
          className="absolute inset-y-0 right-0 flex w-10 cursor-pointer items-center justify-center rounded-sm text-ink-muted transition-colors hover:text-primary disabled:cursor-not-allowed disabled:text-ink-disabled"
          disabled={disabled}
          aria-label={visible ? "隱藏密碼" : "顯示密碼"}
          aria-pressed={visible}
          onClick={onToggleVisible}
        >
          {visible ? (
            <EyeOff aria-hidden="true" size={18} strokeWidth={1.75} />
          ) : (
            <Eye aria-hidden="true" size={18} strokeWidth={1.75} />
          )}
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
  const [visibleFields, setVisibleFields] = useState<Record<FieldName, boolean>>({
    currentPassword: false,
    newPassword: false,
    newPasswordConfirmation: false,
  });
  const [capsLockField, setCapsLockField] = useState<FieldName>();

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

    if (changePassword.isPending) {
      return;
    }

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
              setFormError("無法更新密碼，請重新確認輸入內容。");
            }
            return;
          }

          setFormError("系統目前無法更新密碼，請稍後再試。");
          return;
        }

        if (axios.isAxiosError(error) && error.response === undefined) {
          setFormError("目前無法連線至系統，請確認網路連線後再試。");
          return;
        }

        setFormError("系統目前無法更新密碼，請稍後再試。");
      },
    });
  }

  return (
    <main
      className={`${AUTH_FORM_TYPOGRAPHY} flex min-h-screen items-center justify-center bg-canvas px-4 py-8 sm:px-8`}
    >
      <div className="w-full max-w-[480px]">
        <div className="mb-8 flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-sm bg-primary font-mono text-section-label font-semibold text-on-primary">
            ISO
          </span>
          <div>
            <p className="text-section-label font-semibold text-ink">
              首都客運集團
            </p>
            <p className="mt-0.5 text-fine tracking-wide text-ink-muted">
              ISO DOCUMENT CONTROL SYSTEM
            </p>
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
                <div
                  className="mb-5 border-l-2 border-state-danger bg-state-danger-subtle px-3 py-3 text-meta text-state-danger"
                  role="alert"
                >
                  {formError}
                </div>
              )}

              <form className="space-y-2" noValidate onSubmit={handleSubmit}>
                <PasswordField
                  id="currentPassword"
                  label="目前密碼"
                  autoComplete="current-password"
                  placeholder="請輸入目前密碼"
                  autoFocus
                  value={values.currentPassword}
                  error={fieldErrors.currentPassword}
                  visible={visibleFields.currentPassword}
                  capsLockOn={capsLockField === "currentPassword"}
                  disabled={changePassword.isPending}
                  onToggleVisible={() =>
                    setVisibleFields((current) => ({
                      ...current,
                      currentPassword: !current.currentPassword,
                    }))
                  }
                  onCapsLockChange={(enabled) =>
                    setCapsLockField(enabled ? "currentPassword" : undefined)
                  }
                  onChange={(value) => updateField("currentPassword", value)}
                />

                <PasswordField
                  id="newPassword"
                  label="新密碼"
                  autoComplete="new-password"
                  placeholder="請輸入新密碼"
                  value={values.newPassword}
                  error={fieldErrors.newPassword}
                  visible={visibleFields.newPassword}
                  capsLockOn={capsLockField === "newPassword"}
                  disabled={changePassword.isPending}
                  onToggleVisible={() =>
                    setVisibleFields((current) => ({
                      ...current,
                      newPassword: !current.newPassword,
                    }))
                  }
                  onCapsLockChange={(enabled) =>
                    setCapsLockField(enabled ? "newPassword" : undefined)
                  }
                  onChange={(value) => updateField("newPassword", value)}
                />

                <PasswordField
                  id="newPasswordConfirmation"
                  label="確認新密碼"
                  autoComplete="new-password"
                  placeholder="請再次輸入新密碼"
                  value={values.newPasswordConfirmation}
                  error={fieldErrors.newPasswordConfirmation}
                  visible={visibleFields.newPasswordConfirmation}
                  capsLockOn={capsLockField === "newPasswordConfirmation"}
                  disabled={changePassword.isPending}
                  onToggleVisible={() =>
                    setVisibleFields((current) => ({
                      ...current,
                      newPasswordConfirmation:
                        !current.newPasswordConfirmation,
                    }))
                  }
                  onCapsLockChange={(enabled) =>
                    setCapsLockField(
                      enabled ? "newPasswordConfirmation" : undefined,
                    )
                  }
                  onChange={(value) =>
                    updateField("newPasswordConfirmation", value)
                  }
                />

                <div className="flex gap-2 pt-2">
                  <Button
                    className="flex-1"
                    type="submit"
                    loading={changePassword.isPending}
                    loadingText="更新中..."
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
