export type AppEnvironment = 'development' | 'uat' | 'production'

function parseAppEnvironment(value: string | undefined): AppEnvironment {
  switch (value?.trim().toLowerCase()) {
    case 'dev':
    case 'development':
      return 'development'
    case 'uat':
      return 'uat'
    case 'prod':
    case 'production':
      return 'production'
    default:
      return 'production'
  }
}

export const env = Object.freeze({
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL?.trim() || '/api',
  appVersion: import.meta.env.VITE_APP_VERSION?.trim() || '1.0.0',
  appEnvironment: parseAppEnvironment(import.meta.env.VITE_APP_ENVIRONMENT),
})
