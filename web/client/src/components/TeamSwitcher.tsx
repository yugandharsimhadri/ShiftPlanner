import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTeam } from '../team/TeamContext'
import './TeamSwitcher.css'

export default function TeamSwitcher() {
  const { teams, currentTeam, switchTeam } = useTeam()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [])

  if (!currentTeam) return null

  return (
    <div className="team-switcher" ref={ref}>
      <button className="team-switcher-trigger" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        <span className="team-switcher-name">{currentTeam.name}</span>
        <span className={`badge-role${currentTeam.role === 'Admin' ? ' role-admin' : ''}`}>{currentTeam.role}</span>
        <span className="team-switcher-chevron">⌄</span>
      </button>

      {open && (
        <div className="team-switcher-menu">
          <div className="team-switcher-label">Your teams</div>
          {teams.map((team) => (
            <button
              key={team.id}
              className={`team-switcher-option${team.id === currentTeam.id ? ' current' : ''}`}
              onClick={() => {
                setOpen(false)
                if (team.id !== currentTeam.id) switchTeam(team.id)
              }}
            >
              <span>{team.name}</span>
              <span className={`badge-role${team.role === 'Admin' ? ' role-admin' : ''}`}>{team.role}</span>
            </button>
          ))}
          <button
            className="team-switcher-create"
            onClick={() => {
              setOpen(false)
              navigate('/create-team')
            }}
          >
            + Create new team
          </button>
        </div>
      )}
    </div>
  )
}
