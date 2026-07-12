import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createShiftType,
  createSubtrack,
  createTrack,
  deleteShiftType,
  deleteSubtrack,
  deleteTrack,
  getShiftTypes,
  getTracks,
  updateShiftType,
  updateTrack,
} from '../api/endpoints'
import { useTeam } from '../team/TeamContext'
import type { ShiftType, Track } from '../types'
import './Settings.css'

export default function Settings() {
  return (
    <div className="settings-page">
      <TracksSection />
      <ShiftTypesSection />
    </div>
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

function ShiftTypesSection() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const queryClient = useQueryClient()
  const { data: shiftTypes } = useQuery({ queryKey: ['shift-types'], queryFn: getShiftTypes })
  const [form, setForm] = useState<Omit<ShiftType, 'id'>>({ code: '', name: '', start: null, end: null, color: '#4453AD', isOvernight: false })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['shift-types'] })
    queryClient.invalidateQueries({ queryKey: ['roster'] })
  }

  const createMutation = useMutation({
    mutationFn: createShiftType,
    onSuccess: () => {
      setForm({ code: '', name: '', start: null, end: null, color: '#4453AD', isOvernight: false })
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
          <input type="color" value={form.color} onChange={(e) => setForm({ ...form, color: e.target.value })} className="color-swatch" />
          <button className="btn" disabled={!form.code.trim() || !form.name.trim()} onClick={() => createMutation.mutate(form)}>
            Add shift type
          </button>
        </div>
      )}
    </section>
  )
}
