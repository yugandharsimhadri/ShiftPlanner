import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { createLeaveRequest } from '../api/endpoints'

interface Props {
  onClose: () => void
  onCreated: () => void
}

export default function LeaveRequestModal({ onClose, onCreated }: Props) {
  const today = new Date().toISOString().slice(0, 10)
  const [startDate, setStartDate] = useState(today)
  const [endDate, setEndDate] = useState(today)
  const [reason, setReason] = useState('')

  const mutation = useMutation({
    mutationFn: createLeaveRequest,
    onSuccess: () => {
      onCreated()
      onClose()
    },
  })

  const invalidRange = endDate < startDate

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <h2>Request leave</h2>
        <p style={{ color: 'var(--ink-soft)', fontSize: 13, marginTop: -8, marginBottom: 16 }}>
          Your team lead will approve or decline this — once approved, these dates show as
          "Leave" on the roster.
        </p>

        <div className="field-row">
          <div className="field">
            <label htmlFor="leave-start">Start date</label>
            <input id="leave-start" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="leave-end">End date</label>
            <input id="leave-end" type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
          </div>
        </div>

        <div className="field">
          <label htmlFor="leave-reason">Reason (optional)</label>
          <input
            id="leave-reason"
            type="text"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="e.g. Family trip"
          />
        </div>

        {invalidRange && <p className="error-text">End date can't be before the start date.</p>}
        {mutation.isError && <p className="error-text">Couldn't submit the request. Please try again.</p>}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn"
            disabled={invalidRange || mutation.isPending}
            onClick={() => mutation.mutate({ startDate, endDate, reason: reason.trim() || null })}
          >
            {mutation.isPending ? 'Submitting…' : 'Submit request'}
          </button>
        </div>
      </div>
    </div>
  )
}
