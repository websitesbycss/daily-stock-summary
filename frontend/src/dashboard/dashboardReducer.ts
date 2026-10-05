import { BREAKPOINTS, COLUMNS, DEFAULT_SIZE, firstFreeSlot, moveInReadingOrder } from './layout'
import type { Breakpoint, DashboardState, GridItem, Layouts, View } from './types'

export type DashboardAction =
  | { type: 'add'; symbol: string }
  | { type: 'remove'; symbol: string }
  | { type: 'setView'; symbol: string; view: View }
  /** The grid reports the layout of the breakpoint it is showing; other breakpoints are not touched. */
  | { type: 'layoutChange'; breakpoint: Breakpoint; layout: readonly GridItem[] }
  | { type: 'move'; symbol: string; direction: -1 | 1; breakpoint: Breakpoint }

export const emptyDashboard: DashboardState = { panels: [], layouts: {} }

/**
 * Makes every breakpoint's layout match the panel list: orphans are dropped, missing panels get
 * the first free slot, and items are clamped to the column count.
 */
export function reconcileLayouts(state: DashboardState): DashboardState {
  const symbols = new Set(state.panels.map((panel) => panel.symbol))
  const layouts: Layouts = {}
  for (const breakpoint of BREAKPOINTS) {
    const columns = COLUMNS[breakpoint]
    const seen = new Set<string>()
    const items: GridItem[] = (state.layouts[breakpoint] ?? [])
      // One item per panel: duplicate ids would give the grid duplicate React keys.
      .filter((item) => symbols.has(item.i) && !seen.has(item.i) && seen.add(item.i))
      .map((item) => {
        const w = Math.min(item.w, columns)
        return { ...item, w, x: Math.min(item.x, columns - w) }
      })
    for (const panel of state.panels) {
      if (items.some((item) => item.i === panel.symbol)) continue
      const size = DEFAULT_SIZE[breakpoint]
      items.push({ i: panel.symbol, ...firstFreeSlot(items, columns, size), ...size })
    }
    layouts[breakpoint] = items
  }
  return { ...state, layouts }
}

/** Compares positions, ignoring the order of the items in each array (the grid may report them sorted). */
function sameLayouts(a: Layouts, b: Layouts): boolean {
  const canonical = (layouts: Layouts) =>
    BREAKPOINTS.map((breakpoint) =>
      [...(layouts[breakpoint] ?? [])].sort((p, q) => p.i.localeCompare(q.i)),
    )
  return JSON.stringify(canonical(a)) === JSON.stringify(canonical(b))
}

export function dashboardReducer(state: DashboardState, action: DashboardAction): DashboardState {
  switch (action.type) {
    case 'add': {
      if (state.panels.some((panel) => panel.symbol === action.symbol)) return state
      return reconcileLayouts({
        ...state,
        panels: [...state.panels, { symbol: action.symbol, view: 'chart' }],
      })
    }
    case 'remove': {
      if (!state.panels.some((panel) => panel.symbol === action.symbol)) return state
      return reconcileLayouts({
        ...state,
        panels: state.panels.filter((panel) => panel.symbol !== action.symbol),
      })
    }
    case 'setView': {
      return {
        ...state,
        panels: state.panels.map((panel) =>
          panel.symbol === action.symbol ? { ...panel, view: action.view } : panel,
        ),
      }
    }
    case 'layoutChange': {
      // Keep our state when nothing actually moved, so the report that follows every render does
      // not cause another render. The grid decorates items with its own flags; keep only ours.
      const reported = action.layout.map(({ i, x, y, w, h }) => ({ i, x, y, w, h }))
      const next = reconcileLayouts({
        ...state,
        layouts: { ...state.layouts, [action.breakpoint]: reported },
      })
      return sameLayouts(next.layouts, state.layouts) ? state : next
    }
    case 'move': {
      const current = state.layouts[action.breakpoint]
      if (!current) return state
      const moved = moveInReadingOrder(current, action.symbol, action.direction)
      if (moved === current) return state
      return { ...state, layouts: { ...state.layouts, [action.breakpoint]: [...moved] } }
    }
  }
}
