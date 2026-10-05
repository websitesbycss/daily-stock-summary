import { isValidSymbol, normalizeSymbol } from '../lib/symbol'
import { emptyDashboard, reconcileLayouts } from './dashboardReducer'
import { BREAKPOINTS, MAX_PANELS } from './layout'
import type { DashboardState, GridItem, Layouts, PanelState, View } from './types'

export const STORAGE_KEY = 'dss.dashboard.v1'
const VERSION = 1

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const isCount = (value: unknown, min: number): value is number =>
  typeof value === 'number' && Number.isInteger(value) && value >= min && value < 10_000

function parsePanels(raw: unknown): PanelState[] {
  if (!Array.isArray(raw)) return []
  const seen = new Set<string>()
  const panels: PanelState[] = []
  for (const entry of raw) {
    if (!isRecord(entry) || typeof entry.symbol !== 'string') continue
    if (!isValidSymbol(entry.symbol)) continue
    const symbol = normalizeSymbol(entry.symbol)
    if (seen.has(symbol) || panels.length >= MAX_PANELS) continue
    seen.add(symbol)
    const view: View = entry.view === 'table' ? 'table' : 'chart'
    panels.push({ symbol, view })
  }
  return panels
}

function parseItems(raw: unknown): GridItem[] {
  if (!Array.isArray(raw)) return []
  const items: GridItem[] = []
  for (const entry of raw) {
    if (!isRecord(entry) || typeof entry.i !== 'string') continue
    if (
      !isCount(entry.x, 0) ||
      !isCount(entry.y, 0) ||
      !isCount(entry.w, 1) ||
      !isCount(entry.h, 1)
    )
      continue
    items.push({ i: entry.i, x: entry.x, y: entry.y, w: entry.w, h: entry.h })
  }
  return items
}

/** Turns untrusted stored data into a consistent dashboard; anything unusable is dropped. */
export function parseDashboard(raw: unknown): DashboardState {
  if (!isRecord(raw) || raw.version !== VERSION) return emptyDashboard
  const panels = parsePanels(raw.panels)
  const layouts: Layouts = {}
  if (isRecord(raw.layouts)) {
    for (const breakpoint of BREAKPOINTS) layouts[breakpoint] = parseItems(raw.layouts[breakpoint])
  }
  return reconcileLayouts({ panels, layouts })
}

export interface LoadResult {
  state: DashboardState
  /** Saved data existed but could not be used (corrupt, or from another version). */
  restoreFailed: boolean
}

/**
 * Storage can be missing, full, or blocked (private windows); the dashboard just starts empty.
 * Unusable saved text is copied to a backup key before the empty dashboard overwrites it.
 */
export function readDashboard(storage: Storage | undefined = safeLocalStorage()): LoadResult {
  let text: string | null | undefined
  try {
    text = storage?.getItem(STORAGE_KEY)
  } catch (error) {
    console.warn('Saved dashboard could not be read', error)
    return { state: emptyDashboard, restoreFailed: false }
  }
  if (!text) return { state: emptyDashboard, restoreFailed: false }

  try {
    const raw: unknown = JSON.parse(text)
    if (isRecord(raw) && raw.version === VERSION) {
      return { state: parseDashboard(raw), restoreFailed: false }
    }
    console.warn('Saved dashboard has an unknown version; starting empty')
  } catch (error) {
    console.warn('Saved dashboard is not valid JSON; starting empty', error)
  }
  backUp(storage, text)
  return { state: emptyDashboard, restoreFailed: true }
}

export const loadDashboard = (storage?: Storage): DashboardState => readDashboard(storage).state

function backUp(storage: Storage | undefined, text: string) {
  try {
    storage?.setItem(`${STORAGE_KEY}.bak`, text)
  } catch {
    // Nothing more can be done if the backup cannot be written either.
  }
}

/** Returns false when the state could not be saved (full quota, blocked storage, no storage). */
export function saveDashboard(
  state: DashboardState,
  storage: Storage | undefined = safeLocalStorage(),
): boolean {
  try {
    if (!storage) return false
    storage.setItem(STORAGE_KEY, JSON.stringify({ version: VERSION, ...state }))
    return true
  } catch (error) {
    console.warn('Dashboard could not be saved', error)
    return false
  }
}

function safeLocalStorage(): Storage | undefined {
  try {
    return window.localStorage
  } catch {
    return undefined
  }
}
