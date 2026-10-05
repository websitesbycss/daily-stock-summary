import type { ReactNode } from 'react'

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  )
}

export const GripIcon = () => (
  <svg width="12" height="16" viewBox="0 0 12 16" fill="currentColor" aria-hidden="true">
    {[3, 8, 13].flatMap((y) =>
      [3, 9].map((x) => <circle key={`${x}-${y}`} cx={x} cy={y} r="1.2" />),
    )}
  </svg>
)

export const RefreshIcon = () => (
  <Icon>
    <path d="M13.5 8a5.5 5.5 0 1 1-1.6-3.9" />
    <path d="M13.5 2.5v3h-3" />
  </Icon>
)

export const ChevronLeftIcon = () => (
  <Icon>
    <path d="M10 3 5 8l5 5" />
  </Icon>
)

export const ChevronRightIcon = () => (
  <Icon>
    <path d="m6 3 5 5-5 5" />
  </Icon>
)

export const CloseIcon = () => (
  <Icon>
    <path d="m3.5 3.5 9 9M12.5 3.5l-9 9" />
  </Icon>
)
