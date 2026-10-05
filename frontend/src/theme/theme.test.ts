import { describe, expect, it } from 'vitest'
import { applyTheme, loadTheme, parseTheme, saveTheme, THEME_KEY } from './theme'

function memoryStorage(initial?: string): Storage {
  const map = new Map<string, string>()
  if (initial !== undefined) map.set(THEME_KEY, initial)
  return {
    getItem: (key) => map.get(key) ?? null,
    setItem: (key, value) => void map.set(key, value),
  } as Storage
}

const blockedStorage = {
  getItem: () => {
    throw new DOMException('blocked', 'SecurityError')
  },
  setItem: () => {
    throw new DOMException('full', 'QuotaExceededError')
  },
} as unknown as Storage

describe('parseTheme', () => {
  it.each(['light', 'dark'] as const)('keeps a saved %s choice', (mode) => {
    expect(parseTheme(mode)).toBe(mode)
  })

  it.each([null, undefined, '', 'system', 'DARK', 'sepia', 42, {}])(
    'follows the system for %j',
    (value) => {
      expect(parseTheme(value)).toBe('system')
    },
  )
})

describe('theme storage', () => {
  it('restores what was saved', () => {
    const storage = memoryStorage()
    expect(saveTheme('dark', storage)).toBe(true)
    expect(loadTheme(storage)).toBe('dark')
  })

  it('follows the system on a first visit', () => {
    expect(loadTheme(memoryStorage())).toBe('system')
  })

  it('ignores a value that was edited by hand', () => {
    expect(loadTheme(memoryStorage('purple'))).toBe('system')
  })

  it('keeps working when storage is blocked', () => {
    expect(loadTheme(blockedStorage)).toBe('system')
    expect(saveTheme('light', blockedStorage)).toBe(false)
  })
})

describe('applyTheme', () => {
  it('marks the page with an explicit choice', () => {
    const root = document.createElement('html')
    applyTheme('dark', root)
    expect(root.getAttribute('data-theme')).toBe('dark')
    applyTheme('light', root)
    expect(root.getAttribute('data-theme')).toBe('light')
  })

  it('removes the mark for "system" so the stylesheet follows the operating system', () => {
    const root = document.createElement('html')
    applyTheme('dark', root)
    applyTheme('system', root)
    expect(root.hasAttribute('data-theme')).toBe(false)
  })
})
