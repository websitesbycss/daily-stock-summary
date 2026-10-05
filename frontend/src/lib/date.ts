/**
 * The backend already buckets days in the exchange's time zone, so `day` is a plain calendar date.
 * It is never turned into a JavaScript Date: `new Date('2009-01-30')` is UTC midnight and shows
 * Jan 29 in the US. Everything here works on the numeric parts.
 */
export interface DayParts {
  year: number
  month: number
  day: number
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const DAY_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/

export function parseDay(day: string): DayParts {
  const match = DAY_PATTERN.exec(day)
  if (match) {
    const parts = { year: Number(match[1]), month: Number(match[2]), day: Number(match[3]) }
    if (parts.month >= 1 && parts.month <= 12 && parts.day >= 1 && parts.day <= 31) return parts
  }
  throw new RangeError(`Invalid day: ${day}`)
}

/** "Jan 30, 2009" for table and tooltip. */
export function formatDay(day: string): string {
  const { year, month, day: d } = parseDay(day)
  return `${MONTHS[month - 1]} ${d}, ${year}`
}

/** "Jan 30" for chart axis ticks; the same parts and month names as formatDay. */
export function formatDayShort(day: string): string {
  const { month, day: d } = parseDay(day)
  return `${MONTHS[month - 1]} ${d}`
}
