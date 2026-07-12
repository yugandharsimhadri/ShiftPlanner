import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getMe,
  getTeamMembers,
  getTracks,
  getUnassignedCandidates,
  removeMember,
  setCoLead,
  transferLead,
  updateMemberAccessRole,
} from '../api/endpoints'
import TeamMemberFormModal from '../components/TeamMemberFormModal'
import { useTeam } from '../team/TeamContext'
import type { TeamMember, TeamRole, UnassignedPerson } from '../types'
import './TeamMembers.css'

const ROLES: TeamRole[] = ['Viewer', 'Editor', 'Admin']

export default function TeamMembers() {
  const { currentRole, currentTeam } = useTeam()
  const isAdmin = currentRole === 'Admin'
  const queryClient = useQueryClient()

  const { data: members, isLoading } = useQuery({ queryKey: ['team-members', currentTeam?.id], queryFn: getTeamMembers, enabled: !!currentTeam })
  const { data: tracks } = useQuery({ queryKey: ['tracks'], queryFn: getTracks, enabled: !!currentTeam })
  const { data: unassigned } = useQuery({
    queryKey: ['unassigned-candidates', currentTeam?.id],
    queryFn: getUnassignedCandidates,
    enabled: !!currentTeam && isAdmin,
  })
  const { data: me } = useQuery({ queryKey: ['me', currentTeam?.id], queryFn: getMe, enabled: !!currentTeam })
  const isCurrentUserLead = me?.isTeamLead === true

  const [editing, setEditing] = useState<TeamMember | 'new' | UnassignedPerson | null>(null)

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['team-members', currentTeam?.id] })
    queryClient.invalidateQueries({ queryKey: ['unassigned-candidates', currentTeam?.id] })
    queryClient.invalidateQueries({ queryKey: ['me', currentTeam?.id] })
    queryClient.invalidateQueries({ queryKey: ['roster'] })
  }

  const roleMutation = useMutation({
    mutationFn: (p: { id: number; role: TeamRole }) => updateMemberAccessRole(p.id, p.role),
    onSuccess: invalidate,
  })
  const removeMutation = useMutation({ mutationFn: removeMember, onSuccess: invalidate })
  const transferLeadMutation = useMutation({ mutationFn: transferLead, onSuccess: invalidate })
  const coLeadMutation = useMutation({ mutationFn: (p: { id: number; isCoLead: boolean }) => setCoLead(p.id, p.isCoLead), onSuccess: invalidate })

  function handleRemove(m: TeamMember) {
    if (window.confirm(`Remove ${m.name} (${m.code}) from this team? Their roster history on this team is also deleted.`)) {
      removeMutation.mutate(m.id)
    }
  }

  // "new" mode needs a modal even before we know which form fields apply, so a
  // UnassignedPerson object routes to "assign" mode instead of "create" mode.
  const modalMode = editing === 'new' ? 'create' : editing && typeof editing === 'object' && 'accessRole' in editing ? 'edit' : editing ? 'assign' : null

  return (
    <div className="team-members-page">
      <div className="page-heading">
        <h2>Team members</h2>
      </div>
      <p className="page-sub">
        {isAdmin
          ? 'Everyone on this team — who has roster access and what they can do with it. Login is optional; someone can be recorded here and log in later with a matching email or phone number.'
          : 'Everyone on this team, and what they can do with the roster.'}
      </p>

      {isAdmin && (
        <div className="toolbar" style={{ marginBottom: 16 }}>
          <div className="toolbar-spacer" />
          <button className="btn" onClick={() => setEditing('new')} disabled={!tracks}>
            + Add team member
          </button>
        </div>
      )}

      {isAdmin && unassigned && unassigned.length > 0 && (
        <div className="card unassigned-panel">
          <h3>Not yet on this team</h3>
          <p className="field-hint">People you've added elsewhere — add any of them to this team too.</p>
          {unassigned.map((p) => (
            <div key={p.id} className="unassigned-row">
              <span>{p.name}{p.phone ? ` · ${p.phone}` : ''}</span>
              <button className="btn-secondary" onClick={() => setEditing(p)}>Add to this team</button>
            </div>
          ))}
        </div>
      )}

      {isLoading && <div className="empty-state">Loading team members…</div>}

      {members && (
        <div className="card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Track</th>
                <th>Location</th>
                <th>Role</th>
                <th>Access</th>
                <th>Status</th>
                {isAdmin && <th></th>}
              </tr>
            </thead>
            <tbody>
              {members.map((m) => (
                <tr key={m.id}>
                  <td className="mono">{m.code}</td>
                  <td>
                    <div>{m.name}</div>
                    <div className="field-hint">{m.phone}{!m.hasLogin ? ' · no login yet' : ''}</div>
                  </td>
                  <td>
                    {m.trackName && (
                      <span className="pill" style={{ background: 'var(--accent-soft)', color: 'var(--ink)' }}>
                        {m.trackName}{m.subtrackName ? ` / ${m.subtrackName}` : ''}
                      </span>
                    )}
                  </td>
                  <td>{m.location}</td>
                  <td>{m.roleTitle}</td>
                  <td>
                    <div className="role-cell">
                      {isAdmin ? (
                        <select
                          value={m.accessRole}
                          onChange={(e) => roleMutation.mutate({ id: m.id, role: e.target.value as TeamRole })}
                          className="role-select"
                          disabled={m.isTeamLead}
                          title={m.isTeamLead ? 'Transfer the lead to someone else before changing this role.' : undefined}
                        >
                          {ROLES.map((r) => (
                            <option key={r} value={r}>{r}</option>
                          ))}
                        </select>
                      ) : (
                        <span className={`badge-role${m.accessRole === 'Admin' ? ' role-admin' : ''}`}>{m.accessRole}</span>
                      )}
                      {m.isTeamLead && <span className="badge-lead">Lead</span>}
                      {m.isCoLead && <span className="badge-colead">Co-Lead</span>}
                    </div>
                  </td>
                  <td>
                    <span className={`badge${m.status === 'Active' ? ' badge-active' : ''}`}>{m.status}</span>
                  </td>
                  {isAdmin && (
                    <td className="row-actions">
                      <button className="btn-secondary" onClick={() => setEditing(m)}>Edit</button>
                      {isCurrentUserLead && !m.isTeamLead && (
                        <button className="btn-ghost" onClick={() => transferLeadMutation.mutate(m.id)}>Make lead</button>
                      )}
                      {isCurrentUserLead && !m.isTeamLead && (
                        <button className="btn-ghost" onClick={() => coLeadMutation.mutate({ id: m.id, isCoLead: !m.isCoLead })}>
                          {m.isCoLead ? 'Remove co-lead' : 'Make co-lead'}
                        </button>
                      )}
                      {!m.isTeamLead && (
                        <button className="btn-danger" onClick={() => handleRemove(m)}>Remove</button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
              {members.length === 0 && (
                <tr>
                  <td colSpan={isAdmin ? 8 : 7} className="empty-state">No team members yet.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {isAdmin && editing && tracks && currentTeam && modalMode && (
        <TeamMemberFormModal
          mode={modalMode}
          member={modalMode === 'edit' ? (editing as TeamMember) : undefined}
          assignPerson={modalMode === 'assign' ? (editing as UnassignedPerson) : undefined}
          tracks={tracks}
          currentTeamId={currentTeam.id}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  )
}
