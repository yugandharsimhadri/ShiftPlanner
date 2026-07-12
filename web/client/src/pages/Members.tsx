import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  addMember,
  getEmployees,
  getMe,
  getMembers,
  linkMemberEmployee,
  removeMember,
  setCoLead,
  transferLead,
  updateMemberRole,
} from '../api/endpoints'
import { useTeam } from '../team/TeamContext'
import type { TeamRole } from '../types'
import './Members.css'

const ROLES: TeamRole[] = ['Viewer', 'Editor', 'Admin']

export default function Members() {
  const { currentRole, currentTeam } = useTeam()
  const isAdmin = currentRole === 'Admin'
  const queryClient = useQueryClient()

  const { data: members, isLoading } = useQuery({
    queryKey: ['members', currentTeam?.id],
    queryFn: getMembers,
    enabled: !!currentTeam,
  })

  const { data: employees } = useQuery({
    queryKey: ['employees'],
    queryFn: getEmployees,
    enabled: !!currentTeam,
  })

  const { data: me } = useQuery({
    queryKey: ['me', currentTeam?.id],
    queryFn: getMe,
    enabled: !!currentTeam,
  })
  const isCurrentUserLead = me?.isTeamLead === true

  const [email, setEmail] = useState('')
  const [role, setRole] = useState<TeamRole>('Editor')
  const [error, setError] = useState<string | null>(null)

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['members', currentTeam?.id] })
    queryClient.invalidateQueries({ queryKey: ['me', currentTeam?.id] })
  }

  const addMutation = useMutation({
    mutationFn: addMember,
    onSuccess: () => {
      setEmail('')
      setError(null)
      invalidate()
    },
    onError: () => setError('Could not add that member. Check the email address.'),
  })
  const roleMutation = useMutation({ mutationFn: (p: { id: number; role: TeamRole }) => updateMemberRole(p.id, p.role), onSuccess: invalidate })
  const linkMutation = useMutation({
    mutationFn: (p: { id: number; employeeId: string | null }) => linkMemberEmployee(p.id, p.employeeId),
    onSuccess: invalidate,
  })
  const removeMutation = useMutation({ mutationFn: removeMember, onSuccess: invalidate })
  const transferLeadMutation = useMutation({ mutationFn: transferLead, onSuccess: invalidate })
  const coLeadMutation = useMutation({ mutationFn: (p: { id: number; isCoLead: boolean }) => setCoLead(p.id, p.isCoLead), onSuccess: invalidate })

  return (
    <div className="members-page">
      <div className="page-heading">
        <h2>Team members</h2>
      </div>
      <p className="page-sub">
        {isAdmin
          ? 'Add teammates by email. They’ll land here automatically the moment they sign in with a matching account — even if that email already belongs to a different team elsewhere.'
          : 'Everyone with access to this team’s roster, and what they can do with it.'}
      </p>

      {isAdmin && (
        <div className="card member-invite">
          <div className="field-row member-invite-row">
            <div className="field">
              <label htmlFor="member-email">Email</label>
              <input
                id="member-email"
                type="email"
                placeholder="teammate@company.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div className="field member-invite-role">
              <label htmlFor="member-role">Access</label>
              <select id="member-role" value={role} onChange={(e) => setRole(e.target.value as TeamRole)}>
                <option value="Viewer">Viewer — read only</option>
                <option value="Editor">Editor — can edit</option>
                <option value="Admin">Admin — full control</option>
              </select>
            </div>
            <button
              className="btn member-invite-btn"
              disabled={!email.trim() || addMutation.isPending}
              onClick={() => addMutation.mutate({ email: email.trim(), role })}
            >
              {addMutation.isPending ? 'Adding…' : 'Add member'}
            </button>
          </div>
          {error && <p className="error-text">{error}</p>}
        </div>
      )}

      {isLoading && <div className="empty-state">Loading members…</div>}

      {members && (
        <div className="card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Email</th>
                <th>Access</th>
                <th>Status</th>
                <th>Linked employee</th>
                {isAdmin && <th></th>}
              </tr>
            </thead>
            <tbody>
              {members.map((m) => (
                <tr key={m.id}>
                  <td>{m.email}</td>
                  <td>
                    <div className="role-cell">
                      {isAdmin ? (
                        <select
                          value={m.role}
                          onChange={(e) => roleMutation.mutate({ id: m.id, role: e.target.value as TeamRole })}
                          className="role-select"
                          disabled={m.isTeamLead}
                          title={m.isTeamLead ? 'Transfer the lead to someone else before changing this role.' : undefined}
                        >
                          {ROLES.map((r) => (
                            <option key={r} value={r}>
                              {r}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <span className={`badge-role${m.role === 'Admin' ? ' role-admin' : ''}`}>{m.role}</span>
                      )}
                      {m.isTeamLead && <span className="badge-lead">Lead</span>}
                      {m.isCoLead && <span className="badge-colead">Co-Lead</span>}
                    </div>
                  </td>
                  <td>
                    <span className={`status-pill${m.status === 'Active' ? ' status-active' : ''}`}>
                      {m.status === 'Active' ? 'Active' : 'Invited — awaiting first sign-in'}
                    </span>
                  </td>
                  <td>
                    {isAdmin ? (
                      <select
                        className="role-select"
                        value={m.employeeId ?? ''}
                        onChange={(e) => linkMutation.mutate({ id: m.id, employeeId: e.target.value || null })}
                      >
                        <option value="">Not linked</option>
                        {employees?.map((emp) => (
                          <option key={emp.id} value={emp.id}>
                            {emp.name} ({emp.code})
                          </option>
                        ))}
                      </select>
                    ) : (
                      <span className="field-hint">
                        {employees?.find((emp) => emp.id === m.employeeId)?.name ?? '—'}
                      </span>
                    )}
                  </td>
                  {isAdmin && (
                    <td className="row-actions">
                      {isCurrentUserLead && !m.isTeamLead && m.status === 'Active' && (
                        <button className="btn-ghost" onClick={() => transferLeadMutation.mutate(m.id)}>
                          Make lead
                        </button>
                      )}
                      {isCurrentUserLead && !m.isTeamLead && m.status === 'Active' && (
                        <button
                          className="btn-ghost"
                          onClick={() => coLeadMutation.mutate({ id: m.id, isCoLead: !m.isCoLead })}
                        >
                          {m.isCoLead ? 'Remove co-lead' : 'Make co-lead'}
                        </button>
                      )}
                      {!m.isTeamLead && (
                        <button className="btn-danger" onClick={() => removeMutation.mutate(m.id)}>
                          Remove
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
              {members.length === 0 && (
                <tr>
                  <td colSpan={isAdmin ? 5 : 4} className="empty-state">
                    No members yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          {isAdmin && (
            <p className="page-sub" style={{ padding: '0 20px 18px' }}>
              Linking a member to an employee lets them see their own shifts in the mobile app without typing in a
              code.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
