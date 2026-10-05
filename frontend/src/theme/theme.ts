export type ThemeMode = 'light' | 'dark' | 'system'

export const THEME_KEY = 'dss.theme'

export const THEME_MODES: readonly ThemeMode[] = ['light', 'dark', 'system']

/** Anything that is not a known mode (missing, edited by hand, from another version) means "follow the system". */
export function parseTheme(value: unknown): ThemeMode {
  return value === 'light' || value === 'dark' ? value : 'system'
}

function safeLocalStorage(): Storage | undefined {
  try {
    return window.localStorage
  } catch {
    return undefined
  }
}

/** Storage can be blocked (private windows); the theme then simply follows the system. */
export function loadTheme(storage: Storage | undefined = safeLocalStorage()): ThemeMode {
  try {
    return parseTheme(storage?.getItem(THEME_KEY))
  } catch {
    return 'system'
  }
}

export function saveTheme(
  mode: ThemeMode,
  storage: Storage | undefined = safeLocalStorage(),
): boolean {
  try {
    if (!storage) return false
    storage.setItem(THEME_KEY, mode)
    return true
  } catch {
    return false
  }
}

/** "system" removes the attribute so the stylesheet's prefers-color-scheme rules apply. */
export function applyTheme(mode: ThemeMode, root: HTMLElement = document.documentElement): void {
  if (mode === 'system') root.removeAttribute('data-theme')
  else root.setAttribute('data-theme', mode)
}
