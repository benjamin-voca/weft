import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { CommitTimeline } from './CommitTimeline'
import type { CommitDto } from '../api/types'

const commit = (over: Partial<CommitDto>): CommitDto => ({
  id: over.version!.toString(),
  version: over.version!,
  hash: 'abc12345',
  message: 'did something',
  author: 'klajdi',
  changes: [],
  isRollback: false,
  restoredVersion: null,
  createdAt: new Date().toISOString(),
  ...over,
})

const commits: CommitDto[] = [
  commit({ version: 3, message: 'Pause endpoint' }),
  commit({ version: 2, message: 'Subscribe to alert.threshold', isRollback: false }),
  commit({ version: 1, message: 'Initial commit' }),
]

describe('CommitTimeline', () => {
  it('renders every version with HEAD on the newest', () => {
    render(<CommitTimeline assetId="a1" commits={commits} />)
    expect(screen.getByText('HEAD')).toBeInTheDocument()
    expect(screen.getByText('Pause endpoint')).toBeInTheDocument()
    expect(screen.getByText('Initial commit')).toBeInTheDocument()
    expect(screen.getAllByText(/v\d/).length).toBeGreaterThan(0)
  })

  it('flags rollback commits with the restored version', () => {
    const rolled = commits.map((c) =>
      c.version === 2 ? { ...c, isRollback: true, restoredVersion: 1 } : c,
    )
    render(<CommitTimeline assetId="a1" commits={rolled} />)
    expect(screen.getByText(/revert of v1/)).toBeInTheDocument()
  })

  it('offers restore only for non-HEAD commits', () => {
    render(<CommitTimeline assetId="a1" commits={commits} onRestore={() => {}} />)
    expect(screen.queryByText('↺ Restore v3')).not.toBeInTheDocument()
    expect(screen.getByText('↺ Restore v1')).toBeInTheDocument()
  })

  it('does not offer "Compare to HEAD" on the HEAD row', () => {
    render(<CommitTimeline assetId="a1" commits={[commits[0]]} />)
    expect(screen.getByText('HEAD')).toBeInTheDocument()
    expect(screen.queryByText('Compare to HEAD')).not.toBeInTheDocument()
    expect(screen.queryByText('Diff vs v')).not.toBeInTheDocument()
  })
})
