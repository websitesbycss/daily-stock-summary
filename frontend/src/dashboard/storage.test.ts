import { describe, expect, it } from 'vitest'
import { dashboardReducer, emptyDashboard } from './dashboardReducer'
import { loadDashboard, parseDashboard, readDashboard, saveDashboard, STORAGE_KEY } from './storage'

function memoryStorage(initial?: string): Storage {
  const map = new Map<string, string>()
  if (initial !== undefined) map.set(STORAGE_KEY, initial)
  return {
    getItem: (key) => map.get(key) ?? null,
    setItem: (key, value) => void map.set(key, value),
  } as Storage
}

const throwingStorage = {
  getItem: () => {
    throw new DOMException('blocked', 'SecurityError')
  },
  setItem: () => {
    throw new DOMException('full', 'QuotaExceededError')
  },
} as unknown as Storage

describe('dashboard persistence', () => {
  it('restores symbols, views, positions and sizes after a reload', () => {
    let state = ['TSLA', 'AAPL', 'MSFT'].reduce(
      (s, symbol) => dashboardReducer(s, { type: 'add', symbol }),
      emptyDashboard,
    )
    state = dashboardReducer(state, { type: 'setView', symbol: 'AAPL', view: 'table' })
    state = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: [
        { i: 'TSLA', x: 0, y: 0, w: 12, h: 20 },
        { i: 'AAPL', x: 0, y: 20, w: 6, h: 13 },
        { i: 'MSFT', x: 6, y: 20, w: 6, h: 13 },
      ],
    })

    const storage = memoryStorage()
    saveDashboard(state, storage)

    expect(loadDashboard(storage)).toEqual(state)
  })

  it('starts empty when nothing was saved', () => {
    expect(loadDashboard(memoryStorage())).toEqual(emptyDashboard)
  })

  it('starts empty when the saved text is not JSON', () => {
    expect(loadDashboard(memoryStorage('{not json'))).toEqual(emptyDashboard)
  })

  it('starts empty for a different storage version', () => {
    const text = JSON.stringify({ version: 99, panels: [{ symbol: 'TSLA', view: 'chart' }] })
    expect(loadDashboard(memoryStorage(text))).toEqual(emptyDashboard)
  })

  it('survives storage that throws on read and write (blocked site data, full quota)', () => {
    expect(loadDashboard(throwingStorage)).toEqual(emptyDashboard)
    expect(() => saveDashboard(emptyDashboard, throwingStorage)).not.toThrow()
  })
})

describe('parseDashboard with edited or stale data', () => {
  it('drops invalid symbols, repeats, and unknown views', () => {
    const state = parseDashboard({
      version: 1,
      panels: [
        { symbol: 'tsla', view: 'table' },
        { symbol: 'TSLA', view: 'chart' },
        { symbol: '!!!', view: 'chart' },
        { symbol: 'BTC-USD', view: 'candles' },
        { symbol: 42 },
        'AAPL',
      ],
      layouts: {},
    })
    expect(state.panels).toEqual([
      { symbol: 'TSLA', view: 'table' },
      { symbol: 'BTC-USD', view: 'chart' },
    ])
  })

  it('replaces unusable layout items and gives panels without one a free slot', () => {
    const state = parseDashboard({
      version: 1,
      panels: [
        { symbol: 'TSLA', view: 'chart' },
        { symbol: 'AAPL', view: 'chart' },
      ],
      layouts: {
        lg: [
          { i: 'TSLA', x: 0, y: 0, w: 6, h: 13 },
          { i: 'AAPL', x: -3, y: 0.5, w: 6, h: 13 },
          { i: 'GONE', x: 6, y: 0, w: 6, h: 13 },
        ],
      },
    })
    expect(state.layouts.lg).toEqual([
      { i: 'TSLA', x: 0, y: 0, w: 6, h: 13 },
      { i: 'AAPL', x: 6, y: 0, w: 6, h: 13 },
    ])
    expect(state.layouts.sm!.map((i) => i.i)).toEqual(['TSLA', 'AAPL'])
  })

  it('moves a panel that overflows the right edge back inside the grid', () => {
    const state = parseDashboard({
      version: 1,
      panels: [{ symbol: 'TSLA', view: 'chart' }],
      layouts: { md: [{ i: 'TSLA', x: 6, y: 0, w: 6, h: 13 }] },
    })
    expect(state.layouts.md).toEqual([{ i: 'TSLA', x: 2, y: 0, w: 6, h: 13 }])
  })

  it('keeps at most 12 panels', () => {
    const panels = Array.from({ length: 20 }, (_, n) => ({ symbol: `S${n}`, view: 'chart' }))
    expect(parseDashboard({ version: 1, panels, layouts: {} }).panels).toHaveLength(12)
  })
})

describe('stored layout items with one bad field', () => {
  // The panel then gets the default slot instead of a position the grid cannot use.
  it.each([
    ['negative x', { i: 'TSLA', x: -3, y: 0, w: 6, h: 13 }],
    ['fractional y', { i: 'TSLA', x: 0, y: 0.5, w: 6, h: 13 }],
    ['zero width', { i: 'TSLA', x: 0, y: 0, w: 0, h: 13 }],
    ['zero height', { i: 'TSLA', x: 0, y: 0, w: 6, h: 0 }],
    ['absurd width', { i: 'TSLA', x: 0, y: 0, w: 10000, h: 13 }],
    ['width stored as text', { i: 'TSLA', x: 0, y: 0, w: '6', h: 13 }],
  ])('replaces an item with %s', (_label, bad) => {
    const state = parseDashboard({
      version: 1,
      panels: [{ symbol: 'TSLA', view: 'chart' }],
      layouts: { lg: [bad] },
    })
    expect(state.layouts.lg).toEqual([{ i: 'TSLA', x: 0, y: 0, w: 6, h: 13 }])
  })

  it('shrinks a panel stored wider than the breakpoint has columns', () => {
    const state = parseDashboard({
      version: 1,
      panels: [{ symbol: 'TSLA', view: 'chart' }],
      layouts: { sm: [{ i: 'TSLA', x: 0, y: 0, w: 6, h: 13 }] },
    })
    expect(state.layouts.sm).toEqual([{ i: 'TSLA', x: 0, y: 0, w: 1, h: 13 }])
  })

  it('keeps one item when the same panel is stored twice, and ignores other-case ids', () => {
    const state = parseDashboard({
      version: 1,
      panels: [{ symbol: 'TSLA', view: 'chart' }],
      layouts: {
        lg: [
          { i: 'TSLA', x: 0, y: 0, w: 12, h: 20 },
          { i: 'TSLA', x: 6, y: 0, w: 6, h: 13 },
          { i: 'tsla', x: 0, y: 40, w: 6, h: 13 },
        ],
      },
    })
    expect(state.layouts.lg).toEqual([{ i: 'TSLA', x: 0, y: 0, w: 12, h: 20 }])
  })
})

describe('reading saved data that cannot be used', () => {
  it.each([
    ['not JSON', '{oops'],
    ['null', 'null'],
    ['an array', '[]'],
    ['a string', '"x"'],
    ['an object without a version', '{"panels":[]}'],
    ['another version', '{"version":2,"panels":[]}'],
  ])('starts empty, flags it, and keeps a backup when the data is %s', (_label, text) => {
    const storage = memoryStorage(text)
    expect(readDashboard(storage)).toEqual({ state: emptyDashboard, restoreFailed: true })
    expect(storage.getItem(`${STORAGE_KEY}.bak`)).toBe(text)
  })

  it('does not flag a first visit or an empty saved dashboard', () => {
    expect(readDashboard(memoryStorage()).restoreFailed).toBe(false)
    const saved = memoryStorage(JSON.stringify({ version: 1, panels: [], layouts: {} }))
    const result = readDashboard(saved)
    expect(result.state.panels).toEqual([])
    expect(result.restoreFailed).toBe(false)
  })

  it('does not flag storage that cannot be read at all', () => {
    expect(readDashboard(throwingStorage).restoreFailed).toBe(false)
  })
})

describe('saving', () => {
  it('reports success', () => {
    expect(saveDashboard(emptyDashboard, memoryStorage())).toBe(true)
  })

  it('reports failure when storage is full or blocked', () => {
    expect(saveDashboard(emptyDashboard, throwingStorage)).toBe(false)
  })
})
