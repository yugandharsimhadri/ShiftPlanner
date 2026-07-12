import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { copyForward, type CopyForwardRequest } from '../api/endpoints'
import { addMonths, monthLabel } from '../lib/dates'
import type { CopyForwardResult } from '../types'

interface Props {
  sourceYear: number
  sourceMonth: number
  onClose: () => void
  onCopied: () => void
}

export default function CopyMonthModal({ sourceYear, sourceMonth, onClose, onCopied }: Props) {
  const nextMonth = addMonths(sourceYear, sourceMonth, 1)
  const [targetYear, setTargetYear] = useState(nextMonth.year)
  const [targetMonth, setTargetMonth] = useState(nextMonth.month)
  const [pattern, setPattern] = useState<CopyForwardRequest['pattern']>('weekday')
  const [skipInactive, setSkipInactive] = useState(true)
  const [result, setResult] = useState<CopyForwardResult | null>(null)

  const mutation = useMutation({
    mutationFn: copyForward,
    onSuccess: (data) => {
      setResult(data)
      onCopied()
    },
  })

  function handleSubmit() {
    mutation.mutate({ sourceYear, sourceMonth, targetYear, targetMonth, pattern, skipInactive })
  }

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal">
        <h2>Copy month forward</h2>
        <p style={{ color: 'var(--ink-soft)', fontSize: 13, marginTop: -8, marginBottom: 16 }}>
          Copy roster entries from <strong className="mono">{monthLabel(sourceYear, sourceMonth)}</strong> to another
          month.
        </p>

        {!result && (
          <>
            <div className="field-row">
              <div className="field">
                <label htmlFor="target-year">Target year</label>
                <input
                  id="target-year"
                  type="number"
                  value={targetYear}
                  onChange={(e) => setTargetYear(Number(e.target.value))}
                />
              </div>
              <div className="field">
                <label htmlFor="target-month">Target month</label>
                <select
                  id="target-month"
                  value={targetMonth}
                  onChange={(e) => setTargetMonth(Number(e.target.value))}
                >
                  {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
                    <option key={m} value={m}>
                      {monthLabel(2000, m).split(' ')[0]}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="field">
              <label>Date mapping</label>
              <div style={{ display: 'flex', gap: 16, fontSize: 13 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, textTransform: 'none', fontWeight: 400 }}>
                  <input
                    type="radio"
                    checked={pattern === 'weekday'}
                    onChange={() => setPattern('weekday')}
                  />
                  Same weekday (e.g. 2nd Tuesday → 2nd Tuesday)
                </label>
              </div>
              <div style={{ display: 'flex', gap: 16, fontSize: 13, marginTop: 4 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, textTransform: 'none', fontWeight: 400 }}>
                  <input
                    type="radio"
                    checked={pattern === 'exact-date'}
                    onChange={() => setPattern('exact-date')}
                  />
                  Same date number (e.g. the 15th → the 15th)
                </label>
              </div>
            </div>

            <div className="field">
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, textTransform: 'none', fontWeight: 400 }}>
                <input type="checkbox" checked={skipInactive} onChange={(e) => setSkipInactive(e.target.checked)} />
                Skip inactive employees entirely
              </label>
            </div>

            {mutation.isError && <p className="error-text">Copy failed. Please try again.</p>}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={onClose}>
                Cancel
              </button>
              <button className="btn" onClick={handleSubmit} disabled={mutation.isPending}>
                {mutation.isPending ? 'Copying…' : 'Copy'}
              </button>
            </div>
          </>
        )}

        {result && (
          <div>
            <p>
              Copied <strong>{result.copiedCount}</strong> shift assignment{result.copiedCount === 1 ? '' : 's'} to{' '}
              <strong className="mono">{monthLabel(targetYear, targetMonth)}</strong>.
            </p>
            {result.flagged.length > 0 && (
              <>
                <p style={{ marginTop: 12, marginBottom: 6, fontWeight: 600 }}>
                  Flagged entries ({result.flagged.length}) — still copied, review these:
                </p>
                <div style={{ maxHeight: 220, overflowY: 'auto', border: '1px solid var(--line)', borderRadius: 6 }}>
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Employee</th>
                        <th>Date</th>
                        <th>Reason</th>
                      </tr>
                    </thead>
                    <tbody>
                      {result.flagged.map((f, i) => (
                        <tr key={i}>
                          <td>{f.employeeName}</td>
                          <td className="mono">{f.date}</td>
                          <td>
                            <span className="badge">{f.reason}</span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
            <div className="modal-actions">
              <button className="btn" onClick={onClose}>
                Done
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
