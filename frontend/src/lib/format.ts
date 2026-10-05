const price = new Intl.NumberFormat('en-US', { minimumFractionDigits: 4, maximumFractionDigits: 4 })
const grouped = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 })
const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 })

/** Prices always show 4 decimals, matching the API precision. */
export const formatPrice = (value: number): string => price.format(value)

export const formatVolume = (value: number): string => grouped.format(value)

/** Short form for chart axes ("49.1M"). */
export const formatVolumeCompact = (value: number): string => compact.format(value)
