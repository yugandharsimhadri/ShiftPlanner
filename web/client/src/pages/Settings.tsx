import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createJobRole,
  createLocation,
  createShiftType,
  createSubtrack,
  createTrack,
  deleteJobRole,
  deleteLocation,
  deleteShiftType,
  deleteSubtrack,
  deleteTrack,
  getJobRoles,
  getLocations,
  getShiftTypes,
  getTeamManagers,
  getTeamSettings,
  getTracks,
  grantManager,
  revokeManager,
  searchPeopleByPhone,
  updateShiftType,
  updateTeamSettings,
  updateTrack,
} from '../api/endpoints'
import { useTeam } from '../team/TeamContext'
import { ALL_DAYS, type DayOfWeekName, type ShiftType, type Track } from '../types'
import './Settings.css'

export default function Settings() {
  return (
    <div className="settings-page">
      <TeamSettingsSection />
      <TracksSection />
      <LocationsSection />
      <JobRolesSection />
      <ManagersSection />
      <ShiftTypesSection />
    </div>
  )
}

function TeamSettingsSection() {
  const { currentRole } = useTeam()
  const isAdmin = currentRole === 'Admin'
  const queryClient = useQueryClient()
  const { data: settings } = useQuery({ queryKey: ['team-settings'], queryFn: getTeamSettings })

  const [name, setName] = useState('')
  const [orgName, setOrgName] = useState('')
  const [teamStrength, setTeamStrength] = useState('')
  const [shiftsCovered, setShiftsCovered] = useState('')
  const [offDays, setOffDays] = useState<DayOfWeekName[]>([])
  const [compOffsEnabled, setCompOffsEnabled] = useState(false)

  useEffect(() => {
    if (!settings) return
    setName(settings.name)
    setOrgName(settings.orgName ?? '')
    setTeamStrength(settings.teamStrength?.toString() ?? '')
    setShiftsCovered(settings.shiftsCovered ?? '')
    setOffDays(settings.defaultOffDays)
    setCompOffsEnabled(settings.compOffsEnabled)
  }, [settings])

  const saveMutation = useMutation({
    mutationFn: updateTeamSettings,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['team-settings'] })
      queryClient.invalidateQueries({ queryKey: ['teams', 'mine'] })
    },
  })

  const toggleDay = (day: DayOfWeekName) => {
    if (!isAdmin) return
    setOffDays((days) => (days.includes(day) ? days.filter((d) => d !== day) : [...days, day]))
  }

  const save = () =>
    saveMutation.mutate({
      name: name.trim(),
      orgName: orgName.trim() || null,
      teamStrength: teamStrength.trim() ? Number(teamStrength) : null,
      shiftsCovered: shiftsCovered.trim() || null,
      defaultOffDays: offDays,
      compOffsEnabled,
    })

  if (!settings) return null

  return (
    <section className="card settings-section">
      <h2>Team settings</h2>

      <div className="settings-readout">
        <span>Team members: <b>{settings.activeMemberCount}</b>{settings.teamStrength ? ` of ${settings.teamStrength} budgeted` : ''}</span>
        <span>Lead: <b>{settings.leadName ?? '—'}</b></span>
        <span>Co-lead: <b>{settings.coLeadName ?? 'none'}</b></span>
      </div>

      <div className="settings-field-grid">
        <div className="settings-field">
          <label htmlFor="team-name">Team name</label>
          <input id="team-name" value={name} onChange={(e) => setName(e.target.value)} readOnly={!isAdmin} placeholder="e.g. Store 42 Ops" />
        </div>
        <div className="settings-field">
          <label htmlFor="org-name">Organization name</label>
          <input id="org-name" value={orgName} onChange={(e) => setOrgName(e.target.value)} readOnly={!isAdmin} placeholder="e.g. Acme Retail" />
        </div>
        <div className="settings-field">
          <label htmlFor="team-strength">Team strength (budgeted headcount)</label>
          <input
            id="team-strength"
            type="number"
            min={0}
            value={teamStrength}
            onChange={(e) => setTeamStrength(e.target.value)}
            readOnly={!isAdmin}
            placeholder="Informational only"
          />
        </div>
        <div className="settings-field">
          <label htmlFor="shifts-covered">Shifts covered</label>
          <input
            id="shifts-covered"
            value={shiftsCovered}
            onChange={(e) => setShiftsCovered(e.target.value)}
            readOnly={!isAdmin}
            placeholder="e.g. 24x7, Day shift only"
          />
        </div>
      </div>

      <div className="settings-field">
        <label>Default weekly off days</label>
        <div className="day-toggle-row">
          {ALL_DAYS.map((day) => (
            <button
              key={day}
              type="button"
              className={`day-toggle${offDays.includes(day) ? ' on' : ''}`}
              onClick={() => toggleDay(day)}
              disabled={!isAdmin}
            >
              {day.slice(0, 3)}
            </button>
          ))}
        </div>
      </div>

      <label className="compoff-check">
        <input
          type="checkbox"
          checked={compOffsEnabled}
          onChange={(e) => setCompOffsEnabled(e.target.checked)}
          disabled={!isAdmin}
        />
        Allow comp-offs — working a default off day earns a make-up day off
      </label>

      {isAdmin && (
        <>
          <button className="btn" onClick={save} disabled={!name.trim() || saveMutation.isPending}>
            {saveMutation.isPending ? 'Saving…' : 'Save team settings'}
          </button>
          {saveMutation.isError && <p className="error-text">Could not save — that team name may already be in use.</p>}
        </>
      )}
    </section>
  )
}

function TracksSection() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const queryClient = useQueryClient()
  const { data: tracks } = useQuery({ queryKey: ['tracks'], queryFn: getTracks })
  const [newTrackName, setNewTrackName] = useState('')
  const [newTrackLead, setNewTrackLead] = useState('')
  const [newTrackColor, setNewTrackColor] = useState('#4453AD')
  const [newSubtrackName, setNewSubtrackName] = useState<Record<number, string>>({})

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['tracks'] })
    queryClient.invalidateQueries({ queryKey: ['roster'] })
  }

  const createTrackMutation = useMutation({
    mutationFn: createTrack,
    onSuccess: () => {
      setNewTrackName('')
      setNewTrackLead('')
      invalidate()
    },
  })
  const updateTrackMutation = useMutation({ mutationFn: (t: Track) => updateTrack(t.id, t), onSuccess: invalidate })
  const deleteTrackMutation = useMutation({ mutationFn: deleteTrack, onSuccess: invalidate })
  const createSubtrackMutation = useMutation({ mutationFn: createSubtrack, onSuccess: invalidate })
  const deleteSubtrackMutation = useMutation({ mutationFn: deleteSubtrack, onSuccess: invalidate })

  return (
    <section className="card settings-section">
      <h2>Tracks &amp; subtracks</h2>
      <div className="track-list">
        {tracks?.map((track) => (
          <div className="track-row" key={track.id}>
            <div className="track-row-main">
              <input
                type="color"
                value={track.color}
                onChange={(e) => updateTrackMutation.mutate({ ...track, color: e.target.value })}
                className="color-swatch"
                disabled={!canEdit}
              />
              <input
                className="track-name-input"
                defaultValue={track.name}
                onBlur={(e) => e.target.value !== track.name && updateTrackMutation.mutate({ ...track, name: e.target.value })}
                readOnly={!canEdit}
              />
              <input
                className="track-lead-input"
                placeholder="Lead name"
                defaultValue={track.leadName ?? ''}
                onBlur={(e) =>
                  e.target.value !== (track.leadName ?? '') &&
                  updateTrackMutation.mutate({ ...track, leadName: e.target.value || null })
                }
                readOnly={!canEdit}
              />
              {canEdit && (
                <button className="btn-danger" onClick={() => deleteTrackMutation.mutate(track.id)}>
                  Delete
                </button>
              )}
            </div>
            <div className="subtrack-list">
              {track.subtracks.map((sub) => (
                <span className="pill subtrack-pill" key={sub.id}>
                  {sub.name}
                  {canEdit && (
                    <button className="pill-remove" onClick={() => deleteSubtrackMutation.mutate(sub.id)} aria-label={`Remove ${sub.name}`}>
                      ×
                    </button>
                  )}
                </span>
              ))}
              {canEdit && (
                <input
                  className="subtrack-add-input"
                  placeholder="+ subtrack"
                  value={newSubtrackName[track.id] ?? ''}
                  onChange={(e) => setNewSubtrackName((s) => ({ ...s, [track.id]: e.target.value }))}
                  onKeyDown={(e) => {
                    const name = newSubtrackName[track.id]?.trim()
                    if (e.key === 'Enter' && name) {
                      createSubtrackMutation.mutate({ trackId: track.id, name })
                      setNewSubtrackName((s) => ({ ...s, [track.id]: '' }))
                    }
                  }}
                />
              )}
            </div>
          </div>
        ))}
      </div>

      {canEdit && (
        <div className="add-row">
          <input type="color" value={newTrackColor} onChange={(e) => setNewTrackColor(e.target.value)} className="color-swatch" />
          <input placeholder="New track name" value={newTrackName} onChange={(e) => setNewTrackName(e.target.value)} />
          <input placeholder="Lead name" value={newTrackLead} onChange={(e) => setNewTrackLead(e.target.value)} />
          <button
            className="btn"
            disabled={!newTrackName.trim()}
            onClick={() => createTrackMutation.mutate({ name: newTrackName.trim(), leadName: newTrackLead || null, color: newTrackColor })}
          >
            Add track
          </button>
        </div>
      )}
    </section>
  )
}

// Shared by Locations and Job roles — both are flat, team-scoped master lists an
// admin can add to or prune, same interaction as the subtrack chips above.
function MasterListSection({
  title, hint, items, canEdit, onAdd, onDelete, placeholder,
}: {
  title: string
  hint: string
  items: { id: number; name: string }[] | undefined
  canEdit: boolean
  onAdd: (name: string) => void
  onDelete: (id: number) => void
  placeholder: string
}) {
  const [newName, setNewName] = useState('')

  const submit = () => {
    if (!newName.trim()) return
    onAdd(newName.trim())
    setNewName('')
  }

  return (
    <section className="card settings-section">
      <h2>{title}</h2>
      <p className="field-hint">{hint}</p>
      <div className="subtrack-list">
        {items?.map((item) => (
          <span className="pill subtrack-pill" key={item.id}>
            {item.name}
            {canEdit && (
              <button className="pill-remove" onClick={() => onDelete(item.id)} aria-label={`Remove ${item.name}`}>
                ×
              </button>
            )}
          </span>
        ))}
        {items?.length === 0 && <p className="field-hint">None yet.</p>}
      </div>
      {canEdit && (
        <div className="add-row">
          <input
            placeholder={placeholder}
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && submit()}
          />
          <button className="btn" disabled={!newName.trim()} onClick={submit}>
            Add
          </button>
        </div>
      )}
    </section>
  )
}

function LocationsSection() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const queryClient = useQueryClient()
  const { data: locations } = useQuery({ queryKey: ['locations'], queryFn: getLocations })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['locations'] })
  const createMutation = useMutation({ mutationFn: createLocation, onSuccess: invalidate })
  const deleteMutation = useMutation({ mutationFn: deleteLocation, onSuccess: invalidate })

  return (
    <MasterListSection
      title="Locations"
      hint="Cities team members can be based out of. Add more as you expand."
      items={locations}
      canEdit={canEdit}
      onAdd={(name) => createMutation.mutate({ name })}
      onDelete={(id) => deleteMutation.mutate(id)}
      placeholder="+ city"
    />
  )
}

function JobRolesSection() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const queryClient = useQueryClient()
  const { data: jobRoles } = useQuery({ queryKey: ['job-roles'], queryFn: getJobRoles })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['job-roles'] })
  const createMutation = useMutation({ mutationFn: createJobRole, onSuccess: invalidate })
  const deleteMutation = useMutation({ mutationFn: deleteJobRole, onSuccess: invalidate })

  return (
    <MasterListSection
      title="Job roles / titles"
      hint="Roles team members can be assigned, e.g. Cashier or Team Lead."
      items={jobRoles}
      canEdit={canEdit}
      onAdd={(name) => createMutation.mutate({ name })}
      onDelete={(id) => deleteMutation.mutate(id)}
      placeholder="+ role"
    />
  )
}

// Grants read-only oversight of this team's live-availability dashboard to someone
// already known to this admin — never a full roster/edit access, and never an
// arbitrary cross-tenant lookup (the search endpoint itself enforces that scope).
function ManagersSection() {
  const { currentRole } = useTeam()
  const isAdmin = currentRole === 'Admin'
  const queryClient = useQueryClient()
  const [phone, setPhone] = useState('')

  const { data: managers } = useQuery({ queryKey: ['team-managers'], queryFn: getTeamManagers, enabled: isAdmin })
  const { data: results } = useQuery({
    queryKey: ['managers-search', phone],
    queryFn: () => searchPeopleByPhone(phone),
    enabled: isAdmin && phone.trim().length >= 3,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['team-managers'] })
  const grantMutation = useMutation({
    mutationFn: grantManager,
    onSuccess: () => { setPhone(''); invalidate() },
  })
  const revokeMutation = useMutation({ mutationFn: revokeManager, onSuccess: invalidate })

  if (!isAdmin) return null

  const grantedIds = new Set((managers ?? []).map((m) => m.personId))

  return (
    <section className="card settings-section">
      <h2>Managers</h2>
      <p className="field-hint">
        People who can see this team's live availability across all the teams they oversee — without gaining roster access here.
      </p>

      <div className="subtrack-list">
        {managers?.map((m) => (
          <span className="pill subtrack-pill" key={m.id}>
            {m.personName} · {m.personPhone}
            <button className="pill-remove" onClick={() => revokeMutation.mutate(m.id)} aria-label={`Remove ${m.personName} as manager`}>
              ×
            </button>
          </span>
        ))}
        {managers?.length === 0 && <p className="field-hint">No managers yet.</p>}
      </div>

      <div className="field" style={{ marginTop: 14 }}>
        <label htmlFor="manager-search">Add a manager by phone number</label>
        <input
          id="manager-search"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          placeholder="Search by phone (min 3 digits)"
        />
        {results && results.length > 0 && (
          <div className="team-checklist" style={{ marginTop: 8 }}>
            {results.map((p) => (
              <div key={p.id} className="unassigned-row" style={{ borderTop: 'none', padding: '4px 0' }}>
                <span>{p.name} · {p.phone}</span>
                <button
                  className="btn-secondary"
                  disabled={grantedIds.has(p.id) || grantMutation.isPending}
                  onClick={() => grantMutation.mutate(p.id)}
                >
                  {grantedIds.has(p.id) ? 'Already a manager' : 'Add as manager'}
                </button>
              </div>
            ))}
          </div>
        )}
        {phone.trim().length >= 3 && results?.length === 0 && (
          <p className="field-hint">No one found with that phone number among people you already manage.</p>
        )}
      </div>
    </section>
  )
}

function ShiftTypesSection() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const queryClient = useQueryClient()
  const { data: shiftTypes } = useQuery({ queryKey: ['shift-types'], queryFn: getShiftTypes })
  const [form, setForm] = useState<Omit<ShiftType, 'id'>>({ code: '', name: '', start: null, end: null, color: '#4453AD', isOvernight: false, isWorkShift: true })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['shift-types'] })
    queryClient.invalidateQueries({ queryKey: ['roster'] })
  }

  const createMutation = useMutation({
    mutationFn: createShiftType,
    onSuccess: () => {
      setForm({ code: '', name: '', start: null, end: null, color: '#4453AD', isOvernight: false, isWorkShift: true })
      invalidate()
    },
  })
  const updateMutation = useMutation({ mutationFn: (st: ShiftType) => updateShiftType(st.id, st), onSuccess: invalidate })
  const deleteMutation = useMutation({ mutationFn: deleteShiftType, onSuccess: invalidate })

  return (
    <section className="card settings-section">
      <h2>Shift types</h2>
      <table className="data-table">
        <thead>
          <tr>
            <th>Code</th>
            <th>Name</th>
            <th>Start</th>
            <th>End</th>
            <th>Overnight</th>
            <th>Work shift</th>
            <th>Color</th>
            {canEdit && <th></th>}
          </tr>
        </thead>
        <tbody>
          {shiftTypes?.map((st) => (
            <tr key={st.id}>
              <td className="mono">{st.code}</td>
              <td>
                <input
                  defaultValue={st.name}
                  onBlur={(e) => e.target.value !== st.name && updateMutation.mutate({ ...st, name: e.target.value })}
                  readOnly={!canEdit}
                />
              </td>
              <td className="mono">{st.start?.slice(0, 5) ?? '—'}</td>
              <td className="mono">{st.end?.slice(0, 5) ?? '—'}</td>
              <td>{st.isOvernight ? 'Yes' : 'No'}</td>
              <td>
                <input
                  type="checkbox"
                  checked={st.isWorkShift}
                  onChange={(e) => updateMutation.mutate({ ...st, isWorkShift: e.target.checked })}
                  disabled={!canEdit}
                />
              </td>
              <td>
                <input
                  type="color"
                  value={st.color}
                  className="color-swatch"
                  onChange={(e) => updateMutation.mutate({ ...st, color: e.target.value })}
                  disabled={!canEdit}
                />
              </td>
              {canEdit && (
                <td>
                  <button className="btn-danger" onClick={() => deleteMutation.mutate(st.id)}>
                    Delete
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>

      {canEdit && (
        <div className="add-row shift-add-row">
          <input placeholder="Code" style={{ width: 60 }} value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })} />
          <input placeholder="Name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <input type="time" value={form.start ?? ''} onChange={(e) => setForm({ ...form, start: e.target.value || null })} />
          <input type="time" value={form.end ?? ''} onChange={(e) => setForm({ ...form, end: e.target.value || null })} />
          <label className="overnight-check">
            <input type="checkbox" checked={form.isOvernight} onChange={(e) => setForm({ ...form, isOvernight: e.target.checked })} />
            Overnight
          </label>
          <label className="overnight-check">
            <input type="checkbox" checked={form.isWorkShift} onChange={(e) => setForm({ ...form, isWorkShift: e.target.checked })} />
            Work shift
          </label>
          <input type="color" value={form.color} onChange={(e) => setForm({ ...form, color: e.target.value })} className="color-swatch" />
          <button className="btn" disabled={!form.code.trim() || !form.name.trim()} onClick={() => createMutation.mutate(form)}>
            Add shift type
          </button>
        </div>
      )}
    </section>
  )
}
