import { NavLink, Outlet, Navigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useTeam } from '../team/TeamContext'
import TeamSwitcher from './TeamSwitcher'
import './Layout.css'

export default function Layout() {
  const { isAuthenticated, logout } = useAuth()
  const { teams, isLoading, currentTeam } = useTeam()

  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (isLoading) return <div className="app-loading">Loading your teams…</div>
  if (teams.length === 0) return <Navigate to="/create-team" replace />
  if (!currentTeam) return <Navigate to="/select-team" replace />

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-brand">
          <span className="app-brand-mark" />
          <h1>ShiftPlanner</h1>
        </div>
        <nav className="app-nav">
          <NavLink to="/" end className={({ isActive }) => (isActive ? 'active' : '')}>
            Roster
          </NavLink>
          <NavLink to="/members" className={({ isActive }) => (isActive ? 'active' : '')}>
            Team Members
          </NavLink>
          <NavLink to="/settings" className={({ isActive }) => (isActive ? 'active' : '')}>
            Settings
          </NavLink>
          <NavLink to="/reports" className={({ isActive }) => (isActive ? 'active' : '')}>
            Reports
          </NavLink>
        </nav>
        <TeamSwitcher />
        <button className="btn-ghost" onClick={logout}>
          Log out
        </button>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
