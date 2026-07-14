import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getCompOffs, getRoster, upsertRosterEntry, useCompOff } from '../api/endpoints'
import { addMonths, daysInMonth, monthLabel, toIsoDate } from '../lib/dates'
import RosterTable from '../components/RosterTable'
import ShiftPopup from '../components/ShiftPopup'
import CopyMonthModal from '../components/CopyMonthModal'
import ImportExportPanel from '../components/ImportExportPanel'
import { useTeam } from '../team/TeamContext'
import type { RosterEntry, TeamMember } from '../types'
import './Roster.css'

const TODAY = new Date()

export default function Roster() {
  const { currentRole } = useTeam()
  const canEdit = currentRole === 'Editor' || currentRole === 'Admin'
  const [year, setYear] = useState(TODAY.getFullYear())
  const [month, setMonth] = useState(TODAY.getMonth() + 1)
  const [popup, setPopup] = useState<{ teamMemberId: number; date: string; x: number; y: number } | null>(null)
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
    queryKey: ['compoffs', 'pending', popup?.teamMemberId],
    queryFn: () => getCompOffs('Pending', popup!.teamMemberId),
    enabled: !!popup && canEdit,
  })

  const { data: pendingCompOffsAll } = useQuery({
    queryKey: ['compoffs', 'pending', 'all'],
    queryFn: () => getCompOffs('Pending'),
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
      map.set(`${entry.teamMemberId}|${entry.date}`, entry)
    }
    return map
  }, [data])

  const shiftTypesByCode = useMemo(() => {
    const map = new Map(data?.shiftTypes.map((s) => [s.code, s]) ?? [])
    return map
  }, [data])

  const holidaySet = useMemo(() => new Set((data?.holidays ?? []).map((h) => h.date)), [data])

  const membersByTrack = useMemo(() => {
    const map = new Map<number, TeamMember[]>()
    for (const member of data?.teamMembers ?? []) {
      if (member.trackId === null) continue
      const list = map.get(member.trackId) ?? []
      list.push(member)
      map.set(member.trackId, list)
    }
    return map
  }, [data])

  // A track is optional on a team member now (it wasn't before the Team Members merge) —
  // without this, anyone added without a track would silently vanish from the roster
  // instead of showing up somewhere editable.
  const unassignedMembers = useMemo(
    () => (data?.teamMembers ?? []).filter((m) => m.trackId === null),
    [data]
  )

  const todayIso = toIsoDate(TODAY.getFullYear(), TODAY.getMonth() + 1, TODAY.getDate())
  const stats = useMemo(() => {
    const members = data?.teamMembers ?? []
    const activeMembers = members.filter((m) => m.status === 'Active').length
    let onShiftToday = 0
    let offToday = 0
    for (const member of members) {
      const entry = entriesMap.get(`${member.id}|${todayIso}`)
      const shift = entry?.shiftCode ? shiftTypesByCode.get(entry.shiftCode) : undefined
      if (shift?.isWorkShift) onShiftToday++
      else if (shift) offToday++
    }
    return { activeMembers, onShiftToday, offToday, compOffsPending: pendingCompOffsAll?.length ?? 0 }
  }, [data, entriesMap, shiftTypesByCode, todayIso, pendingCompOffsAll])

  function goMonth(delta: number) {
    const next = addMonths(year, month, delta)
    setYear(next.year)
    setMonth(next.month)
  }

  function handleCellClick(teamMemberId: number, date: string, el: HTMLElement) {
    if (!canEdit) return
    const rect = el.getBoundingClientRect()
    setPopup({ teamMemberId, date, x: rect.left, y: rect.bottom + 6 })
  }

  function handleSelectShift(code: string | null) {
    if (!popup) return
    upsertMutation.mutate({ teamMemberId: popup.teamMemberId, date: popup.date, shiftCode: code })
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
        <div className="stat-row">
          <div className="stat-card">
            <span className="stat-label">Active members</span>
            <span className="stat-num">{stats.activeMembers}</span>
          </div>
          <div className="stat-card">
            <span className="stat-label">On shift today</span>
            <span className="stat-num">{stats.onShiftToday}</span>
          </div>
          <div className="stat-card">
            <span className="stat-label">Off today</span>
            <span className="stat-num stat-warn">{stats.offToday}</span>
          </div>
          <div className="stat-card">
            <span className="stat-label">Comp-offs pending</span>
            <span className="stat-num stat-warn">{stats.compOffsPending}</span>
          </div>
        </div>
      )}

      {data && (
        <div className="track-sections">
          {data.tracks.map((track) => {
            const members = membersByTrack.get(track.id) ?? []
            if (members.length === 0) return null
            return (
              <section key={track.id} className="track-section">
                <div className="track-heading">
                  <span className="pill-dot" style={{ background: track.color }} />
                  <h3>{track.name}</h3>
                  {track.leadName && <span className="track-lead">Lead: {track.leadName}</span>}
                  <span className="track-count mono">{members.length}</span>
                </div>
                <RosterTable
                  members={members}
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

          {unassignedMembers.length > 0 && (
            <section className="track-section">
              <div className="track-heading">
                <span className="pill-dot" style={{ background: 'var(--ink-faint)' }} />
                <h3>Unassigned</h3>
                <span className="track-count mono">{unassignedMembers.length}</span>
              </div>
              <RosterTable
                members={unassignedMembers}
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
          )}

          {data.teamMembers.length === 0 && (
            <div className="empty-state">
              No team members yet — add one from the Team Members tab.
            </div>
          )}
        </div>
      )}

      {popup && data && (
        <ShiftPopup
          x={popup.x}
          y={popup.y}
          shiftTypes={data.shiftTypes}
          currentCode={entriesMap.get(`${popup.teamMemberId}|${popup.date}`)?.shiftCode ?? null}
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
