import { useState } from 'react'
import {
  Bar,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
  type TooltipContentProps,
} from 'recharts'
import type { DailySummary } from '../api/types'
import { formatDay, formatDayShort } from '../lib/date'
import { formatPrice, formatVolume, formatVolumeCompact } from '../lib/format'

interface Props {
  /** Oldest day first, exactly as the API returns it, so the latest day lands on the right. */
  data: DailySummary[]
  symbol: string
}

function ChartTooltip({ active, payload }: TooltipContentProps) {
  const point = payload?.[0]?.payload as DailySummary | undefined
  if (!active || !point) return null
  return (
    <div className="chart-tooltip">
      <p className="chart-tooltip__day">{formatDay(point.day)}</p>
      <dl>
        <dt>High average</dt>
        <dd className="num" style={{ color: 'var(--high)' }}>
          {formatPrice(point.highAverage)}
        </dd>
        <dt>Low average</dt>
        <dd className="num" style={{ color: 'var(--low)' }}>
          {formatPrice(point.lowAverage)}
        </dd>
        <dt>Volume</dt>
        <dd className="num">{formatVolume(point.volume)}</dd>
      </dl>
    </div>
  )
}

/** Below this plot width the volume axis is dropped and ticks thin out so labels stay readable. */
const COMPACT_WIDTH = 420

// Constant props: Recharts re-registers an axis or grid whenever one of these changes identity.
const MARGIN = { top: 8, right: 4, bottom: 0, left: 4 }
const TICK = { fill: 'var(--muted)', fontSize: 12 }
const X_AXIS_LINE = { stroke: 'var(--rule)' }
const CURSOR = { stroke: 'var(--rule)' }
const PRICE_DOMAIN: ['auto', 'auto'] = ['auto', 'auto']
const formatPriceTick = (value: number) => value.toFixed(2)

export function SummaryChart({ data, symbol }: Props) {
  const [compact, setCompact] = useState(false)
  return (
    <figure className="chart">
      <div className="chart__legend" aria-hidden="true">
        <span>
          <i style={{ background: 'var(--high)' }} />
          High average
        </span>
        <span>
          <i style={{ background: 'var(--low)' }} />
          Low average
        </span>
        <span>
          <i style={{ background: 'var(--bars)' }} />
          Volume
        </span>
      </div>
      <div
        className="chart__plot"
        role="img"
        aria-label={`${symbol} daily high and low averages with volume. The same figures are in the table view.`}
      >
        <div className="chart__canvas">
          <ResponsiveContainer
            width="100%"
            height="100%"
            onResize={(width) => setCompact(width < COMPACT_WIDTH)}
          >
            <ComposedChart data={data} margin={MARGIN}>
              <CartesianGrid stroke="var(--rule)" vertical={false} />
              <XAxis
                dataKey="day"
                tickFormatter={formatDayShort}
                tick={TICK}
                tickLine={false}
                axisLine={X_AXIS_LINE}
                minTickGap={compact ? 40 : 24}
              />
              <YAxis
                yAxisId="price"
                orientation="left"
                domain={PRICE_DOMAIN}
                tickFormatter={formatPriceTick}
                tick={TICK}
                tickLine={false}
                axisLine={false}
                width={56}
              />
              <YAxis
                yAxisId="volume"
                orientation="right"
                hide={compact}
                tickFormatter={formatVolumeCompact}
                tick={TICK}
                tickLine={false}
                axisLine={false}
                width={48}
              />
              <Tooltip content={ChartTooltip} cursor={CURSOR} isAnimationActive={false} />
              <Bar yAxisId="volume" dataKey="volume" fill="var(--bars)" isAnimationActive={false} />
              <Line
                yAxisId="price"
                dataKey="highAverage"
                stroke="var(--high)"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
              <Line
                yAxisId="price"
                dataKey="lowAverage"
                stroke="var(--low)"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>
    </figure>
  )
}
