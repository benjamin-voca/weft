import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useAuth } from '../auth'
import { WeftMark } from '../components/WeftMark'
import { Button, Field, Input, useToast } from '../components/ui'

export function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const toast = useToast()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const from = (location.state as { from?: string } | null)?.from ?? '/app'

  const submit = async (e?: React.FormEvent) => {
    e?.preventDefault()
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      await login(email, password)
      toast(`Welcome back, ${email}`)
      navigate(from, { replace: true })
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="grid min-h-screen place-items-center bg-bg px-6">
      <motion.div
        initial={{ opacity: 0, y: 14 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.35, ease: 'easeOut' }}
        className="w-full max-w-sm"
        data-testid="login-card"
      >
        <Link to="/" className="mb-8 flex flex-col items-center gap-2.5">
          <WeftMark size={40} />
          <span className="text-center">
            <span className="block text-lg font-semibold tracking-tight">weft</span>
            <span className="block text-[11px] text-faint">version control for Blender</span>
          </span>
        </Link>

        <form onSubmit={submit} className="card p-6">
          <h1 className="text-sm font-semibold">Sign in to the console</h1>
          <p className="mt-1 text-xs text-faint">Your assets, snapshots and renders await.</p>

          <div className="mt-5 flex flex-col gap-4">
            <Field label="Email">
              <Input
                type="email"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@company.com"
                data-testid="login-email"
              />
            </Field>
            <Field label="Password">
              <Input
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                data-testid="login-password"
              />
            </Field>
          </div>

          {error && (
            <motion.p
              initial={{ opacity: 0, y: -4 }}
              animate={{ opacity: 1, y: 0 }}
              className="mt-4 rounded-lg border border-bad/25 bg-bad/10 px-3 py-2 text-xs text-bad"
              data-testid="login-error"
            >
              {error}
            </motion.p>
          )}

          <Button
            type="submit"
            variant="primary"
            className="mt-5 w-full justify-center"
            disabled={busy || !email.trim() || !password}
            data-testid="login-submit"
          >
            {busy ? 'Signing in…' : 'Sign in'}
          </Button>

          <div className="mt-4 rounded-lg border border-line-soft bg-panel-2/50 px-3 py-2.5 text-[11px] leading-relaxed text-faint">
            <p className="mb-1 font-mono uppercase tracking-widest text-dim">demo access</p>
            admin@example.com · admin
            <button
              type="button"
              onClick={() => { setEmail('admin@example.com'); setPassword('admin') }}
              className="ml-2 rounded-md border border-line px-1.5 py-0.5 text-[10px] text-dim transition-colors hover:border-accent/40 hover:text-accent"
              data-testid="login-autofill"
            >
              autofill
            </button>
          </div>
        </form>

        <p className="mt-6 text-center text-xs text-faint">
          <Link to="/" className="transition-colors hover:text-dim">← back to the landing page</Link>
        </p>
      </motion.div>
    </div>
  )
}
