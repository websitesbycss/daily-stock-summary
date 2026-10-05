import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchDailySummary } from './client'
import { ApiError } from './errors'

const problem = (slug: string, status: number, traceId?: string) =>
  new Response(
    JSON.stringify({
      type: `urn:daily-stock-summary:problem:${slug}`,
      title: 'ignored',
      status,
      traceId,
    }),
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )

function stubFetch(response: Response | Error | DOMException) {
  const fetchMock = vi.fn(() =>
    response instanceof Response ? Promise.resolve(response) : Promise.reject(response),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function failureOf(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise
  } catch (error) {
    expect(error).toBeInstanceOf(ApiError)
    return error as ApiError
  }
  throw new Error('expected the request to fail')
}

afterEach(() => vi.unstubAllGlobals())

describe('fetchDailySummary', () => {
  it('returns the days in the order the API sent them', async () => {
    const body = [
      { day: '2026-01-29', lowAverage: 40.2958, highAverage: 49.7534, volume: 49073348 },
      { day: '2026-01-30', lowAverage: 41.1, highAverage: 50.2, volume: 1200 },
    ]
    stubFetch(new Response(JSON.stringify(body), { status: 200 }))
    await expect(fetchDailySummary('TSLA')).resolves.toEqual(body)
  })

  it('url-encodes symbols such as ^GSPC', async () => {
    const fetchMock = stubFetch(new Response('[]', { status: 200 }))
    await fetchDailySummary('^GSPC')
    const url = (fetchMock.mock.calls[0] as unknown as [string])[0]
    expect(url).toBe('http://localhost:5241/api/stocks/%5EGSPC/daily-summary')
  })
})

describe('error mapping', () => {
  it.each([
    ['invalid-symbol', 400, 'invalid-symbol'],
    ['symbol-not-found', 404, 'not-found'],
    ['rate-limited', 429, 'rate-limited'],
    ['upstream-unavailable', 502, 'upstream-unavailable'],
    ['upstream-timeout', 504, 'upstream-timeout'],
    ['unexpected-error', 500, 'unexpected'],
  ])('maps the %s problem (%i) to kind %s', async (slug, status, kind) => {
    stubFetch(problem(slug, status))
    const error = await failureOf(fetchDailySummary('TSLA'))
    expect(error.kind).toBe(kind)
    expect(error.status).toBe(status)
  })

  it('trusts the problem type over the status code when they disagree', async () => {
    stubFetch(problem('rate-limited', 500))
    expect((await failureOf(fetchDailySummary('TSLA'))).kind).toBe('rate-limited')
    stubFetch(problem('symbol-not-found', 400))
    expect((await failureOf(fetchDailySummary('TSLA'))).kind).toBe('not-found')
  })

  it('falls back to the status code for an unknown problem type', async () => {
    stubFetch(problem('something-new', 404))
    expect((await failureOf(fetchDailySummary('TSLA'))).kind).toBe('not-found')
    stubFetch(new Response(JSON.stringify({ type: 'urn:other:thing' }), { status: 429 }))
    expect((await failureOf(fetchDailySummary('TSLA'))).kind).toBe('rate-limited')
  })

  it.each([
    [502, 'upstream-unavailable'],
    [503, 'upstream-unavailable'],
    [408, 'upstream-timeout'],
    [504, 'upstream-timeout'],
    [418, 'unexpected'],
  ])('maps an HTML error page from a proxy (%i) to %s', async (status, kind) => {
    stubFetch(new Response('<html>Gateway</html>', { status }))
    expect((await failureOf(fetchDailySummary('TSLA'))).kind).toBe(kind)
  })

  it('keeps the backend trace id so a failure can be matched to server logs', async () => {
    stubFetch(problem('upstream-unavailable', 502, '00-abc-def-00'))
    expect((await failureOf(fetchDailySummary('TSLA'))).traceId).toBe('00-abc-def-00')
  })

  it('reports a network failure when the server is unreachable', async () => {
    const offline = new TypeError('Failed to fetch')
    stubFetch(offline)
    const error = await failureOf(fetchDailySummary('TSLA'))
    expect(error.kind).toBe('network')
    expect(error.retryable).toBe(true)
    expect(error.cause).toBe(offline)
  })

  it('reports a timeout when the API does not answer in time', async () => {
    stubFetch(new DOMException('The operation timed out.', 'TimeoutError'))
    const error = await failureOf(fetchDailySummary('TSLA'))
    expect(error.kind).toBe('timeout')
    expect(error.retryable).toBe(true)
  })

  it('does not retry unknown symbols, invalid symbols, or rate limits', async () => {
    for (const [slug, status] of [
      ['symbol-not-found', 404],
      ['invalid-symbol', 400],
      ['rate-limited', 429],
    ] as const) {
      stubFetch(problem(slug, status))
      expect((await failureOf(fetchDailySummary('TSLA'))).retryable).toBe(false)
    }
  })

  it.each([
    ['null', 'null'],
    ['an object', '{"days":[]}'],
    ['text that is not JSON', '<html>Welcome</html>'],
  ])('reports an unexpected response when a 200 body is %s', async (_label, body) => {
    stubFetch(new Response(body, { status: 200 }))
    const error = await failureOf(fetchDailySummary('TSLA'))
    expect(error.kind).toBe('unexpected')
    expect(error.retryable).toBe(false)
  })

  it('lets an aborted request pass through instead of reporting a failure', async () => {
    const controller = new AbortController()
    controller.abort()
    stubFetch(new DOMException('Aborted', 'AbortError'))
    await expect(fetchDailySummary('TSLA', controller.signal)).rejects.toMatchObject({
      name: 'AbortError',
    })
  })
})
