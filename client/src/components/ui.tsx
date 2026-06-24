import { createContext, useCallback, useContext, useRef, useState } from 'react'
import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'
import { AnimatePresence, motion } from 'framer-motion'

// ── Button ──────────────────────────────────────────────────────────────────
type ButtonVariant = 'primary' | 'ghost' | 'danger' | 'subtle'

export function Button({
  variant = 'ghost', className = '', children, ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }) {
  const base =
    'inline-flex items-center gap-2 rounded-lg px-3.5 h-9 text-sm font-medium transition-colors duration-150 disabled:opacity-40 disabled:pointer-events-none disabled:cursor-not-allowed select-none'
  const styles: Record<ButtonVariant, string> = {
    primary: 'bg-accent text-[#062723] hover:bg-[#5ee6d4] font-semibold',
    ghost: 'bg-panel-2 text-text border border-line hover:border-[#33415c] hover:bg-panel-3',
    danger: 'bg-bad/10 text-bad border border-bad/25 hover:bg-bad/20',
    subtle: 'text-dim hover:text-text hover:bg-panel-2',
  }
  return (
    <button className={`${base} ${styles[variant]} ${className}`} {...rest}>
      {children}
    </button>
  )
}

// ── Badge ───────────────────────────────────────────────────────────────────
export function Badge({ color = 'dim', children, title }: {
  color?: 'dim' | 'accent' | 'good' | 'bad' | 'warn' | 'iris'
  children: ReactNode
  title?: string
}) {
  const palette = {
    dim: 'bg-panel-2 text-dim border-line',
    accent: 'bg-accent/10 text-accent border-accent/25',
    good: 'bg-good/10 text-good border-good/25',
    bad: 'bg-bad/10 text-bad border-bad/25',
    warn: 'bg-warn/10 text-warn border-warn/25',
    iris: 'bg-iris/10 text-iris border-iris/25',
  }[color]
  return (
    <span
      title={title}
      className={`inline-flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-[11px] font-medium leading-4 ${palette}`}
    >
      {children}
    </span>
  )
}

export function VersionBadge({ version, hash }: { version: number; hash?: string }) {
  return (
    <span className="inline-flex items-center rounded-md border border-line bg-panel-2 font-mono text-[11px] leading-4">
      <span className="px-1.5 py-0.5 text-text">v{version}</span>
      {hash && <span className="px-1.5 py-0.5 border-l border-line text-faint">{hash}</span>}
    </span>
  )
}

// ── Inputs ──────────────────────────────────────────────────────────────────
export function Input({ className = '', ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`h-9 w-full rounded-lg bg-panel-2 border border-line px-3 text-sm text-text placeholder:text-faint transition-colors hover:border-[#2b3850] focus-visible:border-accent/50 ${className}`}
      {...rest}
    />
  )
}

export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1.5 flex items-baseline gap-2 text-xs font-medium text-dim">
        {label}
        {hint && <span className="text-[11px] font-normal text-faint">{hint}</span>}
      </span>
      {children}
    </label>
  )
}

// ── Toggle ──────────────────────────────────────────────────────────────────
export function Toggle({ on, onChange, label }: {
  on: boolean
  onChange: (next: boolean) => void
  label?: string
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={label}
      onClick={() => onChange(!on)}
      className={`relative h-6 w-11 shrink-0 rounded-full border transition-colors duration-200 ${
        on ? 'bg-accent/25 border-accent/40' : 'bg-panel-2 border-line'
      }`}
    >
      <motion.span
        layout
        transition={{ type: 'spring', stiffness: 500, damping: 32 }}
        className={`absolute top-1/2 h-4 w-4 -translate-y-1/2 rounded-full ${
          on ? 'right-1 bg-accent' : 'left-1 bg-faint'
        }`}
      />
    </button>
  )
}

// ── Modal ───────────────────────────────────────────────────────────────────
export function Modal({ open, onClose, title, children, width = 560 }: {
  open: boolean
  onClose: () => void
  title: ReactNode
  children: ReactNode
  width?: number
}) {
  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/55 p-6 pt-[10vh] backdrop-blur-[2px]"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
          transition={{ duration: 0.15 }}
          onMouseDown={(e) => { if (e.target === e.currentTarget) onClose() }}
        >
          <motion.div
            className="card w-full shadow-2xl shadow-black/50"
            style={{ maxWidth: width }}
            initial={{ opacity: 0, scale: 0.96, y: 10 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.97, y: 6 }}
            transition={{ duration: 0.18, ease: [0.2, 0.9, 0.3, 1] }}
          >
            <div className="flex items-center justify-between border-b border-line px-5 py-3.5">
              <h2 className="text-sm font-semibold">{title}</h2>
              <button
                onClick={onClose}
                aria-label="Close dialog"
                className="grid h-7 w-7 place-items-center rounded-md text-faint transition-colors hover:bg-panel-2 hover:text-text"
              >
                ✕
              </button>
            </div>
            <div className="p-5">{children}</div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}

// ── Toasts ──────────────────────────────────────────────────────────────────
interface Toast { id: number; text: string; tone: 'ok' | 'err' | 'info' }
const ToastCtx = createContext<(text: string, tone?: Toast['tone']) => void>(() => {})

export function useToast() {
  return useContext(ToastCtx)
}

export function ToastHost({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(1)

  const push = useCallback((text: string, tone: Toast['tone'] = 'ok') => {
    const id = nextId.current++
    setToasts((t) => [...t, { id, text, tone }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 3800)
  }, [])

  return (
    <ToastCtx.Provider value={push}>
      {children}
      <div className="pointer-events-none fixed bottom-5 right-5 z-[60] flex w-80 flex-col gap-2">
        <AnimatePresence>
          {toasts.map((t) => (
            <motion.div
              key={t.id}
              layout
              initial={{ opacity: 0, x: 24, scale: 0.97 }}
              animate={{ opacity: 1, x: 0, scale: 1 }}
              exit={{ opacity: 0, x: 16, scale: 0.97 }}
              transition={{ duration: 0.2, ease: 'easeOut' }}
              className={`card pointer-events-auto flex items-center gap-2.5 px-3.5 py-2.5 text-sm shadow-xl shadow-black/40 ${
                t.tone === 'err' ? 'border-bad/40' : t.tone === 'info' ? '' : 'border-accent/30'
              }`}
            >
              <span
                className={`h-1.5 w-1.5 shrink-0 rounded-full ${
                  t.tone === 'err' ? 'bg-bad' : t.tone === 'info' ? 'bg-sky' : 'bg-accent'
                }`}
              />
              {t.text}
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </ToastCtx.Provider>
  )
}

// ── Misc ────────────────────────────────────────────────────────────────────
export function StatusDot({ tone, pulse }: { tone: 'good' | 'bad' | 'warn' | 'faint'; pulse?: boolean }) {
  const color = { good: 'bg-good', bad: 'bg-bad', warn: 'bg-warn', faint: 'bg-faint' }[tone]
  return (
    <span
      className={`inline-block h-2 w-2 rounded-full ${color} ${pulse ? 'animate-[pulse-dot_2s_ease-in-out_infinite]' : ''}`}
    />
  )
}

export function Spinner({ className = '' }: { className?: string }) {
  return (
    <span
      className={`inline-block h-4 w-4 animate-spin rounded-full border-2 border-line border-t-accent ${className}`}
    />
  )
}

export function EmptyState({ title, hint, action }: { title: string; hint?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-line py-14 text-center">
      <p className="text-sm font-medium text-dim">{title}</p>
      {hint && <p className="max-w-sm text-xs text-faint">{hint}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}
