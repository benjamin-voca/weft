import { describe, expect, it } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Toggle } from './ui'
import { duration, outputColor, relTime, shortPath } from '../lib/format'

describe('Toggle', () => {
  it('reflects and flips state through the switch role', () => {
    const onChange = vi.fn()
    const { rerender } = render(<Toggle on={false} onChange={onChange} label="Endpoint active" />)
    const sw = screen.getByRole('switch')
    expect(sw).toHaveAttribute('aria-checked', 'false')

    fireEvent.click(sw)
    expect(onChange).toHaveBeenCalledWith(true)

    rerender(<Toggle on={true} onChange={onChange} label="Endpoint active" />)
    expect(screen.getByRole('switch')).toHaveAttribute('aria-checked', 'true')
  })
})

describe('format helpers', () => {
  it('formats render durations in seconds and minutes', () => {
    expect(duration(45_000)).toBe('45s')
    expect(duration(132_000)).toBe('2m 12s')
    expect(duration(0)).toBe('—')
  })

  it('shortens blend paths to the last two segments', () => {
    expect(shortPath('//characters/bella/bella_rig.blend')).toBe('bella/bella_rig.blend')
  })

  it('colors output families deterministically', () => {
    expect(outputColor('final')).toBe(outputColor('final'))
    expect(outputColor('final')).not.toBe(outputColor('bake'))
  })

  it('renders relative time', () => {
    expect(relTime(new Date(Date.now() - 30_000).toISOString())).toBe('just now')
    expect(relTime(new Date(Date.now() - 3 * 60_000).toISOString())).toBe('3m ago')
  })
})
