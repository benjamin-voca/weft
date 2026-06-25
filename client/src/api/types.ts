/** Mirrors of the Weft.Api DTOs (camelCase over the wire). */

export interface AssetConfig {
  sourceFile: string
  description: string
  blenderVersion: string
  engine: string
  resX: number
  resY: number
  render: { samples: number; denoise: boolean }
  outputs: string[]
  materials: Record<string, string>
  nodeToken: string
  outputPattern: string
  isActive: boolean
}

export interface ChangeEntry {
  path: string
  kind: 'added' | 'removed' | 'modified'
  from: string | null
  to: string | null
}

export interface ProjectSummary {
  id: string
  name: string
  description: string
  assetCount: number
  createdAt: string
}

export interface AssetSummary {
  id: string
  name: string
  sourceFile: string
  outputs: string[]
  isActive: boolean
  version: number
  hash: string
  dirty: boolean
  renderCount: number
  successRate: number
  lastRenderAt: string | null
  updatedAt: string
}

export interface AssetDetail {
  id: string
  projectId: string
  projectName: string
  name: string
  workingConfig: AssetConfig
  headVersion: number
  headHash: string
  dirty: boolean
  draftDiff: ChangeEntry[]
  createdAt: string
  updatedAt: string
}

export interface CommitDto {
  id: string
  version: number
  hash: string
  message: string
  author: string
  changes: ChangeEntry[]
  isRollback: boolean
  restoredVersion: number | null
  createdAt: string
}

export interface DiffEndpoint {
  version: number
  hash: string
  label: string
}

export interface DiffDto {
  from: DiffEndpoint
  to: DiffEndpoint
  changes: ChangeEntry[]
}

export interface RenderDto {
  id: string
  assetId: string
  assetName: string
  assetVersion: number
  output: string
  jobData: unknown
  status: 'completed' | 'failed' | 'blocked'
  exitCode: number | null
  durationMs: number
  attempts: number
  error: string | null
  isReplay: boolean
  replayOfId: string | null
  createdAt: string
}

export interface Totals {
  assets: number
  active: number
  renders: number
  completed: number
  failed: number
  blocked: number
  successRate: number
}

export interface ProjectStats {
  totals: Totals
  latency: { avgMs: number; p95Ms: number }
  series: { date: string; completed: number; failed: number; blocked: number }[]
  assets: AssetHealth[]
  recent: RenderDto[]
}

export interface AssetHealth {
  id: string
  name: string
  isActive: boolean
  version: number
  dirty: boolean
  renders: number
  successRate: number
  avgMs: number
  lastRenderAt: string | null
}

export interface OutputGroup {
  group: string
  outputs: string[]
}
