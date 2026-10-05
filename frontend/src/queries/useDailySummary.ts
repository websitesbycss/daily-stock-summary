import { useQuery } from '@tanstack/react-query'
import { fetchDailySummary } from '../api/client'
import type { ApiError } from '../api/errors'
import type { DailySummary } from '../api/types'

export const dailySummaryKey = (symbol: string) => ['daily-summary', symbol] as const

/** One query per (already normalized) symbol. */
export function useDailySummary(symbol: string) {
  return useQuery<DailySummary[], ApiError>({
    queryKey: dailySummaryKey(symbol),
    queryFn: ({ signal }) => fetchDailySummary(symbol, signal),
  })
}
