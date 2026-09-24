import { Badge } from '@/components/common'
import { RELEASE_NOTES, SYSTEM_NAME } from '@/features/about/releaseNotes'

const CHINESE_DIGITS = ['', '一', '二', '三', '四', '五', '六', '七', '八', '九']

// 版本說明沿用「一、二、…十一、」的條列編號。
function toChineseNumeral(value: number): string {
  const digit = (index: number) => CHINESE_DIGITS[index] ?? ''
  if (value < 10) return digit(value)

  const tens = Math.floor(value / 10)
  return `${tens === 1 ? '' : digit(tens)}十${digit(value % 10)}`
}

export function AboutPage() {
  const currentVersion = RELEASE_NOTES[0]?.version

  return (
    <section>
      <div className="mb-4">
        <p className="mb-1 text-label font-medium text-primary">系統</p>
        <h1 className="text-page-title text-ink">版本資訊</h1>
        <p className="mt-1 text-meta text-ink-muted">
          {SYSTEM_NAME}各版本的更新內容
          {currentVersion && `，目前版本為 v${currentVersion}`}。
        </p>
      </div>

      <div className="max-w-4xl space-y-4">
        {RELEASE_NOTES.map((note, index) => (
          <article key={note.version} className="border border-line-strong bg-surface">
            <header className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
              <h2 className="text-section-label text-ink">
                {SYSTEM_NAME} v{note.version}
              </h2>
              {index === 0 && <Badge variant="info">目前版本</Badge>}
            </header>

            <div className="p-4">
              <ol className="space-y-2">
                {note.items.map((item, itemIndex) => (
                  <li key={item.text} className="flex gap-1 text-cell text-ink">
                    <span className="shrink-0">{toChineseNumeral(itemIndex + 1)}、</span>
                    <div className="min-w-0">
                      <p>{item.text}</p>
                      {item.detail && (
                        <p className="mt-1 text-meta text-ink-muted">{item.detail}</p>
                      )}
                      {item.preformatted && (
                        <pre className="mt-2 overflow-x-auto border border-line bg-surface-zebra p-3 font-mono text-meta text-ink">
                          {item.preformatted}
                        </pre>
                      )}
                    </div>
                  </li>
                ))}
              </ol>

              {note.notes?.map((text) => (
                <p key={text} className="mt-3 text-meta text-ink-muted">
                  備註：{text}
                </p>
              ))}
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}

export default AboutPage
