import { motion } from 'framer-motion'

export interface SeriesPoint {
  date: string
  completed: number
  failed: number
  blocked: number
}

/** 14-day stacked delivery bars with hover tooltip. */
export function StackedBars({ points }: { points: SeriesPoint[] }) {
  const max = Math.max(1, ...points.map((p) => p.completed + p.failed + p.blocked))

  return (
    <div>
      <div className="flex h-36 items-end gap-1.5">
        {points.map((p, i) => {
          const total = p.completed + p.failed + p.blocked
          const h = (n: number) => `${(n / max) * 100}%`
          return (
            <div key={p.date} className="group relative flex h-full flex-1 flex-col justify-end">
              <motion.div
                className="flex flex-col-reverse overflow-hidden rounded-[4px]"
                initial={{ height: 0 }}
                animate={{ height: total === 0 ? '2px' : '100%' }}
                transition={{ duration: 0.5, delay: i * 0.03, ease: 'easeOut' }}
              >
                <div style={{ height: h(p.completed) }} className="bg-accent/80" />
                <div style={{ height: h(p.failed) }} className="bg-bad/80" />
                <div style={{ height: h(p.blocked) }} className="bg-warn/60" />
              </motion.div>
              <div
                className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-2 hidden -translate-x-1/2 whitespace-nowrap rounded-md border border-line bg-panel-3 px-2.5 py-1.5 text-[11px] shadow-xl group-hover:block"
              >
                <div className="font-medium">{new Date(p.date + 'T00:00:00').toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}</div>
                <div className="text-faint">
                  <span className="text-accent">{p.completed} ok</span>
                  {p.failed > 0 && <span className="text-bad"> · {p.failed} failed</span>}
                  {p.blocked > 0 && <span className="text-warn"> · {p.blocked} blocked</span>}
                  {total === 0 && 'no traffic'}
                </div>
              </div>
            </div>
          )
        })}
      </div>
      <div className="mt-2 flex justify-between font-mono text-[10px] text-faint">
        <span>{label(points[0]?.date)}</span>
        <span>{label(points[Math.floor(points.length / 2)]?.date)}</span>
        <span>today</span>
      </div>
      <div className="mt-3 flex items-center gap-4 text-[11px] text-faint">
        <Legend color="bg-accent/80" label="completed" />
        <Legend color="bg-bad/80" label="failed" />
        <Legend color="bg-warn/60" label="blocked" />
      </div>
    </div>
  )
}

function label(date?: string) {
  if (!date) return ''
  return new Date(date + 'T00:00:00').toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

function Legend({ color, label }: { color: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span className={`h-2 w-2 rounded-[3px] ${color}`} />
      {label}
    </span>
  )
}

/** Tiny inline trend line for cards. */
export function Sparkline({ values, color = '#3ad9c5' }: { values: number[]; color?: string }) {
  const w = 120
  const h = 32
  const max = Math.max(1, ...values)
  const step = values.length > 1 ? w / (values.length - 1) : w
  const path = values
    .map((v, i) => `${i === 0 ? 'M' : 'L'}${(i * step).toFixed(1)},${(h - (v / max) * (h - 4) - 2).toFixed(1)}`)
    .join(' ')

  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} className="overflow-visible">
      <motion.path
        d={path}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinecap="round"
        initial={{ pathLength: 0 }}
        animate={{ pathLength: 1 }}
        transition={{ duration: 0.8, ease: 'easeOut' }}
      />
    </svg>
  )
}
