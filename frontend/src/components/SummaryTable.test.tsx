import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { DailySummary } from '../api/types'
import { SummaryTable } from './SummaryTable'

// Oldest first, as the API returns it.
const data: DailySummary[] = [
  { day: '2026-01-29', lowAverage: 40.2958, highAverage: 49.7534, volume: 49073348 },
  { day: '2026-01-30', lowAverage: 41.5, highAverage: 48, volume: 1200 },
  { day: '2026-02-02', lowAverage: 39, highAverage: 52.25, volume: 9000000 },
]

function bodyDays() {
  const rows = screen.getAllByRole('row').slice(1)
  return rows.map((row) => within(row).getAllByRole('rowheader')[0].textContent)
}

describe('SummaryTable', () => {
  it('lists the newest day first by default', () => {
    render(<SummaryTable data={data} label="TSLA daily summary" />)
    expect(bodyDays()).toEqual(['Feb 2, 2026', 'Jan 30, 2026', 'Jan 29, 2026'])
    expect(screen.getByRole('columnheader', { name: /Day/ })).toHaveAttribute(
      'aria-sort',
      'descending',
    )
  })

  it('formats prices to 4 decimals and groups volume', () => {
    render(<SummaryTable data={data} label="TSLA daily summary" />)
    const row = screen.getAllByRole('row')[3]
    expect(within(row).getByText('40.2958')).toBeInTheDocument()
    expect(within(row).getByText('49.7534')).toBeInTheDocument()
    expect(within(row).getByText('49,073,348')).toBeInTheDocument()
    expect(screen.getByText('48.0000')).toBeInTheDocument()
  })

  it('sorts by a column, then reverses on a second click', async () => {
    const user = userEvent.setup()
    render(<SummaryTable data={data} label="TSLA daily summary" />)

    await user.click(screen.getByRole('button', { name: /Volume/ }))
    expect(bodyDays()).toEqual(['Jan 29, 2026', 'Feb 2, 2026', 'Jan 30, 2026'])

    await user.click(screen.getByRole('button', { name: /Volume/ }))
    expect(bodyDays()).toEqual(['Jan 30, 2026', 'Feb 2, 2026', 'Jan 29, 2026'])
    expect(screen.getByRole('columnheader', { name: /Volume/ })).toHaveAttribute(
      'aria-sort',
      'ascending',
    )
  })

  it('does not mutate the data it was given', () => {
    const copy = structuredClone(data)
    render(<SummaryTable data={copy} label="TSLA daily summary" />)
    expect(copy).toEqual(data)
  })

  it('reverses the day order when the active Day header is clicked again', async () => {
    const user = userEvent.setup()
    render(<SummaryTable data={data} label="TSLA daily summary" />)

    await user.click(screen.getByRole('button', { name: /Day/ }))

    expect(bodyDays()).toEqual(['Jan 29, 2026', 'Jan 30, 2026', 'Feb 2, 2026'])
    expect(screen.getByRole('columnheader', { name: /Day/ })).toHaveAttribute(
      'aria-sort',
      'ascending',
    )
  })

  it('sorts by low average, highest first, and marks only that column as sorted', async () => {
    const user = userEvent.setup()
    render(<SummaryTable data={data} label="TSLA daily summary" />)

    await user.click(screen.getByRole('button', { name: /Low average/ }))

    // 41.5 (Jan 30), 40.2958 (Jan 29), 39 (Feb 2)
    expect(bodyDays()).toEqual(['Jan 30, 2026', 'Jan 29, 2026', 'Feb 2, 2026'])
    expect(screen.getByRole('columnheader', { name: /Low average/ })).toHaveAttribute(
      'aria-sort',
      'descending',
    )
    for (const other of [/Day/, /High average/, /Volume/]) {
      expect(screen.getByRole('columnheader', { name: other })).toHaveAttribute('aria-sort', 'none')
    }
  })

  it('sorts by high average, highest first', async () => {
    const user = userEvent.setup()
    render(<SummaryTable data={data} label="TSLA daily summary" />)

    await user.click(screen.getByRole('button', { name: /High average/ }))

    // 52.25 (Feb 2), 49.7534 (Jan 29), 48 (Jan 30)
    expect(bodyDays()).toEqual(['Feb 2, 2026', 'Jan 29, 2026', 'Jan 30, 2026'])
  })
})
