import { env, type AppEnvironment } from '@/config/env'

const ENVIRONMENT_LABELS: Record<AppEnvironment, string> = {
  development: 'Development',
  uat: 'UAT',
  production: 'Production',
}

export function SystemVersion() {
  const environmentLabel = ENVIRONMENT_LABELS[env.appEnvironment]

  return (
    <div className="text-right text-fine leading-5 text-ink-muted">
      <p>ISO Document Control System</p>
      <p className="tabular">
        Version {env.appVersion} · {environmentLabel}
      </p>
    </div>
  )
}
