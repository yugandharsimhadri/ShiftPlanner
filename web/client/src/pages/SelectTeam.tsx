import { Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useTeam } from '../team/TeamContext'
import './Onboarding.css'

export default function SelectTeam() {
  const { isAuthenticated, logout } = useAuth()
  const { teams, switchTeam } = useTeam()

  if (!isAuthenticated) return <Navigate to="/login" replace />

  return (
    <div className="onboarding-screen">
      <div className="onboarding-glow" aria-hidden="true" />
      <div className="onboarding-card card">
        <p className="onboarding-eyebrow">Choose a team</p>
        <h1>Which team are you working in?</h1>
        <p className="onboarding-sub">
          Your account is a member of more than one team. Each one's roster, employees, and settings are
          completely independent — you can switch anytime from the header.
        </p>

        <div className="team-pick-list">
          {teams.map((team) => (
            <button key={team.id} className="team-pick-item" onClick={() => switchTeam(team.id)}>
              <span className="team-pick-name">{team.name}</span>
              <span className={`badge-role${team.role === 'Admin' ? ' role-admin' : ''}`}>{team.role}</span>
            </button>
          ))}
        </div>

        <div className="onboarding-footer">
          <button type="button" onClick={logout}>
            Sign out
          </button>
        </div>
      </div>
    </div>
  )
}
