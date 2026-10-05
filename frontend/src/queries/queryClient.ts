import { QueryCache, QueryClient } from '@tanstack/react-query'
import { toApiError } from '../api/errors'
import { notifyApiError } from '../notify'
import { dailySummaryKey } from './useDailySummary'

/** Backend caches for 60 s, so refetching sooner returns the same data. */
const STALE_TIME_MS = 60_000

export function createQueryClient(): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({
      // Runs once per failed fetch (not per observer or render), so each failure raises one toast.
      onError: (error, query) => {
        const [scope, symbol] = query.queryKey
        notifyApiError(
          toApiError(error),
          scope === dailySummaryKey('')[0] ? String(symbol) : undefined,
        )
      },
    }),
    defaultOptions: {
      queries: {
        staleTime: STALE_TIME_MS,
        refetchOnWindowFocus: false,
        // Let the request run and fail with a toast; the default would pause it silently while the browser is offline.
        networkMode: 'always',
        retry: (failureCount, error) => toApiError(error).retryable && failureCount < 1,
      },
    },
  })
}
