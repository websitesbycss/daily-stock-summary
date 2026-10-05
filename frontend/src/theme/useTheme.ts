import { useCallback, useEffect, useState } from 'react'
import { applyTheme, loadTheme, saveTheme, type ThemeMode } from './theme'

/** The chosen theme mode, applied to the page and remembered in the browser. */
export function useTheme() {
  const [mode, setModeState] = useState<ThemeMode>(() => loadTheme())

  useEffect(() => {
    applyTheme(mode)
  }, [mode])

  const setMode = useCallback((next: ThemeMode) => {
    setModeState(next)
    saveTheme(next)
  }, [])

  return { mode, setMode }
}
