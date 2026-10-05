import type { Breakpoint, GridItem } from './types'

/** Container widths (px) at which the grid switches layout. */
export const BREAKPOINT_WIDTHS: Record<Breakpoint, number> = { xl: 1500, lg: 900, md: 600, sm: 0 }

export const COLUMNS: Record<Breakpoint, number> = { xl: 12, lg: 12, md: 8, sm: 1 }

/** Three panels per row on very wide screens, two on laptops and tablets, one on a phone. */
export const DEFAULT_SIZE: Record<Breakpoint, { w: number; h: number }> = {
  xl: { w: 4, h: 12 },
  lg: { w: 6, h: 13 },
  md: { w: 4, h: 13 },
  sm: { w: 1, h: 13 },
}

export const BREAKPOINTS: Breakpoint[] = ['xl', 'lg', 'md', 'sm']

/** Smallest a panel can be resized to: its top bar plus a readable chart. */
export const MIN_SIZE = { w: 3, h: 9 }

/** Same rule the grid uses: the widest breakpoint whose threshold the container width exceeds. */
export function breakpointForWidth(width: number): Breakpoint {
  let match: Breakpoint = 'sm'
  let widest = -1
  for (const breakpoint of BREAKPOINTS) {
    const threshold = BREAKPOINT_WIDTHS[breakpoint]
    if (width > threshold && threshold > widest) {
      match = breakpoint
      widest = threshold
    }
  }
  return match
}

export const MAX_PANELS = 12

function overlaps(a: GridItem, b: GridItem): boolean {
  return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h
}

/** First position, scanning left to right then top to bottom, where a w x h panel fits without overlap. */
export function firstFreeSlot(
  items: readonly GridItem[],
  columns: number,
  size: { w: number; h: number },
): { x: number; y: number } {
  const w = Math.min(size.w, columns)
  const bottom = items.reduce((max, item) => Math.max(max, item.y + item.h), 0)
  for (let y = 0; y <= bottom; y++) {
    for (let x = 0; x + w <= columns; x++) {
      const candidate = { i: '', x, y, w, h: size.h }
      if (!items.some((item) => overlaps(candidate, item))) return { x, y }
    }
  }
  return { x: 0, y: bottom }
}

/** Panels in reading order (top to bottom, then left to right). */
export function readingOrder(items: readonly GridItem[]): string[] {
  return [...items].sort((a, b) => a.y - b.y || a.x - b.x).map((item) => item.i)
}

/**
 * Swaps a panel's position with its neighbour in reading order. Returns the same array when the
 * panel is already first/last (or unknown). Only positions move; each panel keeps its own size.
 */
export function moveInReadingOrder(
  items: readonly GridItem[],
  symbol: string,
  direction: -1 | 1,
): readonly GridItem[] {
  const order = readingOrder(items)
  const index = order.indexOf(symbol)
  const neighbourIndex = index + direction
  if (index === -1 || neighbourIndex < 0 || neighbourIndex >= order.length) return items

  const neighbour = order[neighbourIndex]
  const self = items.find((item) => item.i === symbol)!
  const other = items.find((item) => item.i === neighbour)!
  return items.map((item) => {
    if (item.i === symbol) return { ...item, x: other.x, y: other.y }
    if (item.i === neighbour) return { ...item, x: self.x, y: self.y }
    return item
  })
}
