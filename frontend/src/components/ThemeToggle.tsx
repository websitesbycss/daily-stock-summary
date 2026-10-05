import type { ReactNode } from 'react'
import type { ThemeMode } from '../theme/theme'
import { MonitorIcon, MoonIcon, SunIcon } from './Icons'

const OPTIONS: { mode: ThemeMode; label: string; icon: ReactNode }[] = [
  { mode: 'light', label: 'Light theme', icon: <SunIcon /> },
  { mode: 'dark', label: 'Dark theme', icon: <MoonIcon /> },
  { mode: 'system', label: 'Match system theme', icon: <MonitorIcon /> },
]

interface Props {
  mode: ThemeMode
  onChange: (mode: ThemeMode) => void
}

export function ThemeToggle({ mode, onChange }: Props) {
  return (
    <div className="segmented theme-toggle" role="group" aria-label="Color theme">
      {OPTIONS.map((option) => (
        <button
          key={option.mode}
          type="button"
          className="segmented__option"
          aria-pressed={mode === option.mode}
          aria-label={option.label}
          title={option.label}
          onClick={() => onChange(option.mode)}
        >
          {option.icon}
        </button>
      ))}
    </div>
  )
}
