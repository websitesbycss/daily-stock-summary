import { describe, expect, it } from 'vitest'
import { isValidSymbol, normalizeSymbol } from './symbol'

describe('isValidSymbol', () => {
  it.each(['TSLA', 'tsla', '^GSPC', 'EURUSD=X', 'BTC-USD', 'VOD.L', 'BRK-B', '7203', '  AAPL  '])(
    'accepts %j',
    (input) => {
      expect(isValidSymbol(input)).toBe(true)
    },
  )

  it.each([
    ['empty', ''],
    ['only spaces', '   '],
    ['special characters', '!!!'],
    ['dot only (would alter the upstream URL path)', '.'],
    ['double dot', '..'],
    ['punctuation without a letter or digit', '^=-'],
    ['16 characters', 'ABCDEFGHIJKLMNOP'],
    ['inner space', 'BRK B'],
    ['slash', 'A/B'],
    ['non-ASCII letter', 'ÅBC'],
  ])('rejects %s', (_label, input) => {
    expect(isValidSymbol(input)).toBe(false)
  })

  it('accepts exactly 15 characters', () => {
    expect(isValidSymbol('ABCDEFGHIJKLMNO')).toBe(true)
  })
})

describe('normalizeSymbol', () => {
  it('trims and upper-cases, as the backend does', () => {
    expect(normalizeSymbol('  btc-usd ')).toBe('BTC-USD')
  })
})
