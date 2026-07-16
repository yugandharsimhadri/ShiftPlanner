import type { TeamMemberAvailability } from '../types'
import './AvailabilityList.css'

interface Props {
  members: TeamMemberAvailability[]
}

function localTime(timezone: string | null): string | null {
  if (!timezone) return null
  try {
    return new Intl.DateTimeFormat('en-US', { timeZone: timezone, hour: 'numeric', minute: '2-digit' }).format(new Date())
  } catch {
    return null
  }
}

function availableFor(since: string | null): string | null {
  if (!since) return null
  const minutes = Math.max(0, Math.round((Date.now() - new Date(since).getTime()) / 60000))
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m`
  const hours = Math.floor(minutes / 60)
  const remainder = minutes % 60
  return remainder ? `${hours}h ${remainder}m` : `${hours}h`
}

export default function AvailabilityList({ members }: Props) {
  if (members.length === 0) {
    return <div className="empty-state">No active team members.</div>
  }

  const sorted = [...members].sort((a, b) => {
    if (a.isAvailable !== b.isAvailable) return a.isAvailable ? -1 : 1
    return a.name.localeCompare(b.name)
  })

  return (
    <div className="availability-list">
      {sorted.map((m) => (
        <div key={m.teamMemberId} className={`availability-row${m.isAvailable ? ' is-available' : ''}`}>
          <span className={`status-dot${m.isAvailable ? ' on' : ''}`} aria-hidden="true" />
          <div className="availability-who">
            <div className="availability-name">{m.name}</div>
            <div className="availability-meta mono">
              {m.code}
              {m.trackName ? ` · ${m.trackName}` : ''}
            </div>
          </div>
          <div className="availability-status">
            {m.isAvailable ? (
              <span className="availability-badge on">Available{availableFor(m.availableSince) ? ` · ${availableFor(m.availableSince)}` : ''}</span>
            ) : (
              <span className="availability-badge off">Not available</span>
            )}
          </div>
          <div className="availability-time mono">{localTime(m.timezone) ?? '—'}</div>
        </div>
      ))}
    </div>
  )
}
