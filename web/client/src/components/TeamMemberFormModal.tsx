import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  assignExistingPerson,
  createJobRole,
  createLocation,
  createSubtrack,
  createTeamMember,
  getJobRoles,
  getLocations,
  getMyTeams,
  getNextTeamMemberCode,
  updateTeamMember,
} from '../api/endpoints'
import type {
  EmployeeStatus,
  EmploymentType,
  TeamMember,
  TeamRole,
  Track,
  UnassignedPerson,
} from '../types'

interface Props {
  mode: 'create' | 'edit' | 'assign'
  member?: TeamMember
  assignPerson?: UnassignedPerson
  tracks: Track[]
  currentTeamId: number
  onClose: () => void
}

const ROLES: TeamRole[] = ['Viewer', 'Editor', 'Admin']

export default function TeamMemberFormModal({ mode, member, assignPerson, tracks, currentTeamId, onClose }: Props) {
  const queryClient = useQueryClient()

  const [name, setName] = useState(member?.name ?? assignPerson?.name ?? '')
  const [phone, setPhone] = useState(member?.phone ?? assignPerson?.phone ?? '')
  const [email, setEmail] = useState(member?.email ?? assignPerson?.email ?? '')
  const [notes, setNotes] = useState(member?.notes ?? '')
  const [code, setCode] = useState(member?.code ?? '')
  const [trackId, setTrackId] = useState<number | ''>(member?.trackId ?? '')
  const [subtrackId, setSubtrackId] = useState<number | ''>(member?.subtrackId ?? '')
  const [jobRoleId, setJobRoleId] = useState<number | ''>(member?.jobRoleId ?? '')
  const [locationId, setLocationId] = useState<number | ''>(member?.locationId ?? '')
  const [employmentType, setEmploymentType] = useState<EmploymentType>(member?.employmentType ?? 'FullTime')
  const [joinDate, setJoinDate] = useState(member?.joinDate ?? new Date().toISOString().slice(0, 10))
  const [status, setStatus] = useState<EmployeeStatus>(member?.status ?? 'Active')
  const [accessRole, setAccessRole] = useState<TeamRole>(member?.accessRole ?? 'Viewer')
  const [teamIds, setTeamIds] = useState<number[]>(mode === 'create' ? [currentTeamId] : [])

  const [addingSubtrack, setAddingSubtrack] = useState(false)
  const [newSubtrackName, setNewSubtrackName] = useState('')
  const [addingJobRole, setAddingJobRole] = useState(false)
  const [newJobRoleName, setNewJobRoleName] = useState('')
  const [addingLocation, setAddingLocation] = useState(false)
  const [newLocationName, setNewLocationName] = useState('')

  const selectedTrack = tracks.find((t) => t.id === trackId)

  const { data: jobRoles } = useQuery({ queryKey: ['job-roles'], queryFn: getJobRoles })
  const { data: locations } = useQuery({ queryKey: ['locations'], queryFn: getLocations })

  useEffect(() => {
    // If the chosen track changes, drop a subtrack selection that no longer applies.
    if (subtrackId && !selectedTrack?.subtracks.some((s) => s.id === subtrackId)) {
      setSubtrackId('')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trackId])

  const { data: myTeams } = useQuery({ queryKey: ['teams', 'mine'], queryFn: getMyTeams, enabled: mode === 'create' })
  const adminTeams = (myTeams ?? []).filter((t) => t.role === 'Admin')

  const { data: suggestedCode } = useQuery({
    queryKey: ['team-members', 'next-code'],
    queryFn: getNextTeamMemberCode,
    enabled: mode !== 'edit',
    staleTime: Infinity,
  })
  useEffect(() => {
    if (mode !== 'edit' && !code && suggestedCode) setCode(suggestedCode)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [suggestedCode])

  const addSubtrackMutation = useMutation({
    mutationFn: createSubtrack,
    onSuccess: (sub) => {
      queryClient.invalidateQueries({ queryKey: ['tracks'] }).then(() => setSubtrackId(sub.id))
      setNewSubtrackName('')
      setAddingSubtrack(false)
    },
  })

  const addJobRoleMutation = useMutation({
    mutationFn: createJobRole,
    onSuccess: (role) => {
      queryClient.invalidateQueries({ queryKey: ['job-roles'] }).then(() => setJobRoleId(role.id))
      setNewJobRoleName('')
      setAddingJobRole(false)
    },
  })

  const addLocationMutation = useMutation({
    mutationFn: createLocation,
    onSuccess: (loc) => {
      queryClient.invalidateQueries({ queryKey: ['locations'] }).then(() => setLocationId(loc.id))
      setNewLocationName('')
      setAddingLocation(false)
    },
  })

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ['team-members'] })
    queryClient.invalidateQueries({ queryKey: ['unassigned-candidates'] })
    queryClient.invalidateQueries({ queryKey: ['roster'] })
  }

  const createMutation = useMutation({
    mutationFn: createTeamMember,
    onSuccess: () => { invalidateAll(); onClose() },
  })
  const updateMutation = useMutation({
    mutationFn: (payload: Parameters<typeof updateTeamMember>[1]) => updateTeamMember(member!.id, payload),
    onSuccess: () => { invalidateAll(); onClose() },
  })
  const assignMutation = useMutation({
    mutationFn: assignExistingPerson,
    onSuccess: () => { invalidateAll(); onClose() },
  })

  const busy = createMutation.isPending || updateMutation.isPending || assignMutation.isPending
  const error = createMutation.isError || updateMutation.isError || assignMutation.isError

  function toggleTeam(teamId: number) {
    setTeamIds((ids) => (ids.includes(teamId) ? ids.filter((id) => id !== teamId) : [...ids, teamId]))
  }

  function handleSubmit() {
    if (mode === 'edit') {
      updateMutation.mutate({
        name, phone, email: email || null, notes: notes || null,
        code, trackId: trackId || null, subtrackId: subtrackId || null,
        jobRoleId: jobRoleId || null, locationId: locationId || null,
        employmentType, joinDate, status, accessRole,
      })
      return
    }
    if (mode === 'assign') {
      assignMutation.mutate({
        personId: assignPerson!.id, teamId: currentTeamId,
        code, trackId: trackId || null, subtrackId: subtrackId || null,
        jobRoleId: jobRoleId || null, locationId: locationId || null,
        employmentType, joinDate, accessRole,
      })
      return
    }
    createMutation.mutate({
      name, phone, email: email || null, notes: notes || null,
      code, trackId: trackId || null, subtrackId: subtrackId || null,
      jobRoleId: jobRoleId || null, locationId: locationId || null,
      employmentType, joinDate, status, accessRole, teamIds,
    })
  }

  const title = mode === 'edit' ? `Edit ${member!.name}` : mode === 'assign' ? `Add ${assignPerson!.name} to this team` : 'Add team member'
  const namesLocked = mode === 'assign'
  const needsTeamAssignment = mode !== 'edit'
  const codeRequired = mode === 'edit' || mode === 'assign' || teamIds.length > 0

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal" style={{ width: 600 }}>
        <h2>{title}</h2>

        <div className="field-row">
          <div className="field">
            <label htmlFor="tm-name">Name *</label>
            <input id="tm-name" value={name} onChange={(e) => setName(e.target.value)} readOnly={namesLocked} required />
          </div>
          <div className="field">
            <label htmlFor="tm-code">Code {needsTeamAssignment ? '(for this team)' : '*'}</label>
            <input id="tm-code" value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. EMP-004" required={needsTeamAssignment} />
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="tm-phone">Phone</label>
            <input id="tm-phone" value={phone} onChange={(e) => setPhone(e.target.value)} readOnly={namesLocked} />
          </div>
          <div className="field">
            <label htmlFor="tm-email">Email</label>
            <input id="tm-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} readOnly={namesLocked} />
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="tm-role-title">Role / title</label>
            <div className="inline-picker">
              <select id="tm-role-title" value={jobRoleId} onChange={(e) => setJobRoleId(e.target.value ? Number(e.target.value) : '')}>
                <option value="">None</option>
                {jobRoles?.map((r) => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
              {!addingJobRole && (
                <button type="button" className="btn-ghost inline-add-toggle" onClick={() => setAddingJobRole(true)}>
                  + New role
                </button>
              )}
            </div>
            {addingJobRole && (
              <div className="inline-add-row">
                <input
                  autoFocus
                  placeholder="New role, e.g. Cashier"
                  value={newJobRoleName}
                  onChange={(e) => setNewJobRoleName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && newJobRoleName.trim()) {
                      addJobRoleMutation.mutate({ name: newJobRoleName.trim() })
                    }
                  }}
                />
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={!newJobRoleName.trim() || addJobRoleMutation.isPending}
                  onClick={() => addJobRoleMutation.mutate({ name: newJobRoleName.trim() })}
                >
                  {addJobRoleMutation.isPending ? 'Adding…' : 'Add'}
                </button>
                <button type="button" className="btn-ghost" onClick={() => { setAddingJobRole(false); setNewJobRoleName('') }}>
                  Cancel
                </button>
              </div>
            )}
          </div>
          <div className="field">
            <label htmlFor="tm-location">Location</label>
            <div className="inline-picker">
              <select id="tm-location" value={locationId} onChange={(e) => setLocationId(e.target.value ? Number(e.target.value) : '')}>
                <option value="">None</option>
                {locations?.map((l) => (
                  <option key={l.id} value={l.id}>{l.name}</option>
                ))}
              </select>
              {!addingLocation && (
                <button type="button" className="btn-ghost inline-add-toggle" onClick={() => setAddingLocation(true)}>
                  + New city
                </button>
              )}
            </div>
            {addingLocation && (
              <div className="inline-add-row">
                <input
                  autoFocus
                  placeholder="New city"
                  value={newLocationName}
                  onChange={(e) => setNewLocationName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && newLocationName.trim()) {
                      addLocationMutation.mutate({ name: newLocationName.trim() })
                    }
                  }}
                />
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={!newLocationName.trim() || addLocationMutation.isPending}
                  onClick={() => addLocationMutation.mutate({ name: newLocationName.trim() })}
                >
                  {addLocationMutation.isPending ? 'Adding…' : 'Add'}
                </button>
                <button type="button" className="btn-ghost" onClick={() => { setAddingLocation(false); setNewLocationName('') }}>
                  Cancel
                </button>
              </div>
            )}
          </div>
        </div>

        <div className="field">
          <label htmlFor="tm-track">Track</label>
          <select id="tm-track" value={trackId} onChange={(e) => setTrackId(e.target.value ? Number(e.target.value) : '')}>
            <option value="">Unassigned</option>
            {tracks.map((t) => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="tm-subtrack">Subtrack</label>
          <div className="inline-picker">
            <select
              id="tm-subtrack"
              value={subtrackId}
              onChange={(e) => setSubtrackId(e.target.value ? Number(e.target.value) : '')}
              disabled={!selectedTrack}
            >
              <option value="">{selectedTrack ? 'None' : 'Pick a track first'}</option>
              {selectedTrack?.subtracks.map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
            {selectedTrack && !addingSubtrack && (
              <button type="button" className="btn-ghost inline-add-toggle" onClick={() => setAddingSubtrack(true)}>
                + New subtrack
              </button>
            )}
          </div>
          {selectedTrack && addingSubtrack && (
            <div className="inline-add-row">
              <input
                autoFocus
                placeholder={`New subtrack under ${selectedTrack.name}`}
                value={newSubtrackName}
                onChange={(e) => setNewSubtrackName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && newSubtrackName.trim()) {
                    addSubtrackMutation.mutate({ trackId: selectedTrack.id, name: newSubtrackName.trim() })
                  }
                }}
              />
              <button
                type="button"
                className="btn-secondary"
                disabled={!newSubtrackName.trim() || addSubtrackMutation.isPending}
                onClick={() => addSubtrackMutation.mutate({ trackId: selectedTrack.id, name: newSubtrackName.trim() })}
              >
                {addSubtrackMutation.isPending ? 'Adding…' : 'Add'}
              </button>
              <button type="button" className="btn-ghost" onClick={() => { setAddingSubtrack(false); setNewSubtrackName('') }}>
                Cancel
              </button>
            </div>
          )}
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="tm-type">Employment type</label>
            <select id="tm-type" value={employmentType} onChange={(e) => setEmploymentType(e.target.value as EmploymentType)}>
              <option value="FullTime">Full-time</option>
              <option value="PartTime">Part-time</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="tm-join">Join date</label>
            <input id="tm-join" type="date" value={joinDate} onChange={(e) => setJoinDate(e.target.value)} />
          </div>
        </div>

        <div className="field-row">
          {mode === 'edit' && (
            <div className="field">
              <label htmlFor="tm-status">Employment status</label>
              <select id="tm-status" value={status} onChange={(e) => setStatus(e.target.value as EmployeeStatus)}>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
              </select>
            </div>
          )}
          <div className="field">
            <label htmlFor="tm-access">Access on this team</label>
            <select id="tm-access" value={accessRole} onChange={(e) => setAccessRole(e.target.value as TeamRole)}>
              {ROLES.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
          </div>
        </div>

        {mode === 'create' && (
          <div className="field">
            <label>Teams (optional — leave unchecked to just record this person for now)</label>
            <div className="team-checklist">
              {adminTeams.map((t) => (
                <label key={t.id} className="team-checklist-item">
                  <input type="checkbox" checked={teamIds.includes(t.id)} onChange={() => toggleTeam(t.id)} />
                  {t.name}
                </label>
              ))}
              {adminTeams.length === 0 && <p className="field-hint">You're not an Admin on any team yet.</p>}
            </div>
          </div>
        )}

        {mode !== 'edit' && (
          <div className="field">
            <label htmlFor="tm-notes">Notes</label>
            <textarea id="tm-notes" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
        )}

        {error && <p className="error-text">Could not save. Check the fields and try again.</p>}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button
            className="btn"
            onClick={handleSubmit}
            disabled={!name.trim() || (codeRequired && !code.trim()) || busy}
          >
            {busy ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  )
}
