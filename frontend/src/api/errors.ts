import type { ProblemDetails } from './types'

export type ApiErrorKind =
  | 'invalid-symbol'
  | 'not-found'
  | 'rate-limited'
  | 'upstream-unavailable'
  | 'upstream-timeout'
  /** This app gave up waiting for the API. */
  | 'timeout'
  | 'network'
  | 'unexpected'

const PROBLEM_TYPE_PREFIX = 'urn:daily-stock-summary:problem:'

interface ApiErrorDetails {
  status?: number
  /** Backend correlation id from ProblemDetails, for matching a failure to server logs. */
  traceId?: string
  /** The underlying error, kept for the console. */
  cause?: unknown
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly status?: number
  readonly traceId?: string

  constructor(kind: ApiErrorKind, details: ApiErrorDetails = {}) {
    super(`API error: ${kind}`, { cause: details.cause })
    this.name = 'ApiError'
    this.kind = kind
    this.status = details.status
    this.traceId = details.traceId
  }

  /** Retrying can help only when the failure is transient. */
  get retryable(): boolean {
    return (
      this.kind === 'network' ||
      this.kind === 'timeout' ||
      this.kind === 'upstream-unavailable' ||
      this.kind === 'upstream-timeout'
    )
  }
}

const KINDS_BY_SLUG: Record<string, ApiErrorKind> = {
  'invalid-symbol': 'invalid-symbol',
  'symbol-not-found': 'not-found',
  'rate-limited': 'rate-limited',
  'upstream-unavailable': 'upstream-unavailable',
  'upstream-timeout': 'upstream-timeout',
  'unexpected-error': 'unexpected',
}

function kindFromStatus(status: number): ApiErrorKind {
  switch (status) {
    case 400:
      return 'invalid-symbol'
    case 404:
      return 'not-found'
    case 429:
      return 'rate-limited'
    case 502:
    case 503:
      return 'upstream-unavailable'
    case 408:
    case 504:
      return 'upstream-timeout'
    default:
      return 'unexpected'
  }
}

/**
 * Maps a failed HTTP response to an ApiError. The ProblemDetails `type` is preferred; the status
 * code is the fallback for bodies that are not ProblemDetails (a proxy's HTML error page).
 */
export function apiErrorFromResponse(status: number, problem: ProblemDetails | null): ApiError {
  const details = { status, traceId: problem?.traceId }
  const type = problem?.type
  if (type?.startsWith(PROBLEM_TYPE_PREFIX)) {
    const kind = KINDS_BY_SLUG[type.slice(PROBLEM_TYPE_PREFIX.length)]
    if (kind) return new ApiError(kind, details)
  }
  return new ApiError(kindFromStatus(status), details)
}

/** Anything thrown that is not already an ApiError (fetch rejects with a TypeError offline). */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  if (error instanceof DOMException && error.name === 'TimeoutError') {
    return new ApiError('timeout', { cause: error })
  }
  if (error instanceof TypeError) return new ApiError('network', { cause: error })
  return new ApiError('unexpected', { cause: error })
}
