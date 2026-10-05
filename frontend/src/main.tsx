import '@fontsource-variable/newsreader'
import '@fontsource-variable/hanken-grotesk'
import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { ErrorBoundary } from './components/ErrorBoundary.tsx'
import { Notifications } from './components/Notifications.tsx'
import { createQueryClient } from './queries/queryClient.ts'

const queryClient = createQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary
      fallback={() => (
        <main className="page">
          <p className="empty" role="alert">
            Something went wrong. Reload the page to continue.
          </p>
        </main>
      )}
    >
      <QueryClientProvider client={queryClient}>
        <App />
        <Notifications />
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
)
