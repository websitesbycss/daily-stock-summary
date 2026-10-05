/** Mirrors the backend Symbol value object: ASCII letters/digits and . - ^ =, 1-15 chars, at least one letter or digit. */
const SYMBOL_PATTERN = /^(?=.*[A-Za-z0-9])[A-Za-z0-9.\-^=]{1,15}$/

export function normalizeSymbol(input: string): string {
  return input.trim().toUpperCase()
}

export function isValidSymbol(input: string): boolean {
  return SYMBOL_PATTERN.test(input.trim())
}
