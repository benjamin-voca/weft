import { useEffect, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import type { CommitDto, DiffDto } from '../api/types'
import { api } from '../api/client'
import { Badge, Button, VersionBadge } from './ui'
import { DiffView } from './DiffView'
import { relTime, absTime } from '../lib/format'

export function CommitTimeline({ assetId, commits, onRestore, changedVersions }: {
  assetId: string
  commits: CommitDto[]
  onRestore?: (version: number) => void
  changedVersions?: Set<number>
}) {
  const [compare, setCompare] = useState<[number, number] | null>(null)
  const latest = commits[0]?.version ?? 0

  return (
    <div className="flex flex-col gap-6">
      <AnimatePresence>
        {compare && (
          <motion.div key={`${compare[0]}-${compare[1]}`} exit={{ opacity: 0, y: -6 }}
            initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }}>
            <ComparePanel assetId={assetId} from={compare[0]} to={compare[1]} onClose={() => setCompare(null)} />
          </motion.div>
        )}
      </AnimatePresence>

      <div className="relative">
        <div className="absolute bottom-2 left-[13px] top-2 w-px bg-line" aria-hidden />
        <ul className="flex flex-col gap-3">
          {commits.map((c, i) => (
            <motion.li
              key={c.id}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.25, delay: Math.min(i * 0.04, 0.4) }}
              className="relative flex gap-4"
            >
              <span
                className={`relative z-10 mt-1 grid h-[27px] w-[27px] shrink-0 place-items-center rounded-full border text-[11px] font-semibold ${
                  c.version === latest
                    ? 'border-accent/50 bg-accent/15 text-accent'
                    : 'border-line bg-panel-2 text-dim'
                }`}
              >
                {c.version}
              </span>

              <div className="card min-w-0 flex-1 p-3.5">
                <div className="flex flex-wrap items-center gap-2">
                  <VersionBadge version={c.version} hash={c.hash} />
                  {c.version === latest && <Badge color="accent">HEAD</Badge>}
                  {c.isRollback && (
                    <Badge color="iris" title={`Restores v${c.restoredVersion}`}>
                      ↺ revert of v{c.restoredVersion}
                    </Badge>
                  )}
                  {changedVersions?.has(c.version) && (
                    <Badge color="bad" title="Deliveries failed while this version was live">failures</Badge>
                  )}
                  <span className="ml-auto flex items-center gap-2 text-[11px] text-faint">
                    <span className="font-mono">{c.author}</span>
                    <span title={absTime(c.createdAt)}>{relTime(c.createdAt)}</span>
                  </span>
                </div>

                <p className="mt-2 text-sm text-text">{c.message}</p>

                {c.changes.length > 0 && (
                  <div className="mt-2.5 flex flex-wrap gap-1.5">
                    {c.changes.slice(0, 4).map((ch) => (
                      <span
                        key={ch.path + ch.kind + (ch.to ?? ch.from)}
                        className="inline-flex max-w-[280px] items-center gap-1 truncate rounded-md border border-line-soft bg-panel-2/70 px-1.5 py-0.5 font-mono text-[10.5px] text-faint"
                      >
                        <span className={ch.kind === 'added' ? 'text-good' : ch.kind === 'removed' ? 'text-bad' : 'text-warn'}>
                          {ch.kind === 'added' ? '+' : ch.kind === 'removed' ? '−' : '~'}
                        </span>
                        {ch.path}
                      </span>
                    ))}
                    {c.changes.length > 4 && (
                      <span className="px-1 text-[10.5px] text-faint">+{c.changes.length - 4} more</span>
                    )}
                  </div>
                )}

                <div className="mt-3 flex gap-2">
                  {c.version > 1 && (
                    <Button
                      variant="subtle"
                      className="h-7 px-2 text-xs"
                      onClick={() => setCompare([c.version - 1, c.version])}
                    >
                      Diff vs v{c.version - 1}
                    </Button>
                  )}
                  {i > 0 && (
                    <Button
                      variant="subtle"
                      className="h-7 px-2 text-xs"
                      onClick={() => setCompare([c.version, commits[0].version])}
                    >
                      Compare to HEAD
                    </Button>
                  )}
                  {c.version !== latest && onRestore && (
                    <Button variant="subtle" className="h-7 px-2 text-xs" onClick={() => onRestore(c.version)}>
                      ↺ Restore v{c.version}
                    </Button>
                  )}
                </div>
              </div>
            </motion.li>
          ))}
        </ul>
      </div>
    </div>
  )
}

function ComparePanel({ assetId, from, to, onClose }: {
  assetId: string
  from: number
  to: number
  onClose: () => void
}) {
  const [diff, setDiff] = useState<DiffDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let alive = true
    api.diff(assetId, from, to)
      .then((d) => alive && setDiff(d))
      .catch((e) => alive && setError(e.message))
    return () => { alive = false }
  }, [assetId, from, to])

  return (
    <div className="card p-4">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="text-xs font-semibold text-dim">
          Comparing <span className="font-mono text-text">v{from}</span> →{' '}
          <span className="font-mono text-text">v{to}</span>
        </h3>
        <Button variant="subtle" className="h-6 px-2 text-xs" onClick={onClose}>
          hide
        </Button>
      </div>
      {error && <p className="text-xs text-bad">{error}</p>}
      {!error && !diff && <p className="text-xs text-faint">Loading diff…</p>}
      {diff && <DiffView changes={diff.changes} />}
    </div>
  )
}
