import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { DiffView } from './DiffView'
import type { ChangeEntry } from '../api/types'

const changes: ChangeEntry[] = [
  { path: 'targetUrl', kind: 'modified', from: 'https://old.example.com/h', to: 'https://new.example.com/h' },
  { path: 'events', kind: 'added', from: null, to: 'payout.paid' },
  { path: 'headers.X-Old', kind: 'removed', from: 'gone', to: null },
]

describe('DiffView', () => {
  it('renders added, modified and removed entries with kind badges', () => {
    render(<DiffView changes={changes} />)
    expect(screen.getByText('targetUrl')).toBeInTheDocument()
    expect(screen.getByText('https://new.example.com/h')).toBeInTheDocument()
    expect(screen.getByText('added')).toBeInTheDocument()
    expect(screen.getByText('removed')).toBeInTheDocument()
    expect(screen.getByText('modified')).toBeInTheDocument()
  })

  it('strikethroughs the previous value of a modification', () => {
    render(<DiffView changes={[changes[0]]} />)
    const old = screen.getByText('https://old.example.com/h')
    expect(old.className).toContain('line-through')
  })

  it('masks secret values even in diffs', () => {
    render(<DiffView changes={[{ path: 'secretRef', kind: 'modified', from: '••••1234', to: '••••5678' }]} />)
    expect(screen.getByText('••••5678')).toBeInTheDocument()
    expect(screen.queryByText('whsec_new')).not.toBeInTheDocument()
  })

  it('explains when there are no differences', () => {
    render(<DiffView changes={[]} />)
    expect(screen.getByText('No differences.')).toBeInTheDocument()
  })
})
