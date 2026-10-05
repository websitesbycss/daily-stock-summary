/**
 * A faint outline of a panel (title bar, volume bars and the high and low lines) so the empty dashboard hints at what
 * a symbol will look like. Decorative only: the numbers are invented shapes, hidden from assistive technology, and
 * not interactive.
 */
const VOLUME = [46, 62, 38, 54, 70, 42, 58, 34, 50, 66, 44, 56, 72, 40, 52, 64, 36, 60, 48, 68, 45]
const HIGH = [38, 34, 40, 30, 26, 32, 24, 28, 20, 26, 18, 22, 16, 24, 20, 14, 18, 12, 16, 10, 14]
const LOW = HIGH.map((y) => y + 14)

const WIDTH = 640
const STEP = WIDTH / VOLUME.length

const line = (values: number[]) =>
  values.map((y, i) => `${(i * STEP + STEP / 2).toFixed(1)},${y * 2}`).join(' ')

export function GhostPanel() {
  return (
    <div className="ghost" aria-hidden="true">
      <div className="ghost__bar">
        <span className="ghost__blob" style={{ width: '3.5rem' }} />
        <span className="ghost__blob" style={{ width: '8rem', marginRight: 'auto' }} />
        <span className="ghost__blob" style={{ width: '4.5rem' }} />
      </div>
      <svg className="ghost__svg" viewBox={`0 0 ${WIDTH} 190`} focusable="false">
        {[40, 90, 140].map((y) => (
          <line key={y} className="ghost__grid" x1="0" x2={WIDTH} y1={y} y2={y} />
        ))}
        <g className="ghost__bars">
          {VOLUME.map((height, i) => (
            <rect
              key={i}
              x={i * STEP + STEP * 0.2}
              y={190 - height}
              width={STEP * 0.6}
              height={height}
              rx="2"
            />
          ))}
        </g>
        <polyline className="ghost__line ghost__line--high" points={line(HIGH)} />
        <polyline className="ghost__line ghost__line--low" points={line(LOW)} />
      </svg>
    </div>
  )
}
