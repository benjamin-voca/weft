import { useMemo, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import type { OutputGroup, RenderDto } from '../api/types'
import { api } from '../api/client'
import { Badge, Button, Modal, useToast } from './ui'
import { duration, outputColor, prettyJson, relTime, absTime } from '../lib/format'

export function RenderLog({ assetId, renders, outputs, onReplayed }: {
  assetId: string
  renders: RenderDto[]
  outputs: OutputGroup[]
  onReplayed: () => void
}) {
  const toast = useToast()
  const [expanded, setExpanded] = useState<string | null>(null)
  const [queueOpen, setQueueOpen] = useState(false)
  const [busyId, setBusyId] = useState<string | null>(null)

  const replay = async (r: RenderDto) => {
    setBusyId(r.id)
    try {
      const res = await api.replay(r.id)
      toast(`Re-rendered ${r.output} on current snapshot → ${res.status}`, res.status === 'completed' ? 'ok' : 'err')
      onReplayed()
    } catch (e) {
      toast((e as Error).message, 'err')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <p className="text-xs text-faint">
          Every render is pinned to the snapshot version it used.
        </p>
        <Button variant="ghost" onClick={() => setQueueOpen(true)} data-testid="queue-render">
          ⚡ Queue render
        </Button>
      </div>

      {renders.length === 0 ? (
        <div className="rounded-xl border border-dashed border-line py-12 text-center text-xs text-faint">
          No renders yet — queue one to see the pipeline in action.
        </div>
      ) : (
        <ul className="flex flex-col gap-1.5" data-testid="render-log">
          {renders.map((r, i) => (
            <motion.li
              key={r.id}
              initial={{ opacity: 0, y: 6 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.2, delay: Math.min(i * 0.015, 0.3) }}
            >
              <div
                role="button"
                tabIndex={0}
                onClick={() => setExpanded(expanded === r.id ? null : r.id)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') setExpanded(expanded === r.id ? null : r.id)
                }}
                className="card cursor-pointer px-3.5 py-2.5 transition-colors hover:border-[#33415c]"
                data-testid={`render-row-${r.output}-${r.status}`}
              >
                <div className="flex items-center gap-3">
                  <StatusIcon status={r.status} />
                  <OutputChip output={r.output} />
                  <Badge color="dim" title="Config version that handled this render">
                    v{r.assetVersion}
                  </Badge>
                  {r.attempts > 1 && (
                    <Badge color="warn" title="Retried before settling">↻ {r.attempts} attempts</Badge>
                  )}
                  {r.isReplay && <Badge color="iris">re-render</Badge>}
                  <span className="ml-auto flex items-center gap-3 text-[11px] text-faint">
                    {r.error && (
                      <span
                        className={`max-w-[280px] truncate ${r.status === 'blocked' ? 'text-warn/80' : 'text-bad/80'}`}
                        title={r.error}
                      >
                        {r.error}
                      </span>
                    )}
                    {r.durationMs > 0 && <span className="font-mono">{duration(r.durationMs)}</span>}
                    {r.exitCode !== null && <span className="font-mono">exit {r.exitCode}</span>}
                    <span title={absTime(r.createdAt)}>{relTime(r.createdAt)}</span>
                  </span>
                </div>

                <AnimatePresence>
                  {expanded === r.id && (
                    <motion.div
                      initial={{ height: 0, opacity: 0 }}
                      animate={{ height: 'auto', opacity: 1 }}
                      exit={{ height: 0, opacity: 0 }}
                      transition={{ duration: 0.2, ease: 'easeOut' }}
                      className="overflow-hidden"
                    >
                      <div className="mt-3 flex items-start gap-3 border-t border-line-soft pt-3">
                        <pre className="max-h-52 w-full overflow-auto rounded-lg border border-line-soft bg-bg p-3 font-mono text-[11px] leading-relaxed text-dim">
                          {prettyJson(r.jobData)}
                        </pre>
                        <Button
                          variant="ghost"
                          className="h-7 shrink-0 text-xs"
                          disabled={busyId === r.id}
                          onClick={(e) => {
                            e.stopPropagation()
                            void replay(r)
                          }}
                        >
                          ↻ Re-render on current snapshot
                        </Button>
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            </motion.li>
          ))}
        </ul>
      )}

      <QueueRenderDialog
        open={queueOpen}
        onClose={() => setQueueOpen(false)}
        assetId={assetId}
        outputs={outputs}
        onDone={(status, output) => {
          toast(`Queued ${output} render → ${status}`, status === 'completed' ? 'ok' : 'err')
          onReplayed()
        }}
      />
    </div>
  )
}

function StatusIcon({ status }: { status: RenderDto['status'] }) {
  const map = {
    completed: { cls: 'bg-good/15 text-good', glyph: '✓', title: 'completed' },
    failed: { cls: 'bg-bad/15 text-bad', glyph: '✕', title: 'failed' },
    blocked: { cls: 'bg-warn/15 text-warn', glyph: '⊘', title: 'blocked (locked or disabled output)' },
  }[status]
  return (
    <span
      title={map.title}
      className={`grid h-6 w-6 shrink-0 place-items-center rounded-full text-[11px] font-bold ${map.cls}`}
    >
      {map.glyph}
    </span>
  )
}

export function OutputChip({ output }: { output: string }) {
  const color = outputColor(output)
  return (
    <span
      className="inline-flex items-center rounded-md border px-1.5 py-0.5 font-mono text-[11px] leading-4"
      style={{ color, borderColor: `${color}33`, background: `${color}0d` }}
    >
      {output}
    </span>
  )
}

function QueueRenderDialog({ open, onClose, assetId, outputs, onDone }: {
  open: boolean
  onClose: () => void
  assetId: string
  outputs: OutputGroup[]
  onDone: (status: string, output: string) => void
}) {
  const allOutputs = useMemo(() => outputs.flatMap((g) => g.outputs), [outputs])
  const [output, setOutput] = useState(allOutputs[0] ?? 'final')
  const [jobData, setJobData] = useState('{\n  "frame": 42,\n  "camera": "closeup",\n  "pass": "beauty"\n}')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = async () => {
    setBusy(true)
    setError(null)
    try {
      const parsed = JSON.parse(jobData) as unknown
      const res = await api.queueRender(assetId, output, parsed)
      onDone(res.status, output)
      onClose()
    } catch (e) {
      setError(e instanceof SyntaxError ? 'Job data is not valid JSON' : (e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Queue a render" width={520}>
      <div className="flex flex-col gap-4">
        <label className="block">
          <span className="mb-1.5 block text-xs font-medium text-dim">Output</span>
          <select
            value={output}
            onChange={(e) => setOutput(e.target.value)}
            className="h-9 w-full rounded-lg border border-line bg-panel-2 px-2.5 text-sm"
          >
            {outputs.map((g) => (
              <optgroup key={g.group} label={g.group}>
                {g.outputs.map((o) => (
                  <option key={o} value={o}>{o}</option>
                ))}
              </optgroup>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="mb-1.5 block text-xs font-medium text-dim">
            Job data <span className="text-faint">— include "force": "fail" to simulate a failure</span>
          </span>
          <textarea
            value={jobData}
            onChange={(e) => setJobData(e.target.value)}
            rows={7}
            spellCheck={false}
            className="w-full rounded-lg border border-line bg-panel-2 p-3 font-mono text-xs text-dim focus-visible:border-accent/50"
          />
        </label>

        {error && <p className="text-xs text-bad">{error}</p>}

        <div className="flex justify-end gap-2">
          <Button variant="subtle" onClick={onClose}>Cancel</Button>
          <Button variant="primary" disabled={busy} onClick={() => void run()}>
            {busy ? 'Dispatching…' : 'Dispatch to farm'}
          </Button>
        </div>
      </div>
    </Modal>
  )
}
