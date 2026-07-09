import { Link, NavLink, Navigate, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import { api } from './api/client'
import { useApi } from './lib/useApi'
import { AuthProvider, useAuth } from './auth'
import { ToastHost } from './components/ui'
import { WeftMark } from './components/WeftMark'
import { Landing } from './pages/Landing'
import { Login } from './pages/Login'
import { Overview } from './pages/Overview'
import { ProjectPage } from './pages/ProjectPage'
import { AssetPage } from './pages/AssetPage'

export default function App() {
  return (
    <AuthProvider>
      <ToastHost>
        <AnimatePresence mode="wait">
          <Routes>
            <Route path="/" element={<Landing />} />
            <Route path="/login" element={<Login />} />
            <Route element={<RequireAuth />}>
              <Route path="/app" element={<Shell />}>
                <Route index element={<Overview />} />
                <Route path="p/:projectId" element={<ProjectPage />} />
                <Route path="p/:projectId/a/:assetId" element={<AssetPage />} />
              </Route>
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </AnimatePresence>
      </ToastHost>
    </AuthProvider>
  )
}

function RequireAuth() {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return (
      <div className="grid h-screen place-items-center bg-bg text-sm text-faint" data-testid="auth-loading">
        <span className="flex items-center gap-3"><Spinner /> Restoring session…</span>
      </div>
    )
  }
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return <Outlet />
}

function PageTransition() {
  const location = useLocation()
  return (
    <AnimatePresence mode="wait">
      <motion.div
        key={location.pathname}
        initial={{ opacity: 0, y: 6 }}
        animate={{ opacity: 1, y: 0 }}
        exit={{ opacity: 0, y: -4 }}
        transition={{ duration: 0.16, ease: 'easeOut' }}
        className="min-h-full"
      >
        <Outlet />
      </motion.div>
    </AnimatePresence>
  )
}

function Shell() {
  return (
    <div className="flex min-h-full">
      <Sidebar />
      <main className="min-w-0 flex-1">
        <PageTransition />
      </main>
    </div>
  )
}

function Spinner() {
  return (
    <span className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-line border-t-accent" />
  )
}

function Sidebar() {
  const projects = useApi(api.projects, [])
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const signOut = async () => {
    await logout()
    navigate('/')
  }

  return (
    <aside className="sticky top-0 flex h-screen w-60 shrink-0 flex-col border-r border-line bg-[#0c1017]">
      <Link to="/app" className="flex items-center gap-2.5 px-5 pb-5 pt-6">
        <WeftMark />
        <span>
          <span className="block text-[15px] font-semibold leading-4 tracking-tight">weft</span>
          <span className="block text-[10px] leading-4 text-faint">version control for Blender</span>
        </span>
      </Link>

      <nav className="flex flex-col gap-0.5 px-3" aria-label="Projects">
        <p className="px-2 pb-1.5 pt-3 font-mono text-[10px] uppercase tracking-widest text-faint">
          projects
        </p>
        {projects.data?.map((p) => (
          <NavLink
            key={p.id}
            to={`/app/p/${p.id}`}
            data-testid={`nav-${p.name}`}
            className={({ isActive }) =>
              `relative flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm transition-colors ${
                isActive ? 'text-text' : 'text-dim hover:bg-panel-2/60 hover:text-text'
              }`
            }
          >
            {({ isActive }) => (
              <>
                {isActive && (
                  <motion.span
                    layoutId="nav-pill"
                    className="absolute inset-0 rounded-lg border border-line bg-panel-2"
                    transition={{ type: 'spring', stiffness: 420, damping: 34 }}
                  />
                )}
                <span className="relative flex h-4 w-4 items-center justify-center">
                  <span className="h-1.5 w-1.5 rounded-[3px] bg-accent/70" />
                </span>
                <span className="relative truncate">{p.name}</span>
                <span className="relative ml-auto font-mono text-[10px] text-faint">{p.assetCount}</span>
              </>
            )}
          </NavLink>
        ))}
        {projects.loading && (
          <span className="px-2.5 py-2 text-xs text-faint">loading…</span>
        )}
      </nav>

      <div className="mt-auto px-3 pb-5">
        {user && (
          <div className="mb-2 flex items-center gap-2.5 rounded-lg border border-line bg-panel/60 px-3 py-2.5" data-testid="sidebar-user">
            <span className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-accent/15 font-mono text-xs font-semibold text-accent">
              {user.displayName.slice(0, 1).toUpperCase()}
            </span>
            <span className="min-w-0 flex-1 leading-tight">
              <span className="block truncate text-[11px] font-medium text-text">{user.displayName}</span>
              <span className="block truncate text-[10px] text-faint">{user.email}</span>
            </span>
            <button
              onClick={() => void signOut()}
              title="Sign out"
              aria-label="Sign out"
              data-testid="logout-btn"
              className="grid h-7 w-7 shrink-0 place-items-center rounded-md text-faint transition-colors hover:bg-bad/10 hover:text-bad"
            >
              ⏻
            </button>
          </div>
        )}
      </div>
    </aside>
  )
}
