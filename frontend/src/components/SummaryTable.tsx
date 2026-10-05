import { useMemo, useState } from 'react'
import type { DailySummary } from '../api/types'
import { formatDay } from '../lib/date'
import { formatPrice, formatVolume } from '../lib/format'

type SortKey = keyof DailySummary
type SortDirection = 'asc' | 'desc'

const COLUMNS: { key: SortKey; label: string; numeric: boolean }[] = [
  { key: 'day', label: 'Day', numeric: false },
  { key: 'lowAverage', label: 'Low average', numeric: true },
  { key: 'highAverage', label: 'High average', numeric: true },
  { key: 'volume', label: 'Volume', numeric: true },
]

interface Props {
  data: DailySummary[]
  /** Accessible name; panels show several tables, so each needs its own. */
  label: string
}

/** Sorted newest day first by default. Days are compared as yyyy-MM-dd strings, which sort chronologically. */
export function SummaryTable({ data, label }: Props) {
  const [sort, setSort] = useState<{ key: SortKey; direction: SortDirection }>({
    key: 'day',
    direction: 'desc',
  })

  const rows = useMemo(() => {
    const factor = sort.direction === 'asc' ? 1 : -1
    return [...data].sort((a, b) => {
      const x = a[sort.key]
      const y = b[sort.key]
      return (x < y ? -1 : x > y ? 1 : 0) * factor
    })
  }, [data, sort])

  function toggle(key: SortKey) {
    setSort((current) =>
      current.key === key
        ? { key, direction: current.direction === 'desc' ? 'asc' : 'desc' }
        : { key, direction: 'desc' },
    )
  }

  return (
    <div className="table-scroll">
      <table className="summary-table" aria-label={label}>
        <thead>
          <tr>
            {COLUMNS.map((column) => {
              const active = sort.key === column.key
              return (
                <th
                  key={column.key}
                  scope="col"
                  className={column.numeric ? 'num' : undefined}
                  aria-sort={
                    active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'
                  }
                >
                  <button type="button" className="sort-button" onClick={() => toggle(column.key)}>
                    {column.label}
                    <span className="sort-button__mark" aria-hidden="true">
                      {active ? (sort.direction === 'asc' ? '↑' : '↓') : ''}
                    </span>
                  </button>
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.day}>
              <th scope="row">{formatDay(row.day)}</th>
              <td className="num">{formatPrice(row.lowAverage)}</td>
              <td className="num">{formatPrice(row.highAverage)}</td>
              <td className="num">{formatVolume(row.volume)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
