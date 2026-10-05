import { describe, expect, it } from 'vitest'
import { formatPrice, formatVolume, formatVolumeCompact } from './format'

describe('formatPrice', () => {
  it('always shows 4 decimals, matching the API precision', () => {
    expect(formatPrice(48)).toBe('48.0000')
    expect(formatPrice(40.2958)).toBe('40.2958')
  })

  it('groups thousands', () => {
    expect(formatPrice(1234.5)).toBe('1,234.5000')
  })
})

describe('formatVolume', () => {
  it('groups digits and shows no decimals', () => {
    expect(formatVolume(49073348)).toBe('49,073,348')
    expect(formatVolume(0)).toBe('0')
    expect(formatVolume(999)).toBe('999')
  })
})

describe('formatVolumeCompact (chart axis)', () => {
  it('abbreviates large numbers to one decimal', () => {
    expect(formatVolumeCompact(49073348)).toBe('49.1M')
    expect(formatVolumeCompact(1500)).toBe('1.5K')
    expect(formatVolumeCompact(2_300_000_000)).toBe('2.3B')
  })

  it('leaves small numbers alone', () => {
    expect(formatVolumeCompact(999)).toBe('999')
    expect(formatVolumeCompact(0)).toBe('0')
  })
})
