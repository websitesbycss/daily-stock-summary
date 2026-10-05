import { toast } from 'sonner'
import type { ApiError } from './api/errors'

/** Plain-language message for a failed request; shared by toasts and panel error states. */
export function describeApiError(error: ApiError, symbol?: string): string {
  switch (error.kind) {
    case 'invalid-symbol':
      return 'Enter a valid symbol: letters, numbers, and . - ^ = (up to 15 characters).'
    case 'not-found':
      return symbol
        ? `No data found for ${symbol}. Check the symbol and try again.`
        : 'No data found for that symbol.'
    case 'rate-limited':
      return 'Too many requests. Wait a minute, then try again.'
    case 'upstream-unavailable':
      return 'Yahoo Finance is not responding. Try again in a moment.'
    case 'upstream-timeout':
      return 'Yahoo Finance took too long to respond. Try again.'
    case 'timeout':
      return 'The server took too long to respond. Try again.'
    case 'network':
      return 'Cannot reach the server. Check that the API is running.'
    case 'unexpected':
      return 'Something went wrong. Try again.'
  }
}

/** The only place user-facing notices are raised. `id` replaces an existing toast instead of stacking. */
export const notify = {
  success: (message: string, id?: string) => toast.success(message, { id }),
  info: (message: string, id?: string) => toast.info(message, { id }),
  error: (message: string, id?: string) => toast.error(message, { id }),
}

export function notifyApiError(error: ApiError, symbol?: string): void {
  notify.error(describeApiError(error, symbol), symbol ? `error:${symbol}` : undefined)
}
