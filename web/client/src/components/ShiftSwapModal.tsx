import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { createShiftSwap } from '../api/endpoints'
import type { TeamMember } from '../types'

interface UpcomingShift {
  date: string
  shiftCode: string
}

interface Props {
  upcomingShifts: UpcomingShift[]
  otherMembers: TeamMember[]
  onClose: () => void
  onCreated: () => void
}

export default function ShiftSwapModal({ upcomingShifts, otherMembers, onClose, onCreated }: Props) {
  const [selected, setSelected] = useState(upcomingShifts[0] ?? null)
  const [targetTeamMemberId, setTargetTeamMemberId] = useState<number | ''>('')

  const mutation = useMutation({
    mutationFn: createShiftSwap,
    onSuccess: () => {
      onCreated()
      onClose()
    },
  })

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <h2>Offer a shift</h2>
        <p style={{ color: 'var(--ink-soft)', fontSize: 13, marginTop: -8, marginBottom: 16 }}>
          Anyone who claims this still needs your team lead's approval before it moves on the
          roster.
        </p>

        {upcomingShifts.length === 0 ? (
          <p style={{ fontSize: 13, color: 'var(--ink-soft)' }}>
            You don't have any upcoming assigned shifts to offer.
          </p>
        ) : (
          <>
            <div className="field">
              <label htmlFor="swap-shift">Which shift</label>
              <select
                id="swap-shift"
                value={selected ? `${selected.date}|${selected.shiftCode}` : ''}
                onChange={(e) => {
                  const [date, shiftCode] = e.target.value.split('|')
                  setSelected({ date, shiftCode })
                }}
              >
                {upcomingShifts.map((s) => (
                  <option key={`${s.date}|${s.shiftCode}`} value={`${s.date}|${s.shiftCode}`}>
                    {s.date} — {s.shiftCode}
                  </option>
                ))}
              </select>
            </div>

            <div className="field">
              <label htmlFor="swap-target">Offer to (optional)</label>
              <select
                id="swap-target"
                value={targetTeamMemberId}
                onChange={(e) => setTargetTeamMemberId(e.target.value ? Number(e.target.value) : '')}
              >
                <option value="">Open to anyone on the team</option>
                {otherMembers.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name} ({m.code})
                  </option>
                ))}
              </select>
            </div>

            {mutation.isError && <p className="error-text">Couldn't submit the offer. Please try again.</p>}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={onClose}>
                Cancel
              </button>
              <button
                className="btn"
                disabled={!selected || mutation.isPending}
                onClick={() =>
                  selected &&
                  mutation.mutate({
                    date: selected.date,
                    shiftCode: selected.shiftCode,
                    targetTeamMemberId: targetTeamMemberId || null,
                  })
                }
              >
                {mutation.isPending ? 'Submitting…' : 'Offer shift'}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
