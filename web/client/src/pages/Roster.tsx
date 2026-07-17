import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  acknowledgeShift,
  getCompOffs,
  getMe,
  getRoster,
  getRosterPublishStatus,
  publishRoster,
  unpublishRoster,
  upsertRosterEntry,
  useCompOff,
} from '../api/endpoints'
import { addMonths, daysInMonth, monthLabel, toIsoDate } from '../lib/dates'
import RosterTable from '../components/RosterTable'
import ShiftPopup from '../components/ShiftPopup'
import CopyMonthModal from '../components/CopyMonthModal'
import ImportExportPanel from '../components/ImportExportPanel'
import RequestsPanel from '../components/RequestsPanel'
import BulkAssignModal from '../components/BulkAssignModal'
import ApplyPatternModal from '../components/ApplyPatternModal'
import HistoryPanel from '../components/HistoryPanel'
import OnboardingChecklist from '../components/OnboardingChecklist'
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
  const [myShiftsOnly, setMyShiftsOnly] = useState(false)
  const [bulkOpen, setBulkOpen] = useState(false)
  const [patternOpen, setPatternOpen] = useState(false)
  const [historyOpen, setHistoryOpen] = useState(false)

  const queryClient = useQueryClient()
  const rosterKey = ['roster', year, month]

  const { data, isLoading, isError } = useQuery({
    queryKey: rosterKey,
    queryFn: () => getRoster(year, month),
  })

  const { data: me } = useQuery({ queryKey: ['me'], queryFn: getMe })

  const { data: publishStatus } = useQuery({
    queryKey: ['roster-publish-status', year, month],
    queryFn: () => getRosterPublishStatus(year, month),
  })

  function invalidateRoster() {
    queryClient.invalidateQueries({ queryKey: ['roster'] })
    queryClient.invalidateQueries({ queryKey: ['roster-publish-status', year, month] })
    queryClient.invalidateQueries({ queryKey: ['roster-history', year, month] })
  }

  const publishMutation = useMutation({
    mutationFn: () => publishRoster(year, month),
    onSuccess: invalidateRoster,
  })

  const unpublishMutation = useMutation({
    mutationFn: () => unpublishRoster(year, month),
    onSuccess: invalidateRoster,
  })

  const acknowledgeMutation = useMutation({
    mutationFn: acknowledgeShift,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: rosterKey }),
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

  // "Just me" — client-side only, mirrors the same filter Mobile's Roster tab already has.
  const visibleMembers = useMemo(() => {
    const all = data?.teamMembers ?? []
    if (!myShiftsOnly || !me) return all
    return all.filter((m) => m.personId === me.personId)
  }, [data, myShiftsOnly, me])

  const membersByTrack = useMemo(() => {
    const map = new Map<number, TeamMember[]>()
    for (const member of visibleMembers) {
      if (member.trackId === null) continue
      const list = map.get(member.trackId) ?? []
      list.push(member)
      map.set(member.trackId, list)
    }
    return map
  }, [visibleMembers])

  // A track is optional on a team member now (it wasn't before the Team Members merge) —
  // without this, anyone added without a track would silently vanish from the roster
  // instead of showing up somewhere editable.
  const unassignedMembers = useMemo(
    () => visibleMembers.filter((m) => m.trackId === null),
    [visibleMembers]
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

  const myTeamMember = useMemo(
    () => data?.teamMembers.find((m) => m.personId === me?.personId) ?? null,
    [data, me]
  )

  const myUpcomingShifts = useMemo(() => {
    if (!myTeamMember || !data) return []
    return data.entries
      .filter((e) => e.teamMemberId === myTeamMember.id && e.shiftCode && e.date >= todayIso)
      .map((e) => ({ date: e.date, shiftCode: e.shiftCode! }))
  }, [data, myTeamMember, todayIso])

  const otherMembers = useMemo(
    () => (data?.teamMembers ?? []).filter((m) => m.id !== myTeamMember?.id),
    [data, myTeamMember]
  )

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

  function handleSelectShift(code: string | null, note: string | null) {
    if (!popup) return
    upsertMutation.mutate({ teamMemberId: popup.teamMemberId, date: popup.date, shiftCode: code, note })
    setPopup(null)
  }

  // Soft warning only — an Editor can still assign over approved leave if they mean to
  // (e.g. the leave was later cancelled outside the app's own workflow).
  const popupLeaveWarning = useMemo(() => {
    if (!popup || !data) return null
    const onLeave = data.leaveRequests.some(
      (l) => l.teamMemberId === popup.teamMemberId && l.status === 'Approved' && popup.date >= l.startDate && popup.date <= l.endDate
    )
    return onLeave ? 'This member has approved leave on this date.' : null
  }, [popup, data])

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
        <button
          className={`btn-secondary${myShiftsOnly ? ' active' : ''}`}
          onClick={() => setMyShiftsOnly((v) => !v)}
        >
          {myShiftsOnly ? 'Showing: Just me' : 'Show just me'}
        </button>
        <button className="btn-secondary" onClick={() => setImportExportOpen(true)}>
          {canEdit ? 'Import / Export' : 'Export'}
        </button>
        <button className="btn-secondary no-print" onClick={() => window.print()}>
          Print
        </button>
        {canEdit && (
          <>
            <button className="btn-secondary" onClick={() => setHistoryOpen((v) => !v)}>
              History
            </button>
            <button className="btn-secondary" onClick={() => setBulkOpen(true)}>
              Bulk assign
            </button>
            <button className="btn-secondary" onClick={() => setPatternOpen(true)}>
              Apply pattern
            </button>
            <button className="btn" onClick={() => setCopyOpen(true)}>
              Copy month forward
            </button>
          </>
        )}
      </div>

      {canEdit && data && (
        <div className={`publish-bar${publishStatus?.isPublished ? ' published' : ''}`}>
          <span>
            {publishStatus?.isPublished
              ? 'Published — visible to everyone on the team.'
              : "Draft — only Editors and Admins can see this month's roster."}
          </span>
          {publishStatus?.isPublished ? (
            <button className="btn-secondary" disabled={unpublishMutation.isPending} onClick={() => unpublishMutation.mutate()}>
              Unpublish
            </button>
          ) : (
            <button className="btn" disabled={publishMutation.isPending} onClick={() => publishMutation.mutate()}>
              Publish
            </button>
          )}
        </div>
      )}

      {isLoading && <div className="empty-state">Loading roster…</div>}
      {isError && <div className="empty-state error-text">Could not load roster data.</div>}

      {data && canEdit && (
        <OnboardingChecklist
          hasShiftTypes={data.shiftTypes.length > 0}
          hasOtherMembers={data.teamMembers.length > 1}
          hasRosterEntries={data.entries.length > 0}
        />
      )}

      {data && historyOpen && canEdit && (
        <div className="card">
          <HistoryPanel year={year} month={month} />
        </div>
      )}

      {data && (
        <RequestsPanel
          canApprove={canEdit}
          myTeamMemberId={myTeamMember?.id ?? null}
          upcomingShifts={myUpcomingShifts}
          otherMembers={otherMembers}
        />
      )}

      {data && myShiftsOnly && myUpcomingShifts.length > 0 && (
        <div className="card">
          <h4 style={{ margin: '0 0 8px', fontSize: 12, textTransform: 'uppercase', letterSpacing: '0.4px', color: 'var(--ink-faint)' }}>
            Acknowledge your upcoming shifts
          </h4>
          {myUpcomingShifts.map((s) => {
            const entry = entriesMap.get(`${myTeamMember!.id}|${s.date}`)
            if (!entry || entry.acknowledgedAt) return null
            return (
              <div key={s.date} className="request-row">
                <div>{s.date} — {s.shiftCode}</div>
                <div className="request-row-actions">
                  <button className="btn-secondary" onClick={() => acknowledgeMutation.mutate(entry.id)}>
                    Acknowledge
                  </button>
                </div>
              </div>
            )
          })}
        </div>
      )}

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
                  leaveRequests={data.leaveRequests}
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
                leaveRequests={data.leaveRequests}
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
          currentNote={entriesMap.get(`${popup.teamMemberId}|${popup.date}`)?.note ?? null}
          onSelect={handleSelectShift}
          onClose={() => setPopup(null)}
          pendingCompOffs={pendingCompOffs ?? []}
          onUseCompOff={(id) => useCompOffMutation.mutate(id)}
          leaveWarning={popupLeaveWarning}
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

      {bulkOpen && data && (
        <BulkAssignModal
          members={data.teamMembers}
          shiftTypes={data.shiftTypes}
          defaultDate={todayIso}
          onClose={() => setBulkOpen(false)}
          onApplied={invalidateRoster}
        />
      )}

      {patternOpen && data && (
        <ApplyPatternModal
          year={year}
          month={month}
          members={data.teamMembers}
          shiftTypes={data.shiftTypes}
          onClose={() => setPatternOpen(false)}
          onApplied={invalidateRoster}
        />
      )}
    </div>
  )
}
