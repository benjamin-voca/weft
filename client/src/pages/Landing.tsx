import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useAuth } from '../auth'
import { WeftMark } from '../components/WeftMark'
import { Badge, Button, VersionBadge } from '../components/ui'
import { OutputChip } from '../components/RenderLog'

const fade = (delay = 0) => ({
  initial: { opacity: 0, y: 16 },
  animate: { opacity: 1, y: 0 },
  transition: { duration: 0.45, delay, ease: 'easeOut' as const },
})

export function Landing() {
  const { user } = useAuth()

  return (
    <div className="min-h-screen bg-bg" data-testid="landing">
      <nav className="mx-auto flex max-w-5xl items-center justify-between px-8 py-6">
        <span className="flex items-center gap-2.5">
          <WeftMark />
          <span className="text-[15px] font-semibold tracking-tight">weft</span>
        </span>
        <Link to={user ? '/app' : '/login'}>
          {user
            ? <Button variant="primary" data-testid="nav-console">Open console</Button>
            : <Button variant="primary" data-testid="nav-signin">Sign in</Button>}
        </Link>
      </nav>

      <header className="mx-auto grid max-w-5xl grid-cols-1 items-center gap-10 px-8 pb-16 pt-10 md:grid-cols-[1.1fr_1fr]">
        <motion.div {...fade(0)}>
          <p className="font-mono text-[11px] uppercase tracking-[0.2em] text-accent">
            for Blender artists & studios
          </p>
          <h1 className="mt-3 text-4xl font-semibold leading-[1.15] tracking-tight">
            Version control for your Blender&nbsp;projects.
          </h1>
          <p className="mt-4 max-w-lg text-[15px] leading-relaxed text-dim">
            Git simplified to what actually matters: every change to a rig, scene or
            material library becomes an immutable snapshot with a semantic diff.
            Roll back in one click — and every render remembers exactly which
            snapshot it came from.
          </p>
          <div className="mt-7 flex items-center gap-3">
            <Link to={user ? '/app' : '/login'}>
              <Button variant="primary" data-testid="cta-open">
                {user ? 'Open the console' : 'Sign in to the console'}
              </Button>
            </Link>
            <a href="#how">
              <Button variant="ghost">See how it works</Button>
            </a>
          </div>
          <p className="mt-6 font-mono text-[11px] text-faint">
            immutable snapshots · semantic diffs · one-click rollback
          </p>
        </motion.div>

        {/* Product still-life: a diff, a commit, a delivery. */}
        <motion.div {...fade(0.15)} className="flex flex-col gap-3">
          <div className="card p-4">
            <div className="mb-2.5 flex items-center justify-between">
              <span className="text-[11px] font-medium text-dim">Draft vs v5</span>
              <Badge color="warn">2 changes</Badge>
            </div>
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center gap-2 rounded-lg border border-line-soft bg-panel-2/60 px-2.5 py-1.5">
                <span className="grid h-5 w-5 place-items-center rounded-md bg-warn/10 text-[11px] font-bold text-warn">~</span>
                <code className="font-mono text-[11px] text-dim">engine</code>
                <span className="ml-auto text-[11px]"><span className="text-faint line-through">CYCLES</span> <span className="text-faint">→</span> <span className="text-text">EEVEE</span></span>
              </div>
              <div className="flex items-center gap-2 rounded-lg border border-line-soft bg-panel-2/60 px-2.5 py-1.5">
                <span className="grid h-5 w-5 place-items-center rounded-md bg-good/10 text-[11px] font-bold text-good">+</span>
                <code className="font-mono text-[11px] text-dim">materials</code>
                <span className="ml-auto"><OutputChip output="Wet Cobblestone" /></span>
              </div>
            </div>
          </div>

          <div className="card flex items-center gap-3 p-3.5">
            <VersionBadge version={6} hash="9d21c0f4" />
            <Badge color="accent">HEAD</Badge>
            <span className="text-xs text-text">Revert to v4</span>
            <span className="ml-auto font-mono text-[11px] text-faint">klajdi · 2m ago</span>
          </div>

          <div className="card flex items-center gap-3 p-3.5">
            <span className="grid h-6 w-6 place-items-center rounded-full bg-good/15 text-[11px] font-bold text-good">✓</span>
            <OutputChip output="final" />
            <Badge color="dim" title="Snapshot this render used">v5</Badge>
            <span className="ml-auto font-mono text-[11px] text-faint">2m 04s · just now</span>
          </div>
        </motion.div>
      </header>

      <section className="mx-auto max-w-5xl px-8 pb-16">
        <div className="grid grid-cols-1 gap-3 md:grid-cols-4">
          {[
            ['Snapshot history', 'Every save mints an immutable, content-hashed snapshot of the .blend state. Nothing is ever rewritten.'],
            ['Semantic diffs', 'Modifiers, materials, render settings — old struck through, new beside it. No text-diff soup.'],
            ['One-click rollback', 'Restoring an old snapshot creates a new commit. Undo your undo, always.'],
            ['Render pinning', 'Every render remembers the snapshot it came from. Blame broken frames with confidence.'],
          ].map(([title, copy], i) => (
            <motion.div key={title} {...fade(0.2 + i * 0.07)} className="card p-4">
              <h3 className="text-sm font-semibold">{title}</h3>
              <p className="mt-1.5 text-xs leading-relaxed text-faint">{copy}</p>
            </motion.div>
          ))}
        </div>
      </section>

      <section id="how" className="mx-auto max-w-5xl px-8 pb-20">
        <motion.div {...fade(0.1)} className="card px-6 py-8">
          <h2 className="text-center text-sm font-semibold text-dim">Three moves, that's the whole dance</h2>
          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-3">
            {[
              ['1', 'Edit the draft', 'Tweak the rig, scene or render settings. The draft auto-saves and diffs against HEAD as you type.'],
              ['2', 'Commit the snapshot', 'One click mints vN+1 with an auto-written message. Discarding is always safe.'],
              ['3', 'Render & blame', 'Queue renders on the farm, re-run old frames, and see which snapshot handled what.'],
            ].map(([n, title, copy]) => (
              <div key={n} className="flex flex-col items-center gap-2 text-center">
                <span className="grid h-8 w-8 place-items-center rounded-full border border-accent/40 bg-accent/10 font-mono text-sm font-semibold text-accent">
                  {n}
                </span>
                <h3 className="text-sm font-medium">{title}</h3>
                <p className="max-w-xs text-xs leading-relaxed text-faint">{copy}</p>
              </div>
            ))}
          </div>
        </motion.div>
      </section>

      <footer className="border-t border-line-soft py-8 text-center font-mono text-[11px] text-faint">
        weft — woven with .NET, React &amp; PostgreSQL · made for Blender pipelines
      </footer>
    </div>
  )
}
