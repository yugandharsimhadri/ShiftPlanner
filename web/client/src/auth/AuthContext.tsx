import { createContext, useContext, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, getToken, setTeamId, setToken } from '../api/client'
import { loginWithPhone, registerAccount } from '../api/endpoints'

interface AuthContextValue {
  isAuthenticated: boolean
  // identifier can be an email or a phone number — login is optional per team
  // member, and whichever they registered can be used to sign back in.
  login: (identifier: string, password: string) => Promise<void>
  register: (identifier: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function isEmail(identifier: string): boolean {
  return identifier.includes('@')
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(!!getToken())
  const queryClient = useQueryClient()

  async function login(identifier: string, password: string) {
    const trimmed = identifier.trim()
    const accessToken = isEmail(trimmed)
      ? (await api.post('/api/login', { email: trimmed, password })).data.accessToken
      : (await loginWithPhone(trimmed, password)).accessToken

    // A different account may have just been active in this tab (logout -> log
    // back in as someone else) — never let cached team/roster data from that
    // account bleed into this one, even for an instant.
    setTeamId(null)
    queryClient.clear()
    setToken(accessToken)
    setIsAuthenticated(true)
  }

  async function register(identifier: string, password: string) {
    const trimmed = identifier.trim()
    if (isEmail(trimmed)) {
      await registerAccount({ email: trimmed, password })
    } else {
      await registerAccount({ phone: trimmed, password })
    }
    await login(trimmed, password)
  }

  function logout() {
    setToken(null)
    setTeamId(null)
    queryClient.clear()
    setIsAuthenticated(false)
  }

  return (
    <AuthContext.Provider value={{ isAuthenticated, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within AuthProvider')
  return ctx
}
