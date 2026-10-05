import { ApiError, apiErrorFromResponse, toApiError } from './errors'
import type { DailySummary, ProblemDetails } from './types'

const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5241').replace(/\/+$/, '')

/** Longer than the backend's own upstream timeout, so a slow Yahoo is reported by the API first. */
export const REQUEST_TIMEOUT_MS = 20_000

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    const body: unknown = await response.json()
    return body && typeof body === 'object' ? (body as ProblemDetails) : null
  } catch {
    return null
  }
}

/**
 * Fetches the daily summary for a symbol. Throws ApiError on any failure, including a response
 * that is not an array; an abort caused by the caller's signal passes through unchanged.
 */
export async function fetchDailySummary(
  symbol: string,
  signal?: AbortSignal,
): Promise<DailySummary[]> {
  const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS)
  const combined = signal ? AbortSignal.any([signal, timeout]) : timeout

  try {
    const response = await fetch(
      `${baseUrl}/api/stocks/${encodeURIComponent(symbol)}/daily-summary`,
      { headers: { Accept: 'application/json' }, signal: combined },
    )

    if (!response.ok) {
      throw apiErrorFromResponse(response.status, await readProblem(response))
    }

    const body: unknown = await response.json()
    if (!Array.isArray(body)) {
      throw new ApiError('unexpected', { status: response.status, cause: body })
    }
    return body as DailySummary[]
  } catch (error) {
    if (signal?.aborted) throw error
    throw toApiError(error)
  }
}
