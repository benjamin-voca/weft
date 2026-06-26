import type {
  AssetConfig, AssetDetail, AssetSummary, CommitDto, DiffDto, OutputGroup,
  ProjectStats, ProjectSummary, RenderDto,
} from './types'

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message)
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const body = await res.json()
      if (body?.error) message = body.error
    } catch { /* non-JSON error body */ }
    throw new ApiError(message, res.status)
  }
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export interface AuthUser {
  email: string
  displayName: string
  role: string
}

export const api = {
  me: () => request<AuthUser>('/api/auth/me'),

  login: (email: string, password: string) =>
    request<AuthUser>('/api/auth/login', {
      method: 'POST', body: JSON.stringify({ email, password }),
    }),

  logout: () => request<{ ok: boolean }>('/api/auth/logout', { method: 'POST' }),

  projects: () => request<ProjectSummary[]>('/api/projects'),

  project: (id: string) => request<ProjectSummary>(`/api/projects/${id}`),

  createProject: (name: string, description: string) =>
    request<ProjectSummary>('/api/projects', { method: 'POST', body: JSON.stringify({ name, description }) }),

  assets: (projectId: string) => request<AssetSummary[]>(`/api/projects/${projectId}/assets`),

  createAsset: (projectId: string, name: string, config: AssetConfig) =>
    request<AssetDetail>(`/api/projects/${projectId}/assets`, {
      method: 'POST', body: JSON.stringify({ name, config }),
    }),

  asset: (id: string) => request<AssetDetail>(`/api/assets/${id}`),

  saveConfig: (id: string, config: AssetConfig) =>
    request<AssetDetail>(`/api/assets/${id}/config`, { method: 'PUT', body: JSON.stringify({ config }) }),

  discard: (id: string) =>
    request<AssetDetail>(`/api/assets/${id}/discard`, { method: 'POST' }),

  commit: (id: string, message: string | null) =>
    request<CommitDto>(`/api/assets/${id}/commit`, { method: 'POST', body: JSON.stringify({ message }) }),

  commits: (id: string) => request<CommitDto[]>(`/api/assets/${id}/commits`),

  diff: (id: string, from?: number | 'head', to?: number | 'draft') => {
    const params = new URLSearchParams()
    if (from !== undefined && from !== 'head') params.set('from', String(from))
    if (to !== undefined && to !== 'draft') params.set('to', String(to))
    const qs = params.toString()
    return request<DiffDto>(`/api/assets/${id}/diff${qs ? `?${qs}` : ''}`)
  },

  rollback: (id: string, version: number, message: string | null) =>
    request<CommitDto>(`/api/assets/${id}/rollback`, {
      method: 'POST', body: JSON.stringify({ version, message }),
    }),

  deleteAsset: (id: string) => request<void>(`/api/assets/${id}`, { method: 'DELETE' }),

  renders: (assetId: string, limit = 60) =>
    request<RenderDto[]>(`/api/assets/${assetId}/renders?limit=${limit}`),

  queueRender: (assetId: string, output: string, jobData: unknown) =>
    request<RenderDto>(`/api/assets/${assetId}/renders`, {
      method: 'POST', body: JSON.stringify({ output, jobData }),
    }),

  replay: (renderId: string) =>
    request<RenderDto>(`/api/renders/${renderId}/replay`, { method: 'POST' }),

  stats: (projectId: string) => request<ProjectStats>(`/api/projects/${projectId}/stats`),

  outputs: () => request<OutputGroup[]>('/api/outputs'),
}
