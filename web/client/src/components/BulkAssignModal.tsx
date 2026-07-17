import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { bulkAssignRoster } from '../api/endpoints'
import type { ShiftType, TeamMember } from '../types'

interface Props {
  members: TeamMember[]
  shiftTypes: ShiftType[]
  defaultDate: string
  onClose: () => void
  onApplied: () => void
}

function datesInRange(start: string, end: string): string[] {
  const dates: string[] = []
  let cursor = new Date(start)
  const last = new Date(end)
  while (cursor <= last) {
    dates.push(cursor.toISOString().slice(0, 10))
    cursor = new Date(cursor.getTime() + 86400000)
  }
  return dates
}

export default function BulkAssignModal({ members, shiftTypes, defaultDate, onClose, onApplied }: Props) {
  const [selectedMemberIds, setSelectedMemberIds] = useState<Set<number>>(new Set())
  const [startDate, setStartDate] = useState(defaultDate)
  const [endDate, setEndDate] = useState(defaultDate)
  const [shiftCode, setShiftCode] = useState<string>(shiftTypes[0]?.code ?? '')

  const mutation = useMutation({
    mutationFn: bulkAssignRoster,
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

  const invalidRange = endDate < startDate

  function handleSubmit() {
    mutation.mutate({
      teamMemberIds: Array.from(selectedMemberIds),
      dates: datesInRange(startDate, endDate),
      shiftCode: shiftCode || null,
    })
  }

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <h2>Assign shift to many members</h2>
        <p style={{ color: 'var(--ink-soft)', fontSize: 13, marginTop: -8, marginBottom: 16 }}>
          Sets the same shift for every selected member across the date range — much faster
          than one cell at a time.
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

        <div className="field-row">
          <div className="field">
            <label htmlFor="bulk-start">From date</label>
            <input id="bulk-start" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="bulk-end">To date</label>
            <input id="bulk-end" type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
          </div>
        </div>

        <div className="field">
          <label htmlFor="bulk-shift">Shift</label>
          <select id="bulk-shift" value={shiftCode} onChange={(e) => setShiftCode(e.target.value)}>
            <option value="">— Clear —</option>
            {shiftTypes.map((st) => (
              <option key={st.code} value={st.code}>{st.code} · {st.name}</option>
            ))}
          </select>
        </div>

        {invalidRange && <p className="error-text">End date can't be before the start date.</p>}
        {mutation.isError && <p className="error-text">Couldn't apply the change. Please try again.</p>}
        {mutation.data && mutation.data.errors.length > 0 && (
          <p className="error-text">{mutation.data.errors.length} row(s) couldn't be set — check for inactive members.</p>
        )}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn" disabled={selectedMemberIds.size === 0 || invalidRange || mutation.isPending} onClick={handleSubmit}>
            {mutation.isPending ? 'Applying…' : 'Apply'}
          </button>
        </div>
      </div>
    </div>
  )
}
