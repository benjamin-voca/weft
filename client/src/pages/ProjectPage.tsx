import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import type { AssetConfig, OutputGroup } from '../api/types'
import { api } from '../api/client'
import { useApi } from '../lib/useApi'
import { Button, EmptyState, Field, Input, Modal, Spinner, StatusDot } from '../components/ui'
import { StackedBars } from '../components/charts'
import { OutputChip } from '../components/RenderLog'
import { duration, relTime, shortPath } from '../lib/format'

export function ProjectPage() {
  const { projectId = '' } = useParams()
  const project = useApi(() => api.project(projectId), [projectId])
  const stats = useApi(() => api.stats(projectId), [projectId])
  const assets = useApi(() => api.assets(projectId), [projectId])
  const catalog = useApi(() => api.outputs(), [])
  const [createOpen, setCreateOpen] = useState(false)

  const s = stats.data

  return (
    <div className="mx-auto max-w-5xl px-8 py-10" data-testid="project-page">
      <motion.header
        initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }}
        className="mb-6 flex items-start justify-between gap-4"
      >
        <div>
          <p className="font-mono text-[11px] uppercase tracking-[0.2em] text-faint">production</p>
          <h1 className="mt-1 text-2xl font-semibold tracking-tight">{project.data?.name ?? '…'}</h1>
          <p className="mt-1 text-sm text-dim">{project.data?.description}</p>
        </div>
        <Button variant="primary" onClick={() => setCreateOpen(true)} data-testid="new-asset">
          + New asset
        </Button>
      </motion.header>

      {s && (
        <>
          <div className="mb-5 grid grid-cols-2 gap-3 md:grid-cols-5">
            <Stat label="Render success" value={`${s.totals.successRate.toFixed(1)}%`}
              tone={s.totals.successRate >= 90 ? 'good' : s.totals.successRate >= 70 ? 'warn' : 'bad'} testId="stat-success" />
            <Stat label="Renders (14d)" value={s.totals.renders.toLocaleString()} testId="stat-renders" />
            <Stat label="Avg render" value={duration(s.latency.avgMs)} />
            <Stat label="p95 render" value={duration(s.latency.p95Ms)} />
            <Stat label="Assets" value={`${s.totals.active}/${s.totals.assets} active`} />
          </div>

          <div className="card mb-8 p-5">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-sm font-semibold">Render traffic</h2>
              <span className="text-[11px] text-faint">last 14 days</span>
            </div>
            <StackedBars points={s.series} />
          </div>
        </>
      )}

      <h2 className="mb-3 text-sm font-semibold text-dim">Assets</h2>
      {assets.loading && !assets.data && (
        <div className="flex items-center gap-3 py-10 text-sm text-faint"><Spinner /> Loading assets…</div>
      )}
      {assets.data && assets.data.length === 0 && (
        <EmptyState
          title="No assets yet"
          hint="Assets hold your .blend states under version control — every change becomes a snapshot."
          action={<Button variant="primary" onClick={() => setCreateOpen(true)}>Create the first asset</Button>}
        />
      )}
      <ul className="flex flex-col gap-2" data-testid="asset-list">
        {assets.data?.map((a, i) => (
          <motion.li
            key={a.id}
            initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.25, delay: Math.min(i * 0.05, 0.3) }}
          >
            <Link
              to={`/app/p/${projectId}/a/${a.id}`}
              className="card block px-4 py-3.5 transition-all duration-150 hover:border-[#33415c]"
              data-testid={`asset-row-${a.name}`}
            >
              <div className="flex flex-wrap items-center gap-2.5">
                <StatusDot tone={!a.isActive ? 'faint' : a.successRate >= 90 ? 'good' : a.successRate >= 70 ? 'warn' : 'bad'} />
                <span className="text-sm font-semibold">{a.name}</span>
                <code className="font-mono text-[11px] text-faint">{shortPath(a.sourceFile ?? '')}</code>
                <span className="ml-auto flex items-center gap-2">
                  {a.dirty && (
                    <span title="Uncommitted draft changes" className="flex items-center gap-1 rounded-md border border-warn/30 bg-warn/10 px-1.5 py-0.5 text-[11px] text-warn">
                      <span className="h-1.5 w-1.5 rounded-full bg-warn" /> draft
                    </span>
                  )}
                  <span className="rounded-md border border-line bg-panel-2 px-1.5 py-0.5 font-mono text-[11px] text-dim">
                    v{a.version} · {a.hash}
                  </span>
                </span>
              </div>
              {a.outputs.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {a.outputs.map((o) => <OutputChip key={o} output={o} />)}
                </div>
              )}
              <div className="mt-2 flex flex-wrap items-center gap-4 text-[11px] text-faint">
                <span>{a.renderCount.toLocaleString()} renders</span>
                <span className={a.successRate >= 90 ? 'text-good/80' : a.successRate >= 70 ? 'text-warn' : 'text-bad'}>
                  {a.successRate.toFixed(0)}% completed
                </span>
                {a.lastRenderAt && <span>last render {relTime(a.lastRenderAt)}</span>}
                <span>updated {relTime(a.updatedAt)}</span>
              </div>
            </Link>
          </motion.li>
        ))}
      </ul>

      <CreateAssetDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        projectId={projectId}
        outputs={catalog.data ?? []}
        onCreated={async () => {
          assets.refresh()
          stats.refresh()
          project.refresh()
          setCreateOpen(false)
        }}
      />
    </div>
  )
}

function Stat({ label, value, tone, testId }: {
  label: string
  value: string
  tone?: 'good' | 'warn' | 'bad'
  testId?: string
}) {
  const color = tone === 'good' ? 'text-good' : tone === 'warn' ? 'text-warn' : tone === 'bad' ? 'text-bad' : 'text-text'
  return (
    <div className="card px-4 py-3" data-testid={testId}>
      <p className="text-[11px] text-faint">{label}</p>
      <p className={`mt-1 font-mono text-lg font-semibold ${color}`}>{value}</p>
    </div>
  )
}

function CreateAssetDialog({ open, onClose, projectId, outputs, onCreated }: {
  open: boolean
  onClose: () => void
  projectId: string
  outputs: OutputGroup[]
  onCreated: () => void | Promise<void>
}) {
  const [name, setName] = useState('')
  const [source, setSource] = useState('')
  const [description, setDescription] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const create = async () => {
    setBusy(true)
    setError(null)
    try {
      const config: AssetConfig = {
        sourceFile: source,
        description,
        blenderVersion: '4.2.3',
        engine: 'CYCLES',
        resX: 1920,
        resY: 1080,
        render: { samples: 128, denoise: true },
        outputs: selected,
        materials: {},
        nodeToken: '',
        outputPattern: '',
        isActive: true,
      }
      await api.createAsset(projectId, name, config)
      setName(''); setSource(''); setDescription(''); setSelected([])
      await onCreated()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="New asset under version control" width={560}>
      <div className="flex flex-col gap-4">
        <div className="grid grid-cols-2 gap-4">
          <Field label="Name">
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Bella — Character Rig" data-testid="create-name" />
          </Field>
          <Field label="Source file">
            <Input
              value={source}
              onChange={(e) => setSource(e.target.value)}
              placeholder="//assets/…/file.blend"
              data-testid="create-source"
            />
          </Field>
        </div>
        <Field label="Description">
          <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What it is for" />
        </Field>
        <div>
          <p className="mb-2 text-xs font-medium text-dim">Enabled render outputs</p>
          <div className="flex max-h-40 flex-wrap gap-1.5 overflow-y-auto rounded-xl border border-line bg-panel-2/40 p-3">
            {outputs.map((g) => g.outputs).flat().map((output) => {
              const on = selected.includes(output)
              return (
                <button
                  key={output}
                  type="button"
                  aria-pressed={on}
                  onClick={() => setSelected(on ? selected.filter((e) => e !== output) : [...selected, output])}
                  className={`rounded-md border px-1.5 py-0.5 font-mono text-[11px] transition-colors ${
                    on ? 'border-accent/40 bg-accent/10 text-accent' : 'border-line text-faint hover:text-dim'
                  }`}
                >
                  {output}
                </button>
              )
            })}
          </div>
        </div>
        {error && <p className="text-xs text-bad">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="subtle" onClick={onClose}>Cancel</Button>
          <Button
            variant="primary"
            disabled={busy || !name.trim() || !source.trim()}
            onClick={() => void create()}
            data-testid="create-submit"
          >
            {busy ? 'Creating…' : 'Create asset (v1)'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
