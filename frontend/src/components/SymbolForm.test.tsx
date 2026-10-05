import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { notify } from '../notify'
import { SymbolForm } from './SymbolForm'

vi.mock('../notify', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../notify')>()
  return { ...actual, notify: { success: vi.fn(), info: vi.fn(), error: vi.fn() } }
})

beforeEach(() => vi.clearAllMocks())

describe('SymbolForm', () => {
  it('submits a normalized symbol when Enter is pressed', async () => {
    const onSubmit = vi.fn()
    const user = userEvent.setup()
    render(<SymbolForm onSubmit={onSubmit} />)

    await user.type(screen.getByLabelText('Stock symbol'), '  btc-usd{Enter}')

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith('BTC-USD')
    expect(notify.error).not.toHaveBeenCalled()
  })

  it('shows an error toast and sends nothing for an invalid symbol, then clears the error on typing', async () => {
    const onSubmit = vi.fn()
    const user = userEvent.setup()
    render(<SymbolForm onSubmit={onSubmit} />)
    const field = screen.getByLabelText('Stock symbol')

    await user.type(field, '!!!{Enter}')

    expect(onSubmit).not.toHaveBeenCalled()
    expect(notify.error).toHaveBeenCalledWith(
      'Enter a valid symbol: letters, numbers, and . - ^ = (up to 15 characters).',
      'invalid-symbol',
    )
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(field).toHaveFocus()

    await user.type(field, 'A')

    expect(field).toHaveAttribute('aria-invalid', 'false')
  })

  it('treats an empty submission as invalid and returns focus to the field', async () => {
    const onSubmit = vi.fn()
    const user = userEvent.setup()
    render(<SymbolForm onSubmit={onSubmit} />)

    await user.click(screen.getByRole('button', { name: 'Add symbol' }))

    expect(onSubmit).not.toHaveBeenCalled()
    expect(notify.error).toHaveBeenCalledOnce()
    expect(screen.getByLabelText('Stock symbol')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Stock symbol')).toHaveFocus()
  })

  it('stops accepting characters after 15', async () => {
    const user = userEvent.setup()
    render(<SymbolForm onSubmit={vi.fn()} />)

    await user.type(screen.getByLabelText('Stock symbol'), 'ABCDEFGHIJKLMNOPQRST')

    expect(screen.getByLabelText('Stock symbol')).toHaveValue('ABCDEFGHIJKLMNO')
  })

  it('clears the field after a valid submit so the next symbol can be typed', async () => {
    const user = userEvent.setup()
    render(<SymbolForm onSubmit={vi.fn()} />)

    await user.type(screen.getByLabelText('Stock symbol'), 'aapl{Enter}')

    expect(screen.getByLabelText('Stock symbol')).toHaveValue('')
  })
})
