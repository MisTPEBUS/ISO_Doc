import { ShieldCheck } from 'lucide-react'

export function SecurityNotice() {
  return (
    <div className="flex items-start gap-2 text-fine leading-5 text-ink-muted">
      <ShieldCheck
        className="mt-0.5 shrink-0 text-primary"
        aria-hidden="true"
        size={15}
        strokeWidth={1.75}
      />
      <p>
        <span className="block font-medium text-ink">公司內部資訊系統</span>
        系統操作、文件異動與下載紀錄將依公司資訊安全規範留存。
      </p>
    </div>
  )
}
