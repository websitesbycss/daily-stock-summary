import { describe, expect, it } from 'vitest'
import { dashboardReducer, emptyDashboard, type DashboardAction } from './dashboardReducer'
import { readingOrder } from './layout'
import type { Breakpoint, DashboardState } from './types'

function run(...actions: DashboardAction[]): DashboardState {
  return actions.reduce(dashboardReducer, emptyDashboard)
}

const add = (symbol: string): DashboardAction => ({ type: 'add', symbol })
const at = (state: DashboardState, breakpoint: Breakpoint, symbol: string) =>
  state.layouts[breakpoint]!.find((item) => item.i === symbol)

describe('adding panels', () => {
  it('lays out two per row on desktop, two per row on tablet, one per row on a phone', () => {
    const state = run(add('TSLA'), add('AAPL'), add('MSFT'))

    expect(at(state, 'lg', 'TSLA')).toEqual({ i: 'TSLA', x: 0, y: 0, w: 6, h: 13 })
    expect(at(state, 'lg', 'AAPL')).toEqual({ i: 'AAPL', x: 6, y: 0, w: 6, h: 13 })
    expect(at(state, 'lg', 'MSFT')).toEqual({ i: 'MSFT', x: 0, y: 13, w: 6, h: 13 })

    expect(at(state, 'md', 'AAPL')).toEqual({ i: 'AAPL', x: 4, y: 0, w: 4, h: 13 })
    expect(at(state, 'sm', 'MSFT')).toEqual({ i: 'MSFT', x: 0, y: 26, w: 1, h: 13 })
  })

  it('starts every panel on the chart view', () => {
    expect(run(add('TSLA')).panels).toEqual([{ symbol: 'TSLA', view: 'chart' }])
  })

  it('ignores a symbol that is already on the dashboard', () => {
    const before = run(add('TSLA'))
    expect(dashboardReducer(before, add('TSLA'))).toBe(before)
  })

  it('puts a new panel into the gap a removed panel left', () => {
    const state = run(add('A'), add('B'), add('C'), { type: 'remove', symbol: 'B' }, add('D'))
    expect(at(state, 'lg', 'D')).toMatchObject({ x: 6, y: 0 })
  })
})

describe('very wide screens', () => {
  it('lays out three panels per row, then starts a new row', () => {
    const state = run(add('TSLA'), add('AAPL'), add('MSFT'), add('NVDA'))

    expect(at(state, 'xl', 'TSLA')).toEqual({ i: 'TSLA', x: 0, y: 0, w: 4, h: 12 })
    expect(at(state, 'xl', 'AAPL')).toEqual({ i: 'AAPL', x: 4, y: 0, w: 4, h: 12 })
    expect(at(state, 'xl', 'MSFT')).toEqual({ i: 'MSFT', x: 8, y: 0, w: 4, h: 12 })
    expect(at(state, 'xl', 'NVDA')).toEqual({ i: 'NVDA', x: 0, y: 12, w: 4, h: 12 })
  })

  it('keeps laptops at two per row', () => {
    const state = run(add('TSLA'), add('AAPL'), add('MSFT'))
    expect(at(state, 'lg', 'MSFT')).toEqual({ i: 'MSFT', x: 0, y: 13, w: 6, h: 13 })
  })
})

describe('removing panels', () => {
  it('drops the panel and its layout items at every breakpoint', () => {
    const state = run(add('TSLA'), add('AAPL'), { type: 'remove', symbol: 'TSLA' })
    expect(state.panels.map((p) => p.symbol)).toEqual(['AAPL'])
    for (const breakpoint of ['lg', 'md', 'sm'] as const) {
      expect(state.layouts[breakpoint]!.map((i) => i.i)).toEqual(['AAPL'])
    }
  })
})

describe('removing a symbol that is not there', () => {
  it('changes nothing', () => {
    const state = run(add('TSLA'))
    expect(dashboardReducer(state, { type: 'remove', symbol: 'AAPL' })).toBe(state)
  })
})

describe('switching a panel view', () => {
  it('changes only that panel', () => {
    const state = run(add('TSLA'), add('AAPL'), { type: 'setView', symbol: 'AAPL', view: 'table' })
    expect(state.panels).toEqual([
      { symbol: 'TSLA', view: 'chart' },
      { symbol: 'AAPL', view: 'table' },
    ])
  })
})

describe('layout changes reported by the grid', () => {
  it('stores the positions after a drag', () => {
    const start = run(add('TSLA'), add('AAPL'))
    const dragged = dashboardReducer(start, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: [
        { i: 'TSLA', x: 6, y: 0, w: 6, h: 13 },
        { i: 'AAPL', x: 0, y: 0, w: 6, h: 13 },
      ],
    })
    expect(readingOrder(dragged.layouts.lg!)).toEqual(['AAPL', 'TSLA'])
  })

  it('keeps the same state when the grid reports what is already stored', () => {
    const state = run(add('TSLA'), add('AAPL'))
    expect(
      dashboardReducer(state, {
        type: 'layoutChange',
        breakpoint: 'lg',
        layout: state.layouts.lg!,
      }),
    ).toBe(state)
  })

  it('ignores layout items for panels that are no longer on the dashboard', () => {
    const state = run(add('TSLA'))
    const next = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: [
        { i: 'TSLA', x: 0, y: 0, w: 6, h: 13 },
        { i: 'GONE', x: 6, y: 0, w: 6, h: 13 },
      ],
    })
    expect(next.layouts.lg!.map((i) => i.i)).toEqual(['TSLA'])
  })

  it('leaves the other breakpoints alone and ignores the item flags added by the grid', () => {
    const state = run(add('TSLA'), add('AAPL'))
    const next = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: state.layouts.lg!.map((item) => ({ ...item, moved: false, static: false })),
    })
    expect(next).toBe(state)
    expect(next.layouts.md).toEqual(state.layouts.md)
  })

  it('keeps a resized panel size', () => {
    const state = run(add('TSLA'))
    const next = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: [{ i: 'TSLA', x: 0, y: 0, w: 12, h: 20 }],
    })
    expect(at(next, 'lg', 'TSLA')).toEqual({ i: 'TSLA', x: 0, y: 0, w: 12, h: 20 })
  })
})

describe('moving a panel by keyboard', () => {
  it('swaps it with its neighbour at the current breakpoint only', () => {
    const state = run(add('TSLA'), add('AAPL'), {
      type: 'move',
      symbol: 'TSLA',
      direction: 1,
      breakpoint: 'lg',
    })
    expect(readingOrder(state.layouts.lg!)).toEqual(['AAPL', 'TSLA'])
    expect(readingOrder(state.layouts.md!)).toEqual(['TSLA', 'AAPL'])
  })

  it('does nothing for the first panel moving earlier', () => {
    const before = run(add('TSLA'), add('AAPL'))
    expect(
      dashboardReducer(before, { type: 'move', symbol: 'TSLA', direction: -1, breakpoint: 'lg' }),
    ).toBe(before)
  })
})

describe('layout reports in unusual shapes', () => {
  it('treats the same positions in a different array order as no change', () => {
    const state = run(add('TSLA'), add('AAPL'))
    const reversed = [...state.layouts.lg!].reverse()
    expect(
      dashboardReducer(state, { type: 'layoutChange', breakpoint: 'lg', layout: reversed }),
    ).toBe(state)
  })

  it('keeps one item when the grid reports a panel twice', () => {
    const state = run(add('TSLA'))
    const next = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: [
        { i: 'TSLA', x: 0, y: 0, w: 12, h: 20 },
        { i: 'TSLA', x: 6, y: 0, w: 6, h: 13 },
      ],
    })
    expect(next.layouts.lg).toEqual([{ i: 'TSLA', x: 0, y: 0, w: 12, h: 20 }])
  })

  it('places a panel the grid did not report in the first free slot', () => {
    const state = run(add('TSLA'), add('AAPL'))
    const next = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'lg',
      layout: [{ i: 'TSLA', x: 0, y: 0, w: 12, h: 13 }],
    })
    expect(at(next, 'lg', 'AAPL')).toEqual({ i: 'AAPL', x: 0, y: 13, w: 6, h: 13 })
  })
})

describe('moving panels at other breakpoints', () => {
  it.each(['md', 'sm'] as const)('swaps neighbours in the %s layout only', (breakpoint) => {
    const start = run(add('TSLA'), add('AAPL'))
    const state = dashboardReducer(start, {
      type: 'move',
      symbol: 'AAPL',
      direction: -1,
      breakpoint,
    })
    expect(readingOrder(state.layouts[breakpoint]!)).toEqual(['AAPL', 'TSLA'])
    expect(readingOrder(state.layouts.lg!)).toEqual(['TSLA', 'AAPL'])
  })
})

describe('a report for one breakpoint', () => {
  it('replaces only that breakpoint, even if the positions look like another layout', () => {
    const state = run(add('TSLA'), add('AAPL'))
    const next = dashboardReducer(state, {
      type: 'layoutChange',
      breakpoint: 'sm',
      layout: [
        { i: 'AAPL', x: 0, y: 0, w: 1, h: 13 },
        { i: 'TSLA', x: 0, y: 13, w: 1, h: 13 },
      ],
    })

    expect(readingOrder(next.layouts.sm!)).toEqual(['AAPL', 'TSLA'])
    expect(next.layouts.lg).toEqual(state.layouts.lg)
    expect(next.layouts.md).toEqual(state.layouts.md)
  })
})
