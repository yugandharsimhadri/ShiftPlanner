import { NavLink, Outlet, Navigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { getManagerTeams } from '../api/endpoints'
import { useAuth } from '../auth/AuthContext'
import { useTeam } from '../team/TeamContext'
import TeamSwitcher from './TeamSwitcher'
import './Layout.css'

export default function Layout() {
  const { isAuthenticated, logout } = useAuth()
  const { teams, isLoading, currentTeam } = useTeam()
  const { data: managerTeams } = useQuery({ queryKey: ['manager', 'teams'], queryFn: getManagerTeams, enabled: isAuthenticated })

  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (isLoading) return <div className="app-loading">Loading your teams…</div>
  if (teams.length === 0) return <Navigate to="/create-team" replace />
  if (!currentTeam) return <Navigate to="/select-team" replace />

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <div className="app-brand">
          <span className="app-brand-mark" />
          <h1>ShiftPlanner</h1>
        </div>
        <TeamSwitcher />
        <nav className="app-nav">
          <NavLink to="/" end className={({ isActive }) => (isActive ? 'active' : '')}>
            Roster
          </NavLink>
          <NavLink to="/live" className={({ isActive }) => (isActive ? 'active' : '')}>
            Live
          </NavLink>
          {managerTeams && managerTeams.length > 0 && (
            <NavLink to="/manager" className={({ isActive }) => (isActive ? 'active' : '')}>
              Manager view
            </NavLink>
          )}
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
        <NavLink to="/profile" className="app-profile-link">
          My profile
        </NavLink>
        <button className="app-logout" onClick={logout}>
          Log out
        </button>
      </aside>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
