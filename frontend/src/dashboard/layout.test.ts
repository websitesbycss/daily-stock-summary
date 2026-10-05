import { describe, expect, it } from 'vitest'
import { breakpointForWidth, firstFreeSlot, moveInReadingOrder, readingOrder } from './layout'
import type { GridItem } from './types'

const item = (i: string, x: number, y: number, w = 6, h = 13): GridItem => ({ i, x, y, w, h })

describe('firstFreeSlot', () => {
  it('puts the first panel in the top-left corner', () => {
    expect(firstFreeSlot([], 12, { w: 6, h: 13 })).toEqual({ x: 0, y: 0 })
  })

  it('puts the second panel beside the first when the row has room', () => {
    expect(firstFreeSlot([item('A', 0, 0)], 12, { w: 6, h: 13 })).toEqual({ x: 6, y: 0 })
  })

  it('starts a new row when the row is full', () => {
    const full = [item('A', 0, 0), item('B', 6, 0)]
    expect(firstFreeSlot(full, 12, { w: 6, h: 13 })).toEqual({ x: 0, y: 13 })
  })

  it('fills the gap left by a removed panel', () => {
    // B (top right) was removed; C sits on the second row.
    const withGap = [item('A', 0, 0), item('C', 0, 13)]
    expect(firstFreeSlot(withGap, 12, { w: 6, h: 13 })).toEqual({ x: 6, y: 0 })
  })

  it('stacks panels in a single column on a phone', () => {
    expect(firstFreeSlot([item('A', 0, 0, 1)], 1, { w: 1, h: 13 })).toEqual({ x: 0, y: 13 })
  })
})

describe('readingOrder', () => {
  it('reads top to bottom, then left to right', () => {
    const items = [item('D', 6, 13), item('B', 6, 0), item('C', 0, 13), item('A', 0, 0)]
    expect(readingOrder(items)).toEqual(['A', 'B', 'C', 'D'])
  })
})

describe('moveInReadingOrder', () => {
  const grid = [item('A', 0, 0), item('B', 6, 0, 6, 20), item('C', 0, 13)]

  it('swaps positions with the next panel and keeps each panel size', () => {
    const moved = moveInReadingOrder(grid, 'A', 1)
    expect(moved.find((i) => i.i === 'A')).toEqual(item('A', 6, 0, 6, 13))
    expect(moved.find((i) => i.i === 'B')).toEqual(item('B', 0, 0, 6, 20))
    expect(readingOrder(moved)).toEqual(['B', 'A', 'C'])
  })

  it('moves a panel earlier across a row boundary', () => {
    const moved = moveInReadingOrder(grid, 'C', -1)
    expect(readingOrder(moved)).toEqual(['A', 'C', 'B'])
    expect(moved.find((i) => i.i === 'C')).toMatchObject({ x: 6, y: 0 })
  })

  it('returns the same layout when the panel is already first or last', () => {
    expect(moveInReadingOrder(grid, 'A', -1)).toBe(grid)
    expect(moveInReadingOrder(grid, 'C', 1)).toBe(grid)
  })

  it('returns the same layout for an unknown panel', () => {
    expect(moveInReadingOrder(grid, 'ZZZ', 1)).toBe(grid)
  })
})

describe('breakpointForWidth', () => {
  it.each([
    [1920, 'xl'],
    [1501, 'xl'],
    [1500, 'lg'],
    [1200, 'lg'],
    [901, 'lg'],
    [900, 'md'],
    [700, 'md'],
    [601, 'md'],
    [600, 'sm'],
    [390, 'sm'],
    [1, 'sm'],
  ])('a %i px container uses the %s layout', (width, expected) => {
    expect(breakpointForWidth(width)).toBe(expected)
  })

  it('falls back to the smallest layout before the container has been measured', () => {
    expect(breakpointForWidth(0)).toBe('sm')
  })
})
