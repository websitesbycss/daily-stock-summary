import { useRef, useState, type FormEvent } from 'react'
import { ApiError } from '../api/errors'
import { isValidSymbol, normalizeSymbol } from '../lib/symbol'
import { describeApiError, notify } from '../notify'

interface Props {
  onSubmit: (symbol: string) => void
}

export function SymbolForm({ onSubmit }: Props) {
  const [value, setValue] = useState('')
  const [invalid, setInvalid] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!isValidSymbol(value)) {
      setInvalid(true)
      notify.error(describeApiError(new ApiError('invalid-symbol')), 'invalid-symbol')
      inputRef.current?.focus()
      return
    }
    setInvalid(false)
    setValue('')
    onSubmit(normalizeSymbol(value))
  }

  return (
    <form className="symbol-form" onSubmit={handleSubmit} noValidate>
      <label className="symbol-form__label" htmlFor="symbol">
        Stock symbol
      </label>
      <div className="symbol-form__row">
        <input
          ref={inputRef}
          id="symbol"
          className="symbol-form__input"
          value={value}
          onChange={(e) => {
            setValue(e.target.value.toUpperCase())
            setInvalid(false)
          }}
          placeholder="TSLA"
          maxLength={15}
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          aria-invalid={invalid}
        />
        <button className="button button--primary" type="submit">
          Add symbol
        </button>
      </div>
    </form>
  )
}
