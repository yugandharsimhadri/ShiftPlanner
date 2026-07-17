import { useQuery } from '@tanstack/react-query'
import { getRosterHistory } from '../api/endpoints'

interface Props {
  year: number
  month: number
}

export default function HistoryPanel({ year, month }: Props) {
  const { data: rows = [] } = useQuery({
    queryKey: ['roster-history', year, month],
    queryFn: () => getRosterHistory(year, month),
  })

  if (rows.length === 0) {
    return <p className="requests-empty">No changes recorded for this month yet.</p>
  }

  return (
    <div style={{ maxHeight: 260, overflowY: 'auto' }}>
      <table className="data-table">
        <thead>
          <tr>
            <th>When</th>
            <th>Team member</th>
            <th>Date</th>
            <th>Change</th>
            <th>Source</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id}>
              <td className="mono">{new Date(r.changedAt).toLocaleString()}</td>
              <td>{r.memberName}</td>
              <td className="mono">{r.date}</td>
              <td>
                {r.oldShiftCode ?? '—'} → {r.newShiftCode ?? '—'}
              </td>
              <td>
                <span className="badge">{r.source}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
