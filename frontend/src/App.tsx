import { useState } from 'react'
import { Dashboard } from './components/Dashboard'
import { panelId } from './components/Panel'
import { SymbolForm } from './components/SymbolForm'
import { MAX_PANELS } from './dashboard/layout'
import { useDashboard } from './dashboard/useDashboard'
import { notify } from './notify'

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

  return (
    <main className="page">
      <header className="intro">
        <p className="wordmark">Daily Stock Summary</p>
        <h1 className="intro__title">Look up stocks</h1>
        <p className="intro__lede">
          Daily low and high averages and total volume for the past month. Add several symbols, then
          drag a panel by its title bar to rearrange them.
        </p>
        <SymbolForm onSubmit={addSymbol} />
        <p className="intro__examples">
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
      </header>

      {state.panels.length === 0 && (
        <p className="empty">No symbols yet. Add one above and its chart appears here.</p>
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
  )
}
