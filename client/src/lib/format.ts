export function relTime(iso: string): string {
  const then = new Date(iso).getTime()
  const secs = Math.max(0, Math.floor((Date.now() - then) / 1000))
  if (secs < 60) return 'just now'
  const mins = Math.floor(secs / 60)
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 14) return `${days}d ago`
  return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

export function absTime(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit',
  })
}

/** Render durations: seconds under a minute, "Xm Ys" above. */
export function duration(n: number): string {
  if (n <= 0) return '—'
  const secs = Math.round(n / 1000)
  if (secs < 60) return `${secs}s`
  return `${Math.floor(secs / 60)}m ${String(secs % 60).padStart(2, '0')}s`
}

const OUTPUT_HUES = ['#3ad9c5', '#7dd3fc', '#a78bfa', '#fbbf24', '#f472b6', '#4ade80']

/** Deterministic color per output family (stable across renders). */
export function outputColor(output: string): string {
  const family = output.split(/[-_.]/)[0] ?? output
  let h = 0
  for (let i = 0; i < family.length; i++) h = (h * 31 + family.charCodeAt(i)) >>> 0
  return OUTPUT_HUES[h % OUTPUT_HUES.length]
}

/** "//pool/characters/bella/file.blend" → "…/bella/file.blend" */
export function shortPath(path: string): string {
  const parts = path.split('/').filter(Boolean)
  if (parts.length <= 2) return parts.join('/')
  return parts.slice(-2).join('/')
}

export function prettyJson(value: unknown): string {
  return JSON.stringify(value, null, 2)
}

export const kindLabel: Record<string, string> = {
  completed: 'completed',
  failed: 'failed',
  blocked: 'blocked',
}
