export type View = 'chart' | 'table'

export type Breakpoint = 'xl' | 'lg' | 'md' | 'sm'

/** One panel's position in the grid, in grid units. `i` is the panel's symbol. */
export interface GridItem {
  i: string
  x: number
  y: number
  w: number
  h: number
}

export type Layouts = Partial<Record<Breakpoint, readonly GridItem[]>>

export interface PanelState {
  /** Normalized (upper-case) symbol; unique across the dashboard. */
  symbol: string
  view: View
}

export interface DashboardState {
  panels: PanelState[]
  layouts: Layouts
}
