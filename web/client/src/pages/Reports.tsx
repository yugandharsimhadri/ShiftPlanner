import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getUtilizationReport } from '../api/endpoints'
import type { UtilizationRow } from '../types'
import './Reports.css'

type SortKey = keyof Pick<
  UtilizationRow,
  'employeeName' | 'totalShiftsWorked' | 'weekendShiftsWorked' | 'compOffsEarned' | 'compOffsUsed' | 'compOffsPending'
>

function firstOfMonthIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export default function Reports() {
  const [start, setStart] = useState(firstOfMonthIso())
  const [end, setEnd] = useState(todayIso())
  const [sortKey, setSortKey] = useState<SortKey>('weekendShiftsWorked')
  const [sortDesc, setSortDesc] = useState(true)

  const { data: rows, isLoading, isError } = useQuery({
    queryKey: ['reports', 'utilization', start, end],
    queryFn: () => getUtilizationReport(start, end),
  })

  const sorted = useMemo(() => {
    if (!rows) return []
    const copy = [...rows]
    copy.sort((a, b) => {
      const av = a[sortKey]
      const bv = b[sortKey]
      const cmp = typeof av === 'string' ? av.localeCompare(bv as string) : (av as number) - (bv as number)
      return sortDesc ? -cmp : cmp
    })
    return copy
  }, [rows, sortKey, sortDesc])

  function sortBy(key: SortKey) {
    if (key === sortKey) {
      setSortDesc((d) => !d)
    } else {
      setSortKey(key)
      setSortDesc(true)
    }
  }

  function headerFor(key: SortKey, label: string) {
    const active = key === sortKey
    return (
      <th className="sortable-th" onClick={() => sortBy(key)}>
        {label}
        {active && <span className="sort-arrow">{sortDesc ? ' ▾' : ' ▴'}</span>}
      </th>
    )
  }

  return (
    <div className="reports-page">
      <div className="page-heading">
        <h2>Reports</h2>
      </div>
      <p className="page-sub">
        Utilization for the selected period — who's working the most, who's covering weekends, and who's owed a
        comp-off. Click a column to sort.
      </p>

      <div className="card reports-toolbar">
        <div className="field">
          <label htmlFor="report-start">From</label>
          <input id="report-start" type="date" value={start} onChange={(e) => setStart(e.target.value)} />
        </div>
        <div className="field">
          <label htmlFor="report-end">To</label>
          <input id="report-end" type="date" value={end} onChange={(e) => setEnd(e.target.value)} />
        </div>
      </div>

      {isLoading && <div className="empty-state">Loading report…</div>}
      {isError && <div className="empty-state error-text">Could not load the report.</div>}

      {sorted.length > 0 && (
        <div className="card">
          <table className="data-table">
            <thead>
              <tr>
                {headerFor('employeeName', 'Employee')}
                <th>Track</th>
                {headerFor('totalShiftsWorked', 'Shifts worked')}
                {headerFor('weekendShiftsWorked', 'Weekend shifts')}
                {headerFor('compOffsEarned', 'Comp-offs earned')}
                {headerFor('compOffsUsed', 'Comp-offs used')}
                {headerFor('compOffsPending', 'Comp-offs pending')}
              </tr>
            </thead>
            <tbody>
              {sorted.map((row) => (
                <tr key={row.employeeId}>
                  <td>
                    <div className="emp-name">{row.employeeName}</div>
                    <div className="emp-meta mono">{row.employeeCode}</div>
                  </td>
                  <td>{row.trackName ?? '—'}</td>
                  <td className="mono">{row.totalShiftsWorked}</td>
                  <td className="mono">{row.weekendShiftsWorked}</td>
                  <td className="mono">{row.compOffsEarned}</td>
                  <td className="mono">{row.compOffsUsed}</td>
                  <td className="mono">
                    {row.compOffsPending > 0 ? <span className="badge">{row.compOffsPending} owed</span> : 0}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {rows && rows.length === 0 && <div className="empty-state">No active employees on this team yet.</div>}
    </div>
  )
}
