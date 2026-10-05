import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorBoundary } from './ErrorBoundary'

function Broken({ broken }: { broken: boolean }) {
  if (broken) throw new Error('chart failed to load')
  return <p>Chart is showing</p>
}

const fallback = (reset: () => void) => (
  <div role="alert">
    <p>This view could not be displayed.</p>
    <button onClick={reset}>Try again</button>
  </div>
)

beforeEach(() => {
  // React logs the caught error; keep the test output readable.
  vi.spyOn(console, 'error').mockImplementation(() => {})
})

afterEach(() => vi.restoreAllMocks())

describe('ErrorBoundary', () => {
  it('shows the fallback instead of taking the page down, and leaves siblings alone', () => {
    render(
      <>
        <p>Remove button still here</p>
        <ErrorBoundary fallback={fallback}>
          <Broken broken />
        </ErrorBoundary>
      </>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('This view could not be displayed.')
    expect(screen.getByText('Remove button still here')).toBeInTheDocument()
  })

  it('logs the failure for developers', () => {
    render(
      <ErrorBoundary fallback={fallback}>
        <Broken broken />
      </ErrorBoundary>,
    )
    expect(console.error).toHaveBeenCalledWith(
      'Render failure caught by ErrorBoundary',
      expect.objectContaining({ message: 'chart failed to load' }),
      expect.any(String),
    )
  })

  it('recovers when the failure is gone and the user chooses Try again', async () => {
    let broken = true
    const user = userEvent.setup()
    function Child() {
      return <Broken broken={broken} />
    }
    render(
      <ErrorBoundary fallback={fallback}>
        <Child />
      </ErrorBoundary>,
    )

    broken = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(screen.getByText('Chart is showing')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('clears itself when a reset key changes, such as switching to another view', () => {
    const { rerender } = render(
      <ErrorBoundary resetKeys={['chart']} fallback={fallback}>
        <Broken broken />
      </ErrorBoundary>,
    )
    expect(screen.getByRole('alert')).toBeInTheDocument()

    rerender(
      <ErrorBoundary resetKeys={['table']} fallback={fallback}>
        <Broken broken={false} />
      </ErrorBoundary>,
    )

    expect(screen.getByText('Chart is showing')).toBeInTheDocument()
  })

  it('stays on the fallback when the reset keys have not changed', () => {
    const { rerender } = render(
      <ErrorBoundary resetKeys={['chart']} fallback={fallback}>
        <Broken broken />
      </ErrorBoundary>,
    )

    rerender(
      <ErrorBoundary resetKeys={['chart']} fallback={fallback}>
        <Broken broken={false} />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('alert')).toBeInTheDocument()
  })
})
