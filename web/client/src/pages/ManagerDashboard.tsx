import { useQuery } from '@tanstack/react-query'
import { getManagerAvailability } from '../api/endpoints'
import AvailabilityList from '../components/AvailabilityList'
import './ManagerDashboard.css'

const REFRESH_MS = 20000

export default function ManagerDashboard() {
  const { data: teams, isLoading } = useQuery({
    queryKey: ['manager', 'availability'],
    queryFn: getManagerAvailability,
    refetchInterval: REFRESH_MS,
  })

  return (
    <div className="manager-dashboard-page">
      <div className="page-heading">
        <h2>Manager view</h2>
      </div>
      <p className="page-sub">Live availability across every team you oversee, irrespective of what's on each team's roster.</p>

      {isLoading && <div className="empty-state">Loading…</div>}

      {teams && teams.length === 0 && (
        <div className="empty-state">You don't manage any teams yet.</div>
      )}

      {teams?.map((team) => (
        <section key={team.teamId} className="manager-team-section">
          <div className="manager-team-heading">
            <h3>{team.teamName}</h3>
            <span className="track-count mono">
              {team.members.filter((m) => m.isAvailable).length} / {team.members.length} available
            </span>
          </div>
          <div className="card availability-card">
            <AvailabilityList members={team.members} />
          </div>
        </section>
      ))}
    </div>
  )
}
