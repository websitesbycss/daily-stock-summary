import { QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { toast } from 'sonner'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { STORAGE_KEY } from './dashboard/storage'
import { createQueryClient } from './queries/queryClient'

interface Item {
  i: string
  x: number
  y: number
  w: number
  h: number
  minW?: number
  minH?: number
}

interface GridProps {
  breakpoint: string
  children: ReactNode
  layouts: Record<string, Item[]>
  dragConfig: { handle: string; cancel: string }
  onDragStop: (layout: Item[]) => void
  onResizeStop: (layout: Item[]) => void
}

// jsdom has no layout engine, so the grid itself (drag, resize, reflow) is exercised in the
// browser. This stand-in renders the children and records what the dashboard hands the grid, so
// tests can call back the way the real grid does. Everything else is the real app.
const grid = vi.hoisted(() => ({ width: 1000, props: undefined as GridProps | undefined }))

vi.mock('react-grid-layout', () => ({
  Responsive: (props: GridProps) => {
    grid.props = props
    return <div>{props.children}</div>
  },
  useContainerWidth: () => ({ width: grid.width, containerRef: { current: null }, mounted: true }),
}))

vi.mock('sonner', () => ({ toast: { success: vi.fn(), info: vi.fn(), error: vi.fn() } }))

const DAYS = [
  { day: '2026-01-29', lowAverage: 40.2958, highAverage: 49.7534, volume: 49073348 },
  { day: '2026-01-30', lowAverage: 41.5, highAverage: 48, volume: 1200 },
]
const MORE_DAYS = [
  ...DAYS,
  { day: '2026-02-02', lowAverage: 39, highAverage: 52.25, volume: 9000000 },
]

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const problem = (slug: string, status: number) =>
  json({ type: `urn:daily-stock-summary:problem:${slug}`, title: 'x', status }, status)

type Handler = () => Response | Promise<Response>

/** Answers GET /api/stocks/{symbol}/daily-summary per symbol; symbols without a handler succeed. */
function stubApi(handlers: Record<string, Handler> = {}) {
  const calls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) => {
      const symbol = decodeURIComponent(url.split('/api/stocks/')[1].split('/')[0])
      calls.push(symbol)
      return Promise.resolve((handlers[symbol] ?? (() => json(DAYS)))())
    }),
  )
  return calls
}

function renderApp() {
  const client = createQueryClient()
  // Same retry rules as production, without the real wait between attempts.
  client.setDefaultOptions({
    queries: { ...client.getDefaultOptions().queries, retryDelay: 0 },
  })
  return render(
    <QueryClientProvider client={client}>
      <App />
    </QueryClientProvider>,
  )
}

async function addSymbol(user: ReturnType<typeof userEvent.setup>, symbol: string) {
  await user.type(screen.getByLabelText('Stock symbol'), `${symbol}{Enter}`)
}

const panel = (symbol: string) => screen.getByRole('region', { name: symbol })
const panelNames = () => screen.getAllByRole('region').map((el) => el.getAttribute('aria-label'))
const liveRegion = () => document.querySelector('p.sr-only')!
const saved = () => JSON.parse(window.localStorage.getItem(STORAGE_KEY)!)
const slot = (layout: Item[], symbol: string) => layout.find((item) => item.i === symbol)!

beforeEach(() => {
  window.localStorage.clear()
  grid.width = 1000
  grid.props = undefined
  vi.clearAllMocks()
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('adding symbols', () => {
  it('shows an empty state before any symbol is added', () => {
    stubApi()
    renderApp()
    expect(screen.getByText(/No symbols yet/)).toBeInTheDocument()
    expect(screen.queryByRole('region')).not.toBeInTheDocument()
  })

  it('adds a panel per symbol and requests each symbol once', async () => {
    const calls = stubApi()
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'tsla')
    await addSymbol(user, 'aapl')
    await addSymbol(user, 'msft')

    expect(panelNames()).toEqual(['TSLA', 'AAPL', 'MSFT'])
    expect(await screen.findAllByText('2 days, Jan 29, 2026 to Jan 30, 2026')).toHaveLength(3)
    expect([...calls].sort()).toEqual(['AAPL', 'MSFT', 'TSLA'])
  })

  it('adds symbols with punctuation, from the form and from the example links', async () => {
    const calls = stubApi()
    const user = userEvent.setup()
    renderApp()

    await user.click(screen.getByRole('button', { name: '^GSPC' }))
    await addSymbol(user, 'btc-usd')

    expect(panelNames()).toEqual(['^GSPC', 'BTC-USD'])
    expect(await screen.findAllByText(/2 days/)).toHaveLength(2)
    expect([...calls].sort()).toEqual(['BTC-USD', '^GSPC'])
  })

  it('rejects an invalid symbol without adding a panel or sending a request', async () => {
    const calls = stubApi()
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, '!!!')

    expect(screen.queryByRole('region')).not.toBeInTheDocument()
    expect(calls).toEqual([])
    expect(toast.error).toHaveBeenCalledOnce()
  })

  it('does not add or fetch a symbol that is already on the dashboard', async () => {
    const calls = stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await screen.findByText(/2 days/)

    await addSymbol(user, 'tsla')

    expect(panelNames()).toEqual(['TSLA'])
    expect(calls).toEqual(['TSLA'])
    expect(toast.info).toHaveBeenCalledWith('TSLA is already on the dashboard.', {
      id: 'duplicate:TSLA',
    })
    expect(panel('TSLA')).toHaveFocus()
  })

  it('stops at 12 panels and says why', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    for (let n = 1; n <= 12; n++) await addSymbol(user, `S${n}`)

    await addSymbol(user, 'EXTRA')

    expect(screen.getAllByRole('region')).toHaveLength(12)
    expect(toast.info).toHaveBeenCalledWith(
      'The dashboard holds 12 symbols. Remove one to add EXTRA.',
      { id: 'full' },
    )
  })

  it('tells the user a symbol is already there, not that the dashboard is full, when both apply', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    for (let n = 1; n <= 12; n++) await addSymbol(user, `S${n}`)

    await addSymbol(user, 'S3')

    expect(toast.info).toHaveBeenCalledOnce()
    expect(toast.info).toHaveBeenCalledWith('S3 is already on the dashboard.', {
      id: 'duplicate:S3',
    })
  })
})

describe('panel views', () => {
  it('switches one panel to its table without touching the others', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')
    await screen.findAllByText(/2 days/)

    await user.click(within(panel('AAPL')).getByRole('button', { name: 'Table' }))

    expect(
      within(panel('AAPL')).getByRole('table', { name: 'AAPL daily summary' }),
    ).toBeInTheDocument()
    expect(within(panel('TSLA')).queryByRole('table')).not.toBeInTheDocument()
    expect(
      await within(panel('TSLA')).findByRole('img', { name: /TSLA daily/ }),
    ).toBeInTheDocument()
  })

  it('refreshes one symbol and shows the new data', async () => {
    let answers = 0
    const calls = stubApi({ TSLA: () => json(++answers === 1 ? DAYS : MORE_DAYS) })
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    expect(await screen.findByText(/^2 days/)).toBeInTheDocument()

    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Refresh TSLA' }))

    expect(await screen.findByText('3 days, Jan 29, 2026 to Feb 2, 2026')).toBeInTheDocument()
    expect(calls).toEqual(['TSLA', 'TSLA'])
  })
})

describe('failures', () => {
  it('shows an unknown symbol in its own panel only, and Try again recovers it', async () => {
    let healthy = false
    const calls = stubApi({
      BADCO: () => (healthy ? json(DAYS) : problem('symbol-not-found', 404)),
    })
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'BADCO')

    const failing = panel('BADCO')
    expect(await within(failing).findByRole('alert')).toHaveTextContent(
      'No data found for BADCO. Check the symbol and try again.',
    )
    expect(await within(panel('TSLA')).findByText(/2 days/)).toBeInTheDocument()
    expect(within(panel('TSLA')).queryByRole('alert')).not.toBeInTheDocument()
    expect(calls.filter((s) => s === 'BADCO')).toHaveLength(1) // an unknown symbol is not retried
    expect(toast.error).toHaveBeenCalledOnce()
    expect(toast.error).toHaveBeenCalledWith(
      'No data found for BADCO. Check the symbol and try again.',
      { id: 'error:BADCO' },
    )

    healthy = true
    await user.click(within(failing).getByRole('button', { name: 'Try again' }))

    expect(await within(failing).findByText(/2 days/)).toBeInTheDocument()
    expect(within(failing).queryByRole('alert')).not.toBeInTheDocument()
  })

  it('retries a gateway error once and recovers without bothering the user', async () => {
    let attempt = 0
    const calls = stubApi({
      TSLA: () => (++attempt === 1 ? problem('upstream-unavailable', 502) : json(DAYS)),
    })
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'TSLA')

    expect(await screen.findByText(/2 days/)).toBeInTheDocument()
    expect(calls).toEqual(['TSLA', 'TSLA'])
    expect(toast.error).not.toHaveBeenCalled()
  })

  it('reports an unreachable server after one retry', async () => {
    const calls = stubApi({
      TSLA: () => Promise.reject(new TypeError('Failed to fetch')) as never,
    })
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'TSLA')

    expect(await within(panel('TSLA')).findByRole('alert')).toHaveTextContent(
      'Cannot reach the server. Check that the API is running.',
    )
    expect(calls).toEqual(['TSLA', 'TSLA'])
    expect(toast.error).toHaveBeenCalledOnce()
  })

  it('reports a request that timed out', async () => {
    stubApi({
      TSLA: () => Promise.reject(new DOMException('timed out', 'TimeoutError')) as never,
    })
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'TSLA')

    expect(await within(panel('TSLA')).findByRole('alert')).toHaveTextContent(
      'The server took too long to respond. Try again.',
    )
  })

  it('shows the data already loaded when a refresh fails', async () => {
    let answers = 0
    stubApi({
      TSLA: () => (++answers === 1 ? json(DAYS) : problem('upstream-unavailable', 502)),
    })
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await screen.findByText(/2 days/)

    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Refresh TSLA' }))

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledOnce())
    // The refresh has finished (the button is usable again) before the panel is checked.
    await vi.waitFor(() =>
      expect(within(panel('TSLA')).getByRole('button', { name: 'Refresh TSLA' })).toBeEnabled(),
    )
    expect(toast.error).toHaveBeenCalledWith(
      'Yahoo Finance is not responding. Try again in a moment.',
      { id: 'error:TSLA' },
    )
    expect(within(panel('TSLA')).getByText(/2 days/)).toBeInTheDocument()
    expect(within(panel('TSLA')).queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows the loading state while Try again is in flight', async () => {
    let answers = 0
    stubApi({
      BADCO: () => (++answers === 1 ? problem('symbol-not-found', 404) : new Promise(() => {})),
    })
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'BADCO')
    await user.click(await within(panel('BADCO')).findByRole('button', { name: 'Try again' }))

    expect(within(panel('BADCO')).getByRole('status', { name: 'Loading data' })).toBeInTheDocument()
    expect(within(panel('BADCO')).queryByRole('alert')).not.toBeInTheDocument()
  })

  it('explains an empty response instead of showing a blank panel', async () => {
    stubApi({ THIN: () => json([]) })
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'THIN')

    expect(
      await screen.findByText('THIN has no trading data for the past month.'),
    ).toBeInTheDocument()
  })

  it('reports a response that is not a list of days', async () => {
    stubApi({ ODD: () => json({ unexpected: true }) })
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'ODD')

    expect(await within(panel('ODD')).findByRole('alert')).toHaveTextContent(
      'Something went wrong. Try again.',
    )
  })
})

describe('removing and reordering panels', () => {
  it('removes a panel, announces it, and returns focus to the symbol field', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Remove TSLA' }))

    expect(panelNames()).toEqual(['AAPL'])
    expect(liveRegion()).toHaveTextContent('TSLA removed.')
    expect(screen.getByLabelText('Stock symbol')).toHaveFocus()
  })

  it('moves a panel later with the keyboard buttons and keeps focus on the button', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    const earlier = within(panel('TSLA')).getByRole('button', { name: 'Move TSLA earlier' })
    const later = within(panel('TSLA')).getByRole('button', { name: 'Move TSLA later' })
    expect(earlier).toHaveAttribute('aria-disabled', 'true')
    expect(later).toHaveAttribute('aria-disabled', 'false')

    later.focus()
    await user.keyboard('{Enter}')

    expect(liveRegion()).toHaveTextContent('Moved TSLA to position 2 of 2.')
    expect(later).toHaveAttribute('aria-disabled', 'true')
    expect(earlier).toHaveAttribute('aria-disabled', 'false')
    expect(later).toHaveFocus()
    // Tab order follows what is on screen.
    expect(panelNames()).toEqual(['AAPL', 'TSLA'])
  })

  it('ignores a click on a move button that is disabled', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Move TSLA earlier' }))
    await user.click(within(panel('AAPL')).getByRole('button', { name: 'Move AAPL later' }))

    expect(liveRegion()).not.toHaveTextContent(/Moved/)
    expect(panelNames()).toEqual(['TSLA', 'AAPL'])
  })

  it('moves panels in the layout the user sees on a narrow screen', async () => {
    grid.width = 500
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Move TSLA later' }))

    const layouts = saved().layouts
    expect(slot(layouts.sm, 'TSLA')).toMatchObject({ x: 0, y: 13 })
    expect(slot(layouts.sm, 'AAPL')).toMatchObject({ x: 0, y: 0 })
    expect(slot(layouts.lg, 'TSLA')).toMatchObject({ x: 0, y: 0 }) // desktop layout untouched
    expect(panelNames()).toEqual(['AAPL', 'TSLA'])
  })
})

describe('what the dashboard gives the grid', () => {
  it.each([
    [1600, 'xl'],
    [1000, 'lg'],
    [700, 'md'],
    [500, 'sm'],
  ])('tells the grid which layout to use at %i px (%s)', async (width, breakpoint) => {
    grid.width = width
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')

    expect(grid.props!.breakpoint).toBe(breakpoint)
  })

  it('saves a drag at one breakpoint under that breakpoint only', async () => {
    grid.width = 500
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    act(() =>
      grid.props!.onDragStop([
        { i: 'AAPL', x: 0, y: 0, w: 1, h: 13 },
        { i: 'TSLA', x: 0, y: 13, w: 1, h: 13 },
      ]),
    )

    expect(slot(saved().layouts.sm, 'AAPL')).toMatchObject({ x: 0, y: 0 })
    expect(slot(saved().layouts.lg, 'AAPL')).toMatchObject({ x: 6, y: 0, w: 6 }) // desktop layout kept
  })

  it('limits how small a panel can be resized, per breakpoint', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')

    const { layouts } = grid.props!
    expect(slot(layouts.lg, 'TSLA')).toMatchObject({ minW: 3, minH: 9 })
    expect(slot(layouts.sm, 'TSLA')).toMatchObject({ minW: 1, minH: 9 }) // one column on a phone
  })

  it('does not change layout when the width moves a little across a breakpoint edge', async () => {
    // A scrollbar appearing or disappearing moves the width by about 15 px; that must not flip the layout.
    grid.width = 1510
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')
    expect(grid.props!.breakpoint).toBe('xl')

    grid.width = 1495
    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Table' }))
    expect(grid.props!.breakpoint).toBe('xl')

    grid.width = 1510
    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Chart' }))
    expect(grid.props!.breakpoint).toBe('xl')

    grid.width = 1450 // a real change
    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Table' }))
    expect(grid.props!.breakpoint).toBe('lg')

    grid.width = 1510 // and back up only past the dead band
    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Chart' }))
    expect(grid.props!.breakpoint).toBe('lg')
    grid.width = 1530
    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Table' }))
    expect(grid.props!.breakpoint).toBe('xl')
  })

  it('lets the title bar drag a panel but not its buttons', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')

    const { handle, cancel } = grid.props!.dragConfig
    expect(panel('TSLA').querySelector(handle)).not.toBeNull()
    for (const name of ['Table', 'Refresh TSLA', 'Move TSLA later', 'Remove TSLA']) {
      expect(within(panel('TSLA')).getByRole('button', { name }).closest(cancel)).not.toBeNull()
    }
    expect(panel('TSLA').querySelector('.panel__heading')?.closest(cancel)).toBeNull()
  })

  it('keeps a drag through a reload', async () => {
    stubApi()
    const user = userEvent.setup()
    const first = renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    act(() =>
      grid.props!.onDragStop([
        { i: 'TSLA', x: 6, y: 0, w: 6, h: 13 },
        { i: 'AAPL', x: 0, y: 0, w: 12, h: 20 },
      ]),
    )

    expect(panelNames()).toEqual(['AAPL', 'TSLA'])
    expect(slot(saved().layouts.lg, 'AAPL')).toEqual({ i: 'AAPL', x: 0, y: 0, w: 12, h: 20 })

    first.unmount()
    renderApp()

    expect(panelNames()).toEqual(['AAPL', 'TSLA'])
    expect(slot(grid.props!.layouts.lg, 'AAPL')).toMatchObject({ w: 12, h: 20 })
  })
})

describe('persistence', () => {
  it('restores symbols and views after a reload', async () => {
    stubApi()
    const user = userEvent.setup()
    const first = renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')
    await screen.findAllByText(/2 days/)
    await user.click(within(panel('AAPL')).getByRole('button', { name: 'Table' }))
    first.unmount()

    renderApp()

    expect(panelNames()).toEqual(['TSLA', 'AAPL'])
    expect(await within(panel('AAPL')).findByRole('table')).toBeInTheDocument()
    expect(within(panel('TSLA')).queryByRole('table')).not.toBeInTheDocument()
  })

  it('does not bring back a panel that was removed', async () => {
    stubApi()
    const user = userEvent.setup()
    const first = renderApp()
    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')
    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Remove TSLA' }))
    first.unmount()

    renderApp()

    expect(panelNames()).toEqual(['AAPL'])
  })

  it('starts fresh and says so when the saved dashboard is corrupt', () => {
    window.localStorage.setItem(STORAGE_KEY, '{oops')
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    stubApi()
    renderApp()

    expect(screen.getByText(/No symbols yet/)).toBeInTheDocument()
    expect(toast.info).toHaveBeenCalledWith(
      'Your saved dashboard could not be restored, so you are starting fresh.',
      { id: 'restore' },
    )
    expect(window.localStorage.getItem(`${STORAGE_KEY}.bak`)).toBe('{oops')
  })

  it('keeps working, and says so once, when the layout cannot be saved', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError')
    })
    vi.spyOn(console, 'warn').mockImplementation(() => {})
    stubApi()
    const user = userEvent.setup()
    renderApp()

    await addSymbol(user, 'TSLA')
    await addSymbol(user, 'AAPL')

    expect(panelNames()).toEqual(['TSLA', 'AAPL'])
    expect(toast.error).toHaveBeenCalledOnce()
    expect(toast.error).toHaveBeenCalledWith('Your layout could not be saved in this browser.', {
      id: 'save',
    })
  })
})

describe('resizing', () => {
  it('keeps a resized panel through a reload', async () => {
    stubApi()
    const user = userEvent.setup()
    const first = renderApp()
    await addSymbol(user, 'TSLA')

    act(() => grid.props!.onResizeStop([{ i: 'TSLA', x: 0, y: 0, w: 12, h: 20 }]))
    first.unmount()
    renderApp()

    expect(slot(grid.props!.layouts.lg, 'TSLA')).toMatchObject({ w: 12, h: 20 })
  })
})

describe('header modes', () => {
  it('opens as a hero: one heading, the intro, the examples and a faint preview of a panel', () => {
    stubApi()
    const { container } = renderApp()

    expect(
      screen.getByRole('heading', { level: 1, name: 'Daily Stock Summary' }),
    ).toBeInTheDocument()
    expect(screen.getAllByText('Daily Stock Summary')).toHaveLength(1) // no separate wordmark above it
    expect(screen.queryByText('Look up stocks')).not.toBeInTheDocument()
    expect(screen.getByText(/Daily low and high averages/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'TSLA' })).toBeInTheDocument()

    const ghost = container.querySelector('.ghost')
    expect(ghost).toHaveAttribute('aria-hidden', 'true')
    expect(ghost?.querySelector('button, a, input, [tabindex]')).toBeNull()
  })

  it('collapses to a slim bar once a symbol is added, keeping the heading and the field', async () => {
    stubApi()
    const user = userEvent.setup()
    const { container } = renderApp()

    await addSymbol(user, 'TSLA')

    expect(
      screen.getByRole('heading', { level: 1, name: 'Daily Stock Summary' }),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Daily low and high averages/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'AAPL' })).not.toBeInTheDocument() // the example links
    expect(screen.queryByText(/No symbols yet/)).not.toBeInTheDocument()
    expect(container.querySelector('.ghost')).toBeNull()

    // The same field keeps working from the bar.
    await addSymbol(user, 'aapl')
    expect(panelNames()).toEqual(['TSLA', 'AAPL'])
  })

  it('returns to the hero when the last symbol is removed', async () => {
    stubApi()
    const user = userEvent.setup()
    const { container } = renderApp()
    await addSymbol(user, 'TSLA')

    await user.click(within(panel('TSLA')).getByRole('button', { name: 'Remove TSLA' }))

    expect(screen.getByText(/Daily low and high averages/)).toBeInTheDocument()
    expect(container.querySelector('.ghost')).not.toBeNull()
  })
})

describe('theme', () => {
  const choice = (name: string) => screen.getByRole('button', { name })

  it('follows the system until a choice is made', () => {
    stubApi()
    renderApp()

    expect(choice('Match system theme')).toHaveAttribute('aria-pressed', 'true')
    expect(document.documentElement).not.toHaveAttribute('data-theme')
  })

  it('applies and remembers an explicit choice', async () => {
    stubApi()
    const user = userEvent.setup()
    const first = renderApp()

    await user.click(choice('Dark theme'))

    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
    expect(choice('Dark theme')).toHaveAttribute('aria-pressed', 'true')
    expect(choice('Light theme')).toHaveAttribute('aria-pressed', 'false')
    expect(window.localStorage.getItem('dss.theme')).toBe('dark')

    first.unmount()
    document.documentElement.removeAttribute('data-theme')
    renderApp()

    expect(choice('Dark theme')).toHaveAttribute('aria-pressed', 'true')
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark')
  })

  it('goes back to following the system', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await user.click(choice('Light theme'))

    await user.click(choice('Match system theme'))

    expect(document.documentElement).not.toHaveAttribute('data-theme')
    expect(window.localStorage.getItem('dss.theme')).toBe('system')
  })

  it('is available from the slim bar as well', async () => {
    stubApi()
    const user = userEvent.setup()
    renderApp()
    await addSymbol(user, 'TSLA')

    await user.click(choice('Light theme'))

    expect(document.documentElement).toHaveAttribute('data-theme', 'light')
  })
})
