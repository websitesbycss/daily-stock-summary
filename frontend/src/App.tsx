import { useState } from 'react'
import { Dashboard } from './components/Dashboard'
import { GhostPanel } from './components/GhostPanel'
import { panelId } from './components/Panel'
import { SymbolForm } from './components/SymbolForm'
import { ThemeToggle } from './components/ThemeToggle'
import { MAX_PANELS } from './dashboard/layout'
import { useDashboard } from './dashboard/useDashboard'
import { notify } from './notify'
import { useTheme } from './theme/useTheme'

const EXAMPLES = ['TSLA', 'AAPL', '^GSPC', 'BTC-USD']
const FLASH_MS = 1200

/** Scrolls to a panel, focuses it, and flashes its border so the user sees which one it is. */
function focusPanel(symbol: string) {
  const panel = document.getElementById(panelId(symbol))
  if (!panel) return
  panel.scrollIntoView({ block: 'nearest' })
  panel.focus({ preventScroll: true })
  panel.setAttribute('data-flash', '')
  window.setTimeout(() => panel.removeAttribute('data-flash'), FLASH_MS)
}

export default function App() {
  const { state, dispatch } = useDashboard()
  const { mode, setMode } = useTheme()
  const [announcement, setAnnouncement] = useState('')

  function addSymbol(symbol: string) {
    if (state.panels.some((panel) => panel.symbol === symbol)) {
      notify.info(`${symbol} is already on the dashboard.`, `duplicate:${symbol}`)
      focusPanel(symbol)
      return
    }
    if (state.panels.length >= MAX_PANELS) {
      notify.info(`The dashboard holds ${MAX_PANELS} symbols. Remove one to add ${symbol}.`, 'full')
      return
    }
    dispatch({ type: 'add', symbol })
    setAnnouncement(`${symbol} added.`)
  }

  // With no symbols the header is a large hero; once there is something to show it becomes a slim bar.
  const compact = state.panels.length > 0

  return (
    <div className={compact ? 'app app--compact' : 'app app--hero'}>
      <header className="topbar">
        <div className="topbar__inner">
          <h1 className="topbar__title">Daily Stock Summary</h1>
          {!compact && (
            <p className="topbar__lede">
              Daily low and high averages and total volume for the past month. Add several symbols,
              then drag a panel by its title bar to rearrange them.
            </p>
          )}
          <SymbolForm onSubmit={addSymbol} />
          {!compact && (
            <p className="topbar__examples">
              Try{' '}
              {EXAMPLES.map((example, index) => (
                <span key={example}>
                  {index > 0 && ', '}
                  <button type="button" className="link-button" onClick={() => addSymbol(example)}>
                    {example}
                  </button>
                </span>
              ))}
            </p>
          )}
          <ThemeToggle mode={mode} onChange={setMode} />
        </div>
      </header>

      <main className="page">
        {!compact && (
          <div className="empty-state">
            <p className="empty">No symbols yet. Add one above and its chart appears here.</p>
            <GhostPanel />
          </div>
        )}

        <Dashboard
          state={state}
          dispatch={dispatch}
          announce={setAnnouncement}
          onRemoved={() => document.getElementById('symbol')?.focus()}
        />

        <p className="sr-only" role="status" aria-live="polite">
          {announcement}
        </p>
      </main>
    </div>
  )
}
