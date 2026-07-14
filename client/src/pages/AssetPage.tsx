import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import type { RenderDto, AssetConfig } from '../api/types'
import { api } from '../api/client'
import { useApi } from '../lib/useApi'
import { Badge, Button, Modal, Spinner, Input, useToast, VersionBadge } from '../components/ui'
import { ConfigEditor } from '../components/ConfigEditor'
import { CommitTimeline } from '../components/CommitTimeline'
import { RenderLog } from '../components/RenderLog'
import { DiffView } from '../components/DiffView'
import { shortPath } from '../lib/format'

type Tab = 'config' | 'timeline' | 'renders'

const configChanged = (a: AssetConfig, b: AssetConfig) =>
  JSON.stringify(a) !== JSON.stringify(b)

export function AssetPage() {
  const { projectId = '', assetId = '' } = useParams()
  const toast = useToast()

  const detail = useApi(() => api.asset(assetId), [assetId])
  const commits = useApi(() => api.commits(assetId), [assetId])
  const catalog = useApi(() => api.outputs(), [])
  const [renders, setRenders] = useState<RenderDto[] | null>(null)
  const [tab, setTab] = useState<Tab>('config')
  const [draft, setDraft] = useState<AssetConfig | null>(null)
  const [busy, setBusy] = useState(false)
  const [commitOpen, setCommitOpen] = useState(false)
  const [commitMsg, setCommitMsg] = useState('')
  const [restoreVersion, setRestoreVersion] = useState<number | null>(null)

  // The draft is loaded once per asset; afterwards it syncs explicitly
  // (discard/restore) or flows out via the debounced auto-save below.
  const loadedFor = useRef<string | null>(null)
  useEffect(() => {
    if (detail.data && loadedFor.current !== assetId) {
      setDraft(structuredClone(detail.data.workingConfig))
      loadedFor.current = assetId
    }
  }, [detail.data, assetId])

  // Weft has no staging area — the draft IS the working copy, so it persists
  // to the server as you type (debounced) and the draft diff stays live.
  const autosaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const cancelAutosave = () => {
    if (autosaveTimer.current) clearTimeout(autosaveTimer.current)
    autosaveTimer.current = null
  }
  useEffect(() => {
    if (!detail.data || !draft) return
    if (!configChanged(draft, detail.data.workingConfig)) return
    cancelAutosave()
    autosaveTimer.current = setTimeout(() => {
      api.saveConfig(assetId, draft)
        .then(() => detail.refresh())
        .catch(() => { /* invalid mid-typing state — server keeps last good draft */ })
    }, 600)
    return cancelAutosave
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft])

  const loadRenders = useMemo(
    () => () => api.renders(assetId).then(setRenders).catch(() => {}),
    [assetId],
  )
  useEffect(() => { void loadRenders() }, [loadRenders])

  const d = detail.data
  const localDirty = d !== null && draft !== null && configChanged(draft, d.workingConfig)
  const draftDiffCount = d?.draftDiff.length ?? 0

  if (detail.error) {
    return (
      <div className="mx-auto max-w-3xl px-8 py-16">
        <p className="card border-bad/40 p-5 text-sm text-bad">{detail.error}</p>
      </div>
    )
  }
  if (!d || !draft) {
    return (
      <div className="flex items-center gap-3 px-8 py-20 text-sm text-faint">
        <Spinner /> Loading asset…
      </div>
    )
  }

  const saveDraft = async (): Promise<boolean> => {
    cancelAutosave()
    setBusy(true)
    try {
      const updated = await api.saveConfig(assetId, draft)
      setDraft(structuredClone(updated.workingConfig))
      detail.refresh()
      return true
    } catch (e) {
      toast((e as Error).message, 'err')
      return false
    } finally {
      setBusy(false)
    }
  }

  const discard = async () => {
    cancelAutosave()
    setBusy(true)
    try {
      const updated = await api.discard(assetId)
      setDraft(structuredClone(updated.workingConfig))
      detail.refresh()
      toast('Draft discarded — restored from HEAD')
    } catch (e) {
      toast((e as Error).message, 'err')
    } finally {
      setBusy(false)
    }
  }

  const commit = async () => {
    setBusy(true)
    try {
      if (localDirty && !(await saveDraft())) return
      const c = await api.commit(assetId, commitMsg.trim() || null)
      toast(`Committed v${c.version} (${c.hash})`, 'ok')
      setCommitOpen(false)
      setCommitMsg('')
      detail.refresh()
      commits.refresh()
      loadRenders()
      setTab('timeline')
    } catch (e) {
      toast((e as Error).message, 'err')
    } finally {
      setBusy(false)
    }
  }

  const restore = async (version: number) => {
    setBusy(true)
    try {
      const c = await api.rollback(assetId, version, null)
      const fresh = await api.asset(assetId)
      setDraft(structuredClone(fresh.workingConfig))
      toast(`Restored v${version} as new commit v${c.version}`)
      setRestoreVersion(null)
      detail.refresh()
      commits.refresh()
      setTab('timeline')
    } catch (e) {
      toast((e as Error).message, 'err')
    } finally {
      setBusy(false)
    }
  }

  const failureVersions = new Set(
    (renders ?? []).filter((x) => x.status === 'failed').map((x) => x.assetVersion),
  )

  return (
    <div className="mx-auto max-w-5xl px-8 py-10" data-testid="asset-page">
      <motion.header initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="mb-5">
        <nav className="mb-2 flex items-center gap-1.5 text-xs text-faint">
          <Link to="/app" className="transition-colors hover:text-dim">console</Link>
          <span>/</span>
          <Link to={`/app/p/${projectId}`} className="transition-colors hover:text-dim">{d.projectName}</Link>
          <span>/</span>
          <span className="text-dim">{d.name}</span>
        </nav>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight">{d.name}</h1>
          <code className="font-mono text-xs text-faint">{shortPath(d.workingConfig.sourceFile)}</code>
          <span data-testid="head-version">
            <VersionBadge version={d.headVersion} hash={d.headHash} />
          </span>
          {d.dirty ? (
            <Badge color="warn">
              <span className="h-1.5 w-1.5 rounded-full bg-warn" /> draft · {draftDiffCount} change{draftDiffCount === 1 ? '' : 's'}
            </Badge>
          ) : (
            <Badge color="good">in sync</Badge>
          )}
        </div>
      </motion.header>

      <Tabs active={tab} onChange={setTab} draftCount={draftDiffCount} />

      <AnimatePresence mode="wait">
        <motion.div
          key={tab}
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -6 }}
          transition={{ duration: 0.18 }}
        >
          {tab === 'config' && (
            <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_340px]">
              <div className="card p-5">
                <ConfigEditor draft={draft} onChange={setDraft} />
              </div>

              <aside className="flex flex-col gap-4">
                <div className="card p-4">
                  <h3 className="mb-3 flex items-center gap-2 text-xs font-semibold text-dim">
                    Draft vs v{d.headVersion}
                    {localDirty && <Badge color="warn">saving…</Badge>}
                  </h3>
                  <div data-testid="draft-diff">
                    <DiffView changes={d.draftDiff} compact />
                  </div>
                  {d.draftDiff.length === 0 && !localDirty && (
                    <p className="text-xs text-faint">
                      The draft matches v{d.headVersion}. Edit the config to make changes — then commit.
                    </p>
                  )}
                </div>

                <div className="card sticky bottom-4 p-4 shadow-xl shadow-black/30">
                  <p className="mb-3 text-[11px] leading-relaxed text-faint">
                    Weft has no staging area — the draft <em>is</em> the staging area. Commit to mint an
                    immutable snapshot; history is never rewritten.
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant="ghost"
                      disabled={(!localDirty && !d.dirty) || busy}
                      onClick={() => void discard()}
                      data-testid="discard-btn"
                    >
                      Discard
                    </Button>
                    <Button
                      variant="primary"
                      className="flex-1 justify-center"
                      disabled={(!localDirty && !d.dirty) || busy}
                      onClick={async () => {
                        if (localDirty) {
                          if (await saveDraft()) setCommitOpen(true)
                        } else {
                          setCommitOpen(true)
                        }
                      }}
                      data-testid="commit-btn"
                    >
                      Commit changes
                    </Button>
                  </div>
                  {!localDirty && !d.dirty && (
                    <p className="mt-2 text-center text-[11px] text-faint">Nothing to commit</p>
                  )}
                </div>
              </aside>
            </div>
          )}

          {tab === 'timeline' && (
            <CommitTimeline
              assetId={assetId}
              commits={commits.data ?? []}
              onRestore={(v) => setRestoreVersion(v)}
              changedVersions={failureVersions}
            />
          )}

          {tab === 'renders' && (
            <RenderLog
              assetId={assetId}
              renders={renders ?? []}
              outputs={catalog.data ?? []}
              onReplayed={loadRenders}
            />
          )}
        </motion.div>
      </AnimatePresence>

      <Modal open={commitOpen} onClose={() => setCommitOpen(false)} title={`Commit to ${d.name}`} width={520}>
        <div className="flex flex-col gap-4">
          <div className="rounded-lg border border-line-soft bg-panel-2/50 p-3">
            <h4 className="mb-2 text-xs font-medium text-dim">
              {d.draftDiff.length} change{d.draftDiff.length === 1 ? '' : 's'} to snapshot as v{d.headVersion + 1}
            </h4>
            <DiffView changes={d.draftDiff} compact />
          </div>
          <label className="block">
            <span className="mb-1.5 block text-xs font-medium text-dim">
              Message <span className="text-faint">— left empty, Weft writes it from the diff</span>
            </span>
            <Input
              value={commitMsg}
              onChange={(e) => setCommitMsg(e.target.value)}
              placeholder={autoMessage(d.draftDiff)}
              data-testid="commit-message"
            />
          </label>
          <div className="flex justify-end gap-2">
            <Button variant="subtle" onClick={() => setCommitOpen(false)}>Cancel</Button>
            <Button
              variant="primary"
              disabled={busy}
              onClick={() => void commit()}
              data-testid="commit-confirm"
            >
              {busy ? 'Committing…' : `Commit v${d.headVersion + 1}`}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        open={restoreVersion !== null}
        onClose={() => setRestoreVersion(null)}
        width={440}
        title={`Restore v${restoreVersion ?? ''}`}
      >
        {restoreVersion !== null && (
          <div className="flex flex-col gap-4">
            <h3 className="text-sm font-semibold">Restore v{restoreVersion}?</h3>
            <p className="text-xs leading-relaxed text-dim">
              History is immutable: Weft restores the v{restoreVersion} snapshot by creating a{' '}
              <strong className="text-text">new commit</strong> (v{d.headVersion + 1}). The current
              draft, if any, is replaced by the restored config.
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="subtle" onClick={() => setRestoreVersion(null)}>Cancel</Button>
              <Button variant="primary" disabled={busy} onClick={() => void restore(restoreVersion)} data-testid="restore-confirm">
                Restore as v{d.headVersion + 1}
              </Button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  )
}

function autoMessage(changes: { path: string; kind: string; to: string | null }[]): string {
  return changes
    .slice(0, 3)
    .map((c) => `${c.kind === 'added' ? '+' : c.kind === 'removed' ? '-' : '~'} ${c.path}`)
    .join(', ')
}

function Tabs({ active, onChange, draftCount }: {
  active: Tab
  onChange: (t: Tab) => void
  draftCount: number
}) {
  const tabs: { id: Tab; label: string; count?: number }[] = [
    { id: 'config', label: 'Config', count: draftCount },
    { id: 'timeline', label: 'Timeline' },
    { id: 'renders', label: 'Renders' },
  ]
  return (
    <div className="mb-5 flex items-center gap-1 border-b border-line" role="tablist" data-testid="asset-tabs">
      {tabs.map((t) => (
        <button
          key={t.id}
          role="tab"
          aria-selected={active === t.id}
          onClick={() => onChange(t.id)}
          data-testid={`tab-${t.id}`}
          className={`relative px-3.5 pb-2.5 pt-1.5 text-sm transition-colors ${
            active === t.id ? 'font-medium text-text' : 'text-faint hover:text-dim'
          }`}
        >
          <span className="flex items-center gap-1.5">
            {t.label}
            {t.count !== undefined && t.count > 0 && (
              <span className="rounded-full bg-accent/15 px-1.5 py-px font-mono text-[10px] text-accent">
                {t.count}
              </span>
            )}
          </span>
          {active === t.id && (
            <motion.span
              layoutId="tab-underline"
              className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-accent"
              transition={{ type: 'spring', stiffness: 420, damping: 34 }}
            />
          )}
        </button>
      ))}
    </div>
  )
}
