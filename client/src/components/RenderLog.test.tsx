import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { RenderLog } from './RenderLog'
import type { RenderDto } from '../api/types'
import { api } from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    replay: vi.fn(),
    outputs: vi.fn().mockResolvedValue([]),
  },
}))

const job = (over: Partial<RenderDto> = {}): RenderDto => ({
  id: 'r1',
  assetId: 'a1',
  assetName: 'Bella — Character Rig',
  assetVersion: 2,
  output: 'final',
  jobData: { frame: 42 },
  status: 'completed',
  exitCode: 0,
  durationMs: 120_000,
  attempts: 1,
  error: null,
  isReplay: false,
  replayOfId: null,
  createdAt: new Date().toISOString(),
  ...over,
})

describe('RenderLog', () => {
  beforeEach(() => {
    vi.mocked(api.replay).mockReset()
  })

  it('renders status, pinned version and duration', () => {
    render(
      <RenderLog assetId="a1" outputs={[]} onReplayed={() => {}} renders={[job()]} />,
    )
    expect(screen.getByText('final')).toBeInTheDocument()
    expect(screen.getByText('v2')).toBeInTheDocument()
    expect(screen.getByText('2m 00s')).toBeInTheDocument()
  })

  it('flags retried renders', () => {
    render(
      <RenderLog assetId="a1" outputs={[]} onReplayed={() => {}} renders={[job({ attempts: 3 })]} />,
    )
    expect(screen.getByText(/3 attempts/)).toBeInTheDocument()
  })

  it('re-renders through the API', async () => {
    vi.mocked(api.replay).mockResolvedValue(job({ id: 'r2', isReplay: true, status: 'failed' }))
    render(
      <RenderLog assetId="a1" outputs={[]} onReplayed={() => {}} renders={[job()]} />,
    )
    fireEvent.click(screen.getByText('final')) // expand the row first
    fireEvent.click(screen.getByText('↻ Re-render on current snapshot'))
    await vi.waitFor(() => expect(api.replay).toHaveBeenCalledWith('r1'))
  })

  it('expands the job data on click', () => {
    render(
      <RenderLog assetId="a1" outputs={[]} onReplayed={() => {}} renders={[job()]} />,
    )
    expect(screen.queryByText(/"frame"/)).not.toBeInTheDocument()
    fireEvent.click(screen.getByText('final'))
    expect(screen.getByText(/frame/)).toBeInTheDocument()
  })
})
