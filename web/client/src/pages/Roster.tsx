import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getCompOffs, getRoster, upsertRosterEntry, useCompOff } from '../api/endpoints'
import { addMonths, daysInMonth, monthLabel } from '../lib/dates'
import RosterTable from '../components/RosterTable'
import ShiftPopup from '../components/ShiftPopup'
import CopyMonthModal from '../components/CopyMonthModal'
import ImportExportPanel from '../components/ImportExportPanel'
import { useTeam } from '../team/TeamContext'
import type { Employee, RosterEntry } from '../types'
import './Roster.css'

const TODAY = new Date()

export default function Roster() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const [year, setYear] = useState(TODAY.getFullYear())
  const [month, setMonth] = useState(TODAY.getMonth() + 1)
  const [popup, setPopup] = useState<{ employeeId: string; date: string; x: number; y: number } | null>(null)
  const [copyOpen, setCopyOpen] = useState(false)
  const [importExportOpen, setImportExportOpen] = useState(false)

  const queryClient = useQueryClient()
  const rosterKey = ['roster', year, month]

  const { data, isLoading, isError } = useQuery({
    queryKey: rosterKey,
    queryFn: () => getRoster(year, month),
  })

  const upsertMutation = useMutation({
    mutationFn: upsertRosterEntry,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: rosterKey })
      queryClient.invalidateQueries({ queryKey: ['compoffs'] })
    },
  })

  const { data: pendingCompOffs } = useQuery({
    queryKey: ['compoffs', 'pending', popup?.employeeId],
    queryFn: () => getCompOffs('Pending', popup!.employeeId),
    enabled: !!popup && canEdit,
  })

  const useCompOffMutation = useMutation({
    mutationFn: (id: number) => useCompOff(id, popup!.date),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['compoffs'] })
      setPopup(null)
    },
  })

  const days = useMemo(() => {
    const count = daysInMonth(year, month)
    return Array.from({ length: count }, (_, i) => i + 1)
  }, [year, month])

  const entriesMap = useMemo(() => {
    const map = new Map<string, RosterEntry>()
    for (const entry of data?.entries ?? []) {
      map.set(`${entry.employeeId}|${entry.date}`, entry)
    }
    return map
  }, [data])

  const shiftTypesByCode = useMemo(() => {
    const map = new Map(data?.shiftTypes.map((s) => [s.code, s]) ?? [])
    return map
  }, [data])

  const holidaySet = useMemo(() => new Set((data?.holidays ?? []).map((h) => h.date)), [data])

  const employeesByTrack = useMemo(() => {
    const map = new Map<number, Employee[]>()
    for (const emp of data?.employees ?? []) {
      const list = map.get(emp.trackId) ?? []
      list.push(emp)
      map.set(emp.trackId, list)
    }
    return map
  }, [data])

  function goMonth(delta: number) {
    const next = addMonths(year, month, delta)
    setYear(next.year)
    setMonth(next.month)
  }

  function handleCellClick(employeeId: string, date: string, el: HTMLElement) {
    if (!canEdit) return
    const rect = el.getBoundingClientRect()
    setPopup({ employeeId, date, x: rect.left, y: rect.bottom + 6 })
  }

  function handleSelectShift(code: string | null) {
    if (!popup) return
    upsertMutation.mutate({ employeeId: popup.employeeId, date: popup.date, shiftCode: code })
    setPopup(null)
  }

  return (
    <div className="roster-page">
      <div className="toolbar">
        <div className="month-nav">
          <button className="btn-secondary" onClick={() => goMonth(-1)} aria-label="Previous month">
            ‹
          </button>
          <h2 className="month-label mono">{monthLabel(year, month)}</h2>
          <button className="btn-secondary" onClick={() => goMonth(1)} aria-label="Next month">
            ›
          </button>
        </div>
        <div className="toolbar-spacer" />
        <button className="btn-secondary" onClick={() => setImportExportOpen(true)}>
          {canEdit ? 'Import / Export' : 'Export'}
        </button>
        {canEdit && (
          <button className="btn" onClick={() => setCopyOpen(true)}>
            Copy month forward
          </button>
        )}
      </div>

      {isLoading && <div className="empty-state">Loading roster…</div>}
      {isError && <div className="empty-state error-text">Could not load roster data.</div>}

      {data && (
        <div className="track-sections">
          {data.tracks.map((track) => {
            const employees = employeesByTrack.get(track.id) ?? []
            if (employees.length === 0) return null
            return (
              <section key={track.id} className="track-section">
                <div className="track-heading">
                  <span className="pill-dot" style={{ background: track.color }} />
                  <h3>{track.name}</h3>
                  {track.leadName && <span className="track-lead">Lead: {track.leadName}</span>}
                  <span className="track-count mono">{employees.length}</span>
                </div>
                <RosterTable
                  employees={employees}
                  year={year}
                  month={month}
                  days={days}
                  entriesMap={entriesMap}
                  shiftTypesByCode={shiftTypesByCode}
                  holidaySet={holidaySet}
                  offDays={data.defaultOffDays}
                  onCellClick={handleCellClick}
                  canEdit={canEdit}
                />
              </section>
            )
          })}
        </div>
      )}

      {popup && data && (
        <ShiftPopup
          x={popup.x}
          y={popup.y}
          shiftTypes={data.shiftTypes}
          currentCode={entriesMap.get(`${popup.employeeId}|${popup.date}`)?.shiftCode ?? null}
          onSelect={handleSelectShift}
          onClose={() => setPopup(null)}
          pendingCompOffs={pendingCompOffs ?? []}
          onUseCompOff={(id) => useCompOffMutation.mutate(id)}
        />
      )}

      {copyOpen && (
        <CopyMonthModal
          sourceYear={year}
          sourceMonth={month}
          onClose={() => setCopyOpen(false)}
          onCopied={() => queryClient.invalidateQueries({ queryKey: ['roster'] })}
        />
      )}

      {importExportOpen && (
        <ImportExportPanel year={year} month={month} canImport={canEdit} onClose={() => setImportExportOpen(false)} />
      )}
    </div>
  )
}
