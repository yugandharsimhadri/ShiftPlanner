import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useTeam } from '../team/TeamContext'
import './Onboarding.css'

export default function CreateTeam() {
  const { isAuthenticated, logout } = useAuth()
  const { createTeam, teams } = useTeam()
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (!isAuthenticated) return <Navigate to="/login" replace />

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await createTeam(name.trim())
      // createTeam hard-navigates on success; only reset loading on failure.
    } catch {
      setError('Could not create the team. Try a different name.')
      setLoading(false)
    }
  }

  return (
    <div className="onboarding-screen">
      <div className="onboarding-glow" aria-hidden="true" />
      <form className="onboarding-card card" onSubmit={handleSubmit}>
        <p className="onboarding-eyebrow">Step 1 of 1</p>
        <h1>Name your team</h1>
        <p className="onboarding-sub">
          You’ll be the Admin — able to add teammates, assign edit or view-only access, and manage the roster.
          Every team’s data stays completely separate.
        </p>

        <div className="field">
          <label htmlFor="team-name">Team name</label>
          <input
            id="team-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Riverside Clinic"
            autoFocus
            required
          />
        </div>

        {error && <p className="error-text">{error}</p>}

        <button className="btn onboarding-submit" type="submit" disabled={loading || !name.trim()}>
          {loading ? 'Creating…' : 'Create team'}
        </button>

        {teams.length === 0 && (
          <div className="onboarding-footer">
            <button type="button" onClick={logout}>
              Sign out
            </button>
          </div>
        )}
      </form>
    </div>
  )
}
