import { describe, expect, it } from 'vitest'
import { formatDay, formatDayShort, parseDay } from './date'

describe('day formatting', () => {
  it('runs in a US time zone, where UTC-parsed dates would show the previous day', () => {
    expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe('America/Los_Angeles')
    expect(new Date('2009-01-30').getDate()).toBe(29) // the trap lib/date.ts avoids
  })

  it('shows the calendar date exactly as given, in any local time zone', () => {
    // new Date('2009-01-30') is UTC midnight and would print Jan 29 in US time zones.
    // The test runner is started under America/Los_Angeles (see vite.config.ts).
    expect(formatDay('2009-01-30')).toBe('Jan 30, 2009')
    expect(formatDayShort('2009-01-30')).toBe('Jan 30')
  })

  it('handles year boundaries and single-digit days', () => {
    expect(formatDay('2026-01-01')).toBe('Jan 1, 2026')
    expect(formatDay('2025-12-31')).toBe('Dec 31, 2025')
  })

  it('splits a day into numeric parts', () => {
    expect(parseDay('2026-03-09')).toEqual({ year: 2026, month: 3, day: 9 })
  })

  it.each(['2026-3-9', '2026-13-01', '2026-00-10', '2026-01-32', 'Jan 30', ''])(
    'rejects malformed day %j',
    (input) => {
      expect(() => parseDay(input)).toThrow(RangeError)
    },
  )
})
