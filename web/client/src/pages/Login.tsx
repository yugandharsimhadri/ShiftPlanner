import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import './Login.css'

export default function Login() {
  const { isAuthenticated, login, register } = useAuth()
  const navigate = useNavigate()
  const [mode, setMode] = useState<'signin' | 'register'>('signin')
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (isAuthenticated) return <Navigate to="/" replace />

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      if (mode === 'register') await register(identifier, password)
      else await login(identifier, password)
      navigate('/', { replace: true })
    } catch (err: any) {
      if (mode === 'register') {
        const messages = err?.response?.data?.message
          ?? (err?.response?.data?.errors ? Object.values(err.response.data.errors).flat().join(' ') : null)
        setError(messages || 'Could not create that account. It may already be registered.')
      } else {
        setError('Invalid email/phone or password.')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-screen">
      <div className="login-glow" aria-hidden="true" />
      <form className="login-card card" onSubmit={handleSubmit}>
        <div className="login-brand">
          <span className="app-brand-mark" />
          <h1>ShiftPlanner</h1>
        </div>
        <p className="login-sub">
          {mode === 'signin' ? 'Sign in to see your team’s roster.' : 'Create an account to start a team.'}
        </p>

        <div className="login-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'signin'}
            className={mode === 'signin' ? 'active' : ''}
            onClick={() => { setMode('signin'); setError(null) }}
          >
            Sign in
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={mode === 'register'}
            className={mode === 'register' ? 'active' : ''}
            onClick={() => { setMode('register'); setError(null) }}
          >
            Create account
          </button>
        </div>

        <div className="field">
          <label htmlFor="identifier">Email or phone number</label>
          <input
            id="identifier"
            type="text"
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            autoComplete="username"
            placeholder="you@company.com or your phone number"
            required
          />
        </div>
        <div className="field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete={mode === 'signin' ? 'current-password' : 'new-password'}
            placeholder={mode === 'register' ? 'At least 6 characters' : undefined}
            required
          />
        </div>

        {error && <p className="error-text">{error}</p>}

        <button className="btn login-submit" type="submit" disabled={loading}>
          {loading ? 'One moment…' : mode === 'signin' ? 'Sign in' : 'Create account'}
        </button>
      </form>
    </div>
  )
}
