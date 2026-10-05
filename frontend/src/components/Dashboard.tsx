import { useMemo, type Dispatch } from 'react'
import { Responsive, useContainerWidth } from 'react-grid-layout'
import 'react-grid-layout/css/styles.css'
import type { DashboardAction } from '../dashboard/dashboardReducer'
import {
  BREAKPOINT_WIDTHS,
  BREAKPOINTS,
  breakpointForWidth,
  COLUMNS,
  MIN_SIZE,
  readingOrder,
} from '../dashboard/layout'
import type { DashboardState, GridItem, Layouts } from '../dashboard/types'
import { Panel } from './Panel'

interface Props {
  state: DashboardState
  dispatch: Dispatch<DashboardAction>
  /** Called after a panel is removed so the app can put focus somewhere sensible. */
  onRemoved: (symbol: string) => void
  /** Polite screen reader announcement for changes that are otherwise only visual. */
  announce: (message: string) => void
}

/** Adds resize limits for the grid; they are not stored because they never change. */
function withLimits(layouts: Layouts): Layouts {
  const limited: Layouts = {}
  for (const breakpoint of BREAKPOINTS) {
    limited[breakpoint] = layouts[breakpoint]?.map((item) => ({
      ...item,
      minW: Math.min(MIN_SIZE.w, COLUMNS[breakpoint]),
      minH: MIN_SIZE.h,
    }))
  }
  return limited
}

const ROW_HEIGHT = 30
const MARGIN = [16, 16] as const

export function Dashboard({ state, dispatch, onRemoved, announce }: Props) {
  const { width, containerRef, mounted } = useContainerWidth()
  // Derived from the width and handed to the grid, so the grid, the move buttons and the saved
  // layout always refer to the same breakpoint (the grid's own change events do not fire for the
  // first layout, and a resize can make its report cover several breakpoints at once).
  const breakpoint = breakpointForWidth(width)
  const gridLayouts = useMemo(() => withLimits(state.layouts), [state.layouts])
  const order = readingOrder(state.layouts[breakpoint] ?? [])

  const panelsInOrder = [...state.panels].sort(
    (a, b) => order.indexOf(a.symbol) - order.indexOf(b.symbol),
  )

  function saveLayout(layout: readonly GridItem[]) {
    dispatch({ type: 'layoutChange', breakpoint, layout })
  }

  function move(symbol: string, direction: -1 | 1) {
    const index = order.indexOf(symbol)
    dispatch({ type: 'move', symbol, direction, breakpoint })
    announce(`Moved ${symbol} to position ${index + direction + 1} of ${order.length}.`)
  }

  return (
    <div ref={containerRef} className="dashboard">
      {mounted && state.panels.length > 0 && (
        <Responsive
          width={width}
          breakpoint={breakpoint}
          layouts={gridLayouts}
          breakpoints={BREAKPOINT_WIDTHS}
          cols={COLUMNS}
          rowHeight={ROW_HEIGHT}
          margin={MARGIN}
          dragConfig={{
            enabled: true,
            bounded: false,
            handle: '.panel__bar',
            cancel: '.panel__actions',
            threshold: 3,
          }}
          resizeConfig={{ enabled: true, handles: ['se'] }}
          // Saved only when the user finishes a drag or resize. The grid also reports layouts it
          // recomputes by itself (for example while the window jumps between sizes), and those must
          // not overwrite what the user arranged.
          onDragStop={saveLayout}
          onResizeStop={saveLayout}
        >
          {/* DOM order follows the visual order so Tab and screen readers match what is on screen. */}
          {panelsInOrder.map((panel) => (
            <div key={panel.symbol} className="dashboard__item">
              <Panel
                symbol={panel.symbol}
                view={panel.view}
                canMoveEarlier={order.indexOf(panel.symbol) > 0}
                canMoveLater={order.indexOf(panel.symbol) < order.length - 1}
                onViewChange={(view) => dispatch({ type: 'setView', symbol: panel.symbol, view })}
                onMove={(direction) => move(panel.symbol, direction)}
                onRemove={() => {
                  dispatch({ type: 'remove', symbol: panel.symbol })
                  announce(`${panel.symbol} removed.`)
                  onRemoved(panel.symbol)
                }}
              />
            </div>
          ))}
        </Responsive>
      )}
    </div>
  )
}
