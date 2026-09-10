import type { ProblemDetails } from '@/shared/types/problem'

export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail?: string
  readonly errors?: Record<string, string[]>

  constructor(problem: ProblemDetails, errors?: Record<string, string[]>) {
    super(problem.detail ?? problem.title)
    this.name = 'ApiError'
    this.status = problem.status
    this.title = problem.title
    this.detail = problem.detail
    this.errors = errors
  }

  get isValidation(): boolean {
    return this.status === 400 && this.errors !== undefined
  }

  fieldErrors(): Record<string, string[]> {
    return this.errors ?? {}
  }
}
