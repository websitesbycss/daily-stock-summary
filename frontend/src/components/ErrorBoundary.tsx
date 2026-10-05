import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  /** The boundary clears itself when any of these change (for example, the selected view). */
  resetKeys?: readonly unknown[]
  fallback: (reset: () => void) => ReactNode
  children: ReactNode
}

interface State {
  failed: boolean
}

/** Keeps a render failure (including a lazy chunk that fails to load) from blanking the whole app. */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Render failure caught by ErrorBoundary', error, info.componentStack)
  }

  componentDidUpdate(previous: Props) {
    const changed =
      this.props.resetKeys?.length !== previous.resetKeys?.length ||
      this.props.resetKeys?.some((key, index) => !Object.is(key, previous.resetKeys?.[index]))
    if (this.state.failed && changed) this.reset()
  }

  reset = () => this.setState({ failed: false })

  render() {
    return this.state.failed ? this.props.fallback(this.reset) : this.props.children
  }
}
