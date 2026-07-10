import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import type { ProjectSummary, ProjectStats } from '../api/types'
import { api } from '../api/client'
import { useApi } from '../lib/useApi'
import { Badge, Spinner, StatusDot } from '../components/ui'
import { Sparkline } from '../components/charts'
import { duration } from '../lib/format'

export function Overview() {
  const projects = useApi(api.projects, [])
  const stats = useApi(
    async () => {
      const list = await api.projects()
      return Promise.all(list.map(async (p) => ({ project: p, stats: await api.stats(p.id) })))
    },
    [],
  )

  const rows: { project: ProjectSummary; stats: ProjectStats }[] = stats.data ?? []
  const totalRenders = rows.reduce((n, r) => n + r.stats.totals.renders, 0)

  return (
    <div className="mx-auto max-w-5xl px-8 py-10">
      <motion.header
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.35 }}
        className="mb-10"
      >
        <p className="font-mono text-[11px] uppercase tracking-[0.2em] text-accent">weft · console</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Projects</h1>
        <p className="mt-2 max-w-2xl text-sm text-dim">
          Every asset change below is an immutable snapshot with a semantic diff — modifiers,
          materials, render settings. Roll back in one click, and blame failed renders on the
          exact snapshot that broke them.
        </p>
      </motion.header>

      {(projects.error || stats.error) && (
        <p className="card border-bad/40 p-4 text-sm text-bad" data-testid="overview-error">
          {projects.error ?? stats.error}
        </p>
      )}

      {stats.loading && !stats.data && (
        <div className="flex items-center gap-3 py-16 text-sm text-faint" data-testid="overview-loading">
          <Spinner /> Loading projects…
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        {rows.map((row, i) => (
          <motion.div
            key={row.project.id}
            initial={{ opacity: 0, y: 14 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.35, delay: 0.08 + i * 0.07 }}
          >
            <Link
              to={`/app/p/${row.project.id}`}
              className="card group block p-5 transition-all duration-200 hover:-translate-y-0.5 hover:border-[#33415c] hover:shadow-xl hover:shadow-black/30"
              data-testid={`project-card-${row.project.name}`}
            >
              <div className="flex items-center justify-between">
                <h2 className="text-base font-semibold">{row.project.name}</h2>
                <Badge color={row.stats.totals.successRate >= 90 ? 'good' : row.stats.totals.successRate >= 70 ? 'warn' : 'bad'}>
                  {row.stats.totals.successRate.toFixed(0)}% ok
                </Badge>
              </div>
              <p className="mt-1.5 line-clamp-2 min-h-8 text-xs leading-relaxed text-faint">
                {row.project.description}
              </p>

              <div className="mt-4 flex items-end justify-between">
                <div className="flex flex-col gap-1 text-xs text-dim">
                  <span className="flex items-center gap-1.5">
                    <StatusDot tone={row.stats.totals.failed > 0 ? 'warn' : 'good'} />
                    {row.project.assetCount} assets
                  </span>
                  <span>{row.stats.totals.renders.toLocaleString()} renders · avg {duration(row.stats.latency.avgMs)}</span>
                </div>
                <Sparkline values={row.stats.series.map((s) => s.completed + s.failed + s.blocked)} />
              </div>
            </Link>
          </motion.div>
        ))}
      </div>

      {rows.length > 0 && (
        <motion.p
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: 0.4 }}
          className="mt-8 text-center font-mono text-[11px] text-faint"
        >
          {rows.length} productions · {rows.reduce((n, r) => n + r.project.assetCount, 0)} assets ·{' '}
          {totalRenders.toLocaleString()} renders under version control
        </motion.p>
      )}
    </div>
  )
}
