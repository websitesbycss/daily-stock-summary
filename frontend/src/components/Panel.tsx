import { lazy, Suspense } from 'react'
import { formatDay } from '../lib/date'
import { describeApiError } from '../notify'
import type { View } from '../dashboard/types'
import { useDailySummary } from '../queries/useDailySummary'
import { ErrorBoundary } from './ErrorBoundary'
import { ChevronLeftIcon, ChevronRightIcon, CloseIcon, GripIcon, RefreshIcon } from './Icons'
import { SummaryTable } from './SummaryTable'

// Recharts is the bulk of the bundle and a panel may never leave the table view.
const SummaryChart = lazy(() =>
  import('./SummaryChart').then((module) => ({ default: module.SummaryChart })),
)

export const panelId = (symbol: string) => `panel-${symbol}`

interface Props {
  /** Already normalized (trimmed, upper-case) and validated. */
  symbol: string
  view: View
  canMoveEarlier: boolean
  canMoveLater: boolean
  onViewChange: (view: View) => void
  onMove: (direction: -1 | 1) => void
  onRemove: () => void
}

function Skeleton() {
  return (
    <div className="skeleton" role="status" aria-label="Loading data">
      <div className="skeleton__block" />
    </div>
  )
}

export function Panel({
  symbol,
  view,
  canMoveEarlier,
  canMoveLater,
  onViewChange,
  onMove,
  onRemove,
}: Props) {
  const { data, error, isPending, isFetching, refetch } = useDailySummary(symbol)
  const first = data?.[0]
  const last = data?.[data.length - 1]

  return (
    <section id={panelId(symbol)} className="panel" aria-label={symbol} tabIndex={-1}>
      <header className="panel__bar">
        <span className="panel__grip" aria-hidden="true">
          <GripIcon />
        </span>
        <div className="panel__heading">
          <h2 className="panel__title">{symbol}</h2>
          {data && first && last && (
            <p className="panel__range">
              {data.length} {data.length === 1 ? 'day' : 'days'}, {formatDay(first.day)} to{' '}
              {formatDay(last.day)}
            </p>
          )}
        </div>
        <div className="panel__actions">
          <div className="segmented" role="group" aria-label={`${symbol} view`}>
            {(['chart', 'table'] as const).map((option) => (
              <button
                key={option}
                type="button"
                className="segmented__option"
                aria-pressed={view === option}
                onClick={() => onViewChange(option)}
              >
                {option === 'chart' ? 'Chart' : 'Table'}
              </button>
            ))}
          </div>
          <button
            type="button"
            className="icon-button"
            aria-label={`Refresh ${symbol}`}
            title="Refresh"
            onClick={() => void refetch()}
            disabled={isFetching}
          >
            <RefreshIcon />
          </button>
          <button
            type="button"
            className="icon-button"
            aria-label={`Move ${symbol} earlier`}
            title="Move earlier"
            onClick={() => canMoveEarlier && onMove(-1)}
            aria-disabled={!canMoveEarlier}
          >
            <ChevronLeftIcon />
          </button>
          <button
            type="button"
            className="icon-button"
            aria-label={`Move ${symbol} later`}
            title="Move later"
            onClick={() => canMoveLater && onMove(1)}
            aria-disabled={!canMoveLater}
          >
            <ChevronRightIcon />
          </button>
          <button
            type="button"
            className="icon-button"
            aria-label={`Remove ${symbol}`}
            title="Remove"
            onClick={onRemove}
          >
            <CloseIcon />
          </button>
        </div>
      </header>

      <div className={view === 'chart' ? 'panel__body panel__body--chart' : 'panel__body'}>
        {isPending ? (
          <Skeleton />
        ) : !data ? (
          // A failed refresh keeps showing the data already loaded; the toast reports the failure.
          <div className="state" role="alert">
            <p>{error ? describeApiError(error, symbol) : 'Something went wrong. Try again.'}</p>
            <button type="button" className="button" onClick={() => void refetch()}>
              Try again
            </button>
          </div>
        ) : data.length === 0 ? (
          <div className="state">
            <p>{symbol} has no trading data for the past month.</p>
          </div>
        ) : (
          <ErrorBoundary
            resetKeys={[view]}
            fallback={(reset) => (
              <div className="state" role="alert">
                <p>This view could not be displayed. Try the other view, or reload the page.</p>
                <button type="button" className="button" onClick={reset}>
                  Try again
                </button>
              </div>
            )}
          >
            {view === 'chart' ? (
              <Suspense fallback={<Skeleton />}>
                <SummaryChart data={data} symbol={symbol} />
              </Suspense>
            ) : (
              <SummaryTable data={data} label={`${symbol} daily summary`} />
            )}
          </ErrorBoundary>
        )}
      </div>
    </section>
  )
}
