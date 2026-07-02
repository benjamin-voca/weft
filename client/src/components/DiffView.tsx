import { motion } from 'framer-motion'
import type { ChangeEntry } from '../api/types'
import { Badge } from './ui'
import { Masked } from './Masked'

/** Field-level semantic diff — the readable alternative to a text diff. */
export function DiffView({ changes, compact = false }: { changes: ChangeEntry[]; compact?: boolean }) {
  if (changes.length === 0) {
    return <p className="text-xs text-faint">No differences.</p>
  }

  return (
    <ul className="flex flex-col gap-1">
      {changes.map((c, i) => (
        <motion.li
          key={c.path + c.kind}
          initial={{ opacity: 0, x: -6 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.18, delay: i * 0.025 }}
          className={`rounded-lg border border-line-soft bg-panel-2/60 ${compact ? 'px-2.5 py-1.5' : 'px-3 py-2'}`}
        >
          <div className="flex items-center gap-2.5">
            <span
              className={`grid h-5 w-5 shrink-0 place-items-center rounded-md text-[11px] font-bold ${
                c.kind === 'added'
                  ? 'bg-good/10 text-good'
                  : c.kind === 'removed'
                    ? 'bg-bad/10 text-bad'
                    : 'bg-warn/10 text-warn'
              }`}
              title={c.kind}
            >
              {c.kind === 'added' ? '+' : c.kind === 'removed' ? '−' : '~'}
            </span>
            <code className="font-mono text-xs text-dim">{c.path}</code>
            <span className="ml-auto">
              <Badge color={c.kind === 'added' ? 'good' : c.kind === 'removed' ? 'bad' : 'warn'}>
                {c.kind}
              </Badge>
            </span>
          </div>
          <div className="mt-1 break-all pl-[30px] text-xs leading-relaxed">
            {c.kind !== 'added' && <span className="text-faint line-through">{Masked.mask(c.path, c.from)}</span>}
            {c.kind !== 'added' && c.kind !== 'removed' && <span className="mx-1.5 text-faint">→</span>}
            {c.kind !== 'removed' && <span className="text-text">{Masked.mask(c.path, c.to)}</span>}
          </div>
        </motion.li>
      ))}
    </ul>
  )
}
