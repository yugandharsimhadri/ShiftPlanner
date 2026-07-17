import React, { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { applyRosterPattern } from '../api/endpoints'
import type { ShiftType, TeamMember } from '../types'

interface Props {
  year: number
  month: number
  members: TeamMember[]
  shiftTypes: ShiftType[]
  onClose: () => void
  onApplied: () => void
}

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const

export default function ApplyPatternModal({ year, month, members, shiftTypes, onClose, onApplied }: Props) {
  const [selectedMemberIds, setSelectedMemberIds] = useState<Set<number>>(new Set())
  const [pattern, setPattern] = useState<Record<string, string>>({})
  const [skipInactive, setSkipInactive] = useState(true)

  const mutation = useMutation({
    mutationFn: applyRosterPattern,
    onSuccess: () => {
      onApplied()
      onClose()
    },
  })

  function toggleMember(id: number) {
    setSelectedMemberIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function handleSubmit() {
    const weeklyPattern: Record<string, string | null> = {}
    for (const day of WEEKDAYS) {
      weeklyPattern[day] = pattern[day] || null
    }
    mutation.mutate({
      year,
      month,
      teamMemberIds: Array.from(selectedMemberIds),
      weeklyPattern,
      skipInactive,
    })
  }

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <h2>Apply weekly pattern</h2>
        <p style={{ color: 'var(--ink-soft)', fontSize: 13, marginTop: -8, marginBottom: 16 }}>
          Pick a shift for each weekday and it repeats across the whole month for everyone
          selected below — a quick way to build a rotating roster without setting every cell
          by hand.
        </p>

        <div className="field">
          <label>Team members</label>
          <div style={{ maxHeight: 140, overflowY: 'auto', border: '1px solid var(--line)', borderRadius: 6, padding: 8 }}>
            {members.map((m) => (
              <label key={m.id} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, fontWeight: 400, textTransform: 'none', padding: '3px 0' }}>
                <input type="checkbox" checked={selectedMemberIds.has(m.id)} onChange={() => toggleMember(m.id)} />
                {m.name} ({m.code})
              </label>
            ))}
          </div>
        </div>

        <div className="field">
          <label>Weekly pattern</label>
          <div style={{ display: 'grid', gridTemplateColumns: '90px 1fr', gap: 6, alignItems: 'center' }}>
            {WEEKDAYS.map((day) => (
              <React.Fragment key={day}>
                <span style={{ fontSize: 12, color: 'var(--ink-soft)' }}>{day}</span>
                <select
                  value={pattern[day] ?? ''}
                  onChange={(e) => setPattern((p) => ({ ...p, [day]: e.target.value }))}
                >
                  <option value="">— Off —</option>
                  {shiftTypes.map((st) => (
                    <option key={st.code} value={st.code}>{st.code} · {st.name}</option>
                  ))}
                </select>
              </React.Fragment>
            ))}
          </div>
        </div>

        <div className="field">
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, textTransform: 'none', fontWeight: 400 }}>
            <input type="checkbox" checked={skipInactive} onChange={(e) => setSkipInactive(e.target.checked)} />
            Skip inactive team members
          </label>
        </div>

        {mutation.isError && <p className="error-text">Couldn't apply the pattern. Please try again.</p>}
        {mutation.data && mutation.data.errors.length > 0 && (
          <p className="error-text">{mutation.data.errors.length} row(s) couldn't be set — check for inactive members.</p>
        )}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn" disabled={selectedMemberIds.size === 0 || mutation.isPending} onClick={handleSubmit}>
            {mutation.isPending ? 'Applying…' : 'Apply pattern'}
          </button>
        </div>
      </div>
    </div>
  )
}
