import { createContext, useContext, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, getToken, setTeamId, setToken } from '../api/client'

interface AuthContextValue {
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(!!getToken())
  const queryClient = useQueryClient()

  async function login(email: string, password: string) {
    const res = await api.post('/api/login', { email, password })
    // A different account may have just been active in this tab (logout -> log
    // back in as someone else) — never let cached team/roster data from that
    // account bleed into this one, even for an instant.
    setTeamId(null)
    queryClient.clear()
    setToken(res.data.accessToken)
    setIsAuthenticated(true)
  }

  async function register(email: string, password: string) {
    await api.post('/api/register', { email, password })
    await login(email, password)
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
