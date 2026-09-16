import { Megaphone } from 'lucide-react'

export interface SystemAnnouncementProps {
  message?: string
}

export function SystemAnnouncement({ message }: SystemAnnouncementProps) {
  const content = message?.trim()

  if (!content) {
    return null
  }

  return (
    <aside
      className="mt-6 flex items-start gap-2 border border-line bg-primary-subtle px-3 py-2.5 text-meta text-ink"
      aria-label="系統公告"
    >
      <Megaphone
        className="mt-0.5 shrink-0 text-primary"
        aria-hidden="true"
        size={16}
        strokeWidth={1.75}
      />
      <div>
        <p className="font-medium">系統公告</p>
        <p className="mt-0.5 text-ink-muted">{content}</p>
      </div>
    </aside>
  )
}
