// 顯示用日期格式：一律轉為民國年（例如 115/09/30）。
// API 傳輸格式不變：日期為 `YYYY-MM-DD`，時間為 UTC ISO 字串。

const EMPTY_DATE = "－";
const ROC_YEAR_OFFSET = 1911;
const ISO_DATE_PATTERN = /^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})$/;

const rocDateTimeFormatter = new Intl.DateTimeFormat("zh-TW-u-ca-roc", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: "Asia/Taipei",
});

/** `2026-09-30` → `115/09/30`。純日期不經 `Date`，避免時區位移。 */
export function formatRocDate(
  value: string | null | undefined,
  empty: string = EMPTY_DATE,
): string {
  if (!value) return empty;

  const groups = ISO_DATE_PATTERN.exec(value)?.groups;
  if (groups === undefined) return value;

  return `${Number(groups.year) - ROC_YEAR_OFFSET}/${groups.month}/${groups.day}`;
}

/** UTC ISO 時間 → `Asia/Taipei` 的 `115/09/30 14:05`。 */
export function formatRocDateTime(
  value: string | null | undefined,
  empty: string = EMPTY_DATE,
): string {
  if (!value) return empty;

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  const parts = rocDateTimeFormatter.formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";

  return `${part("year")}/${part("month")}/${part("day")} ${part("hour")}:${part("minute")}`;
}
