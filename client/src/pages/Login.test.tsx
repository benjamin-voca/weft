import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { Login } from './Login'
import { ToastHost } from '../components/ui'

// The auth context is exercised end-to-end by the Playwright suite; here we
// unit-test the login form's own behavior against a controllable auth value.
const mockAuth = {
  user: null as { email: string } | null,
  loading: false,
  login: vi.fn(),
  logout: vi.fn(),
}

vi.mock('../auth', () => ({
  useAuth: () => mockAuth,
}))

describe('Login', () => {
  beforeEach(() => {
    mockAuth.login.mockReset()
    mockAuth.login.mockImplementation(async () => {})
  })

  it('collects credentials and calls login, then succeeds silently', async () => {
    render(
      <MemoryRouter>
        <ToastHost>
          <Login />
        </ToastHost>
      </MemoryRouter>,
    )
    fireEvent.change(screen.getByTestId('login-email'), { target: { value: 'admin@example.com' } })
    fireEvent.change(screen.getByTestId('login-password'), { target: { value: 'admin' } })
    fireEvent.click(screen.getByTestId('login-submit'))

    await vi.waitFor(() => expect(mockAuth.login).toHaveBeenCalledWith('admin@example.com', 'admin'))
    expect(screen.queryByTestId('login-error')).not.toBeInTheDocument()
  })

  it('surfaces bad credentials as an inline error', async () => {
    mockAuth.login.mockRejectedValue(new Error('Invalid email or password'))
    render(
      <MemoryRouter>
        <ToastHost>
          <Login />
        </ToastHost>
      </MemoryRouter>,
    )
    fireEvent.change(screen.getByTestId('login-email'), { target: { value: 'admin@example.com' } })
    fireEvent.change(screen.getByTestId('login-password'), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByTestId('login-submit'))

    await vi.waitFor(() => expect(screen.getByTestId('login-error')).toHaveTextContent('Invalid email or password'))
  })

  it('autofills the demo credentials', () => {
    render(
      <MemoryRouter>
        <ToastHost>
          <Login />
        </ToastHost>
      </MemoryRouter>,
    )
    fireEvent.click(screen.getByTestId('login-autofill'))
    expect(screen.getByTestId('login-email')).toHaveValue('admin@example.com')
    expect(screen.getByTestId('login-password')).toHaveValue('admin')
  })

  it('disables submission while a sign-in is in flight', async () => {
    let resolveLogin: () => void = () => {}
    mockAuth.login.mockImplementation(() => new Promise<void>((r) => { resolveLogin = r }))
    render(
      <MemoryRouter>
        <ToastHost>
          <Login />
        </ToastHost>
      </MemoryRouter>,
    )
    fireEvent.change(screen.getByTestId('login-email'), { target: { value: 'admin@example.com' } })
    fireEvent.change(screen.getByTestId('login-password'), { target: { value: 'admin' } })
    fireEvent.click(screen.getByTestId('login-submit'))
    await vi.waitFor(() => expect(screen.getByTestId('login-submit')).toBeDisabled())
    resolveLogin()
  })
})
