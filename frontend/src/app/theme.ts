export const APP_THEME = {
  Dark: "dark",
  Light: "light",
} as const;

export type AppTheme = (typeof APP_THEME)[keyof typeof APP_THEME];

const THEME_STORAGE_KEY = "isodocs.theme";

function isAppTheme(value: string | null | undefined): value is AppTheme {
  return value === APP_THEME.Dark || value === APP_THEME.Light;
}

function readStoredTheme(): AppTheme {
  try {
    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isAppTheme(storedTheme) ? storedTheme : APP_THEME.Dark;
  } catch {
    return APP_THEME.Dark;
  }
}

export function getAppliedTheme(): AppTheme {
  const appliedTheme = document.documentElement.dataset.theme;
  return isAppTheme(appliedTheme) ? appliedTheme : APP_THEME.Dark;
}

export function applyTheme(theme: AppTheme): void {
  document.documentElement.dataset.theme = theme;

  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // localStorage 可能被瀏覽器政策停用；主題仍套用於目前頁面。
  }
}

export function initializeTheme(): void {
  document.documentElement.dataset.theme = readStoredTheme();
}
