/** One trading day, as returned by GET /api/stocks/{symbol}/daily-summary (oldest day first). */
export interface DailySummary {
  /** Exchange-local date, yyyy-MM-dd. Never convert time zones on the client. */
  day: string
  lowAverage: number
  highAverage: number
  volume: number
}

/** RFC 7807 body the backend returns on failure. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
}
