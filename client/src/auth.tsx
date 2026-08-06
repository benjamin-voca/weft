import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { api } from './api/client'
import type { AuthUser } from './api/client'

interface AuthState {
  user: AuthUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthCtx = createContext<AuthState>({
  user: null, loading: true, login: async () => {}, logout: async () => {},
})

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ user: AuthUser | null; loading: boolean }>({
    user: null,
    loading: true,
  })

  useEffect(() => {
    api.me()
      .then((user) => setState((s) => ({ ...s, user })))
      .catch(() => setState((s) => ({ ...s, user: null })))
      .finally(() => setState((s) => ({ ...s, loading: false })))
  }, [])

  const login = async (email: string, password: string) => {
    const user = await api.login(email, password)
    setState((s) => ({ ...s, user }))
  }

  const logout = async () => {
    await api.logout()
    setState((s) => ({ ...s, user: null }))
  }

  return <AuthCtx.Provider value={{ ...state, login, logout }}>{children}</AuthCtx.Provider>
}

export function useAuth() {
  return useContext(AuthCtx)
}
