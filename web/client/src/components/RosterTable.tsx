import { useMemo } from 'react'
import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table'
import type { DayOfWeekName, LeaveRequest, RosterEntry, ShiftType, TeamMember } from '../types'
import { isTeamOffDay, toIsoDate, weekdayLetter } from '../lib/dates'
import './RosterTable.css'

interface Props {
  members: TeamMember[]
  year: number
  month: number
  days: number[]
  entriesMap: Map<string, RosterEntry>
  shiftTypesByCode: Map<string, ShiftType>
  holidaySet: Set<string>
  offDays: DayOfWeekName[]
  onCellClick: (teamMemberId: number, date: string, el: HTMLElement) => void
  canEdit: boolean
  leaveRequests?: LeaveRequest[]
}

// Approved leave overlapping the given date, for the given member — a plain date-range
// scan rather than a precomputed set, since a month only has ~30 days per member and
// approved leave requests are typically few.
function onApprovedLeave(leaveRequests: LeaveRequest[] | undefined, teamMemberId: number, date: string): boolean {
  if (!leaveRequests) return false
  return leaveRequests.some(
    (l) => l.teamMemberId === teamMemberId && l.status === 'Approved' && date >= l.startDate && date <= l.endDate
  )
}

const columnHelper = createColumnHelper<TeamMember>()

export default function RosterTable({
  members,
  year,
  month,
  days,
  entriesMap,
  shiftTypesByCode,
  holidaySet,
  offDays,
  onCellClick,
  canEdit,
  leaveRequests,
}: Props) {
  const columns = useMemo(() => {
    const cols = [
      columnHelper.accessor('id', {
        id: 'member',
        header: 'Team member',
        cell: (info) => {
          const member = info.row.original
          return (
            <div className="emp-cell">
              <div className="emp-name">{member.name}</div>
              <div className="emp-meta mono">
                {member.code}{member.jobRoleName ? ` · ${member.jobRoleName}` : ''}
              </div>
            </div>
          )
        },
      }),
      ...days.map((day) =>
        columnHelper.display({
          id: `d${day}`,
          header: () => {
            const offDay = isTeamOffDay(year, month, day, offDays)
            return (
              <div className={`day-head${offDay ? ' off-day' : ''}${holidaySet.has(toIsoDate(year, month, day)) ? ' holiday' : ''}`}>
                <span className="mono">{String(day).padStart(2, '0')}</span>
                <span className="day-head-dow">{weekdayLetter(year, month, day)}</span>
              </div>
            )
          },
          cell: ({ row }) => {
            const member = row.original
            const date = toIsoDate(year, month, day)
            const entry = entriesMap.get(`${member.id}|${date}`)
            const shift = entry?.shiftCode ? shiftTypesByCode.get(entry.shiftCode) : undefined
            const offDay = isTeamOffDay(year, month, day, offDays)
            const workedOffDay = offDay && shift?.isWorkShift
            const onLeave = !shift && onApprovedLeave(leaveRequests, member.id, date)
            const unassigned = !shift && !offDay && !onLeave
            const hasNote = !!entry?.note
            return (
              <button
                className={`shift-cell${canEdit ? '' : ' shift-cell-readonly'}${offDay && !shift ? ' off-day' : ''}${workedOffDay ? ' comp-off-earned' : ''}${onLeave ? ' on-leave' : ''}${unassigned ? ' unassigned' : ''}${hasNote ? ' has-note' : ''}`}
                style={
                  shift
                    ? ({ '--chip-color': shift.color } as React.CSSProperties)
                    : undefined
                }
                data-filled={!!shift}
                onClick={(e) => canEdit && onCellClick(member.id, date, e.currentTarget)}
                title={
                  shift
                    ? `${shift.name}${entry?.source === 'Copied' ? ' (copied)' : ''}${workedOffDay ? ' — earns a comp-off' : ''}${hasNote ? `\nNote: ${entry!.note}` : ''}`
                    : onLeave
                      ? 'Approved leave'
                      : offDay
                        ? 'Weekly off'
                        : canEdit
                          ? 'Unassigned — needs a shift'
                          : 'No shift'
                }
              >
                {shift ? shift.code : onLeave ? 'L' : ''}
              </button>
            )
          },
        })
      ),
    ]
    return cols
  }, [days, entriesMap, shiftTypesByCode, holidaySet, offDays, year, month, onCellClick, canEdit, leaveRequests])

  const table = useReactTable({
    data: members,
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  return (
    <div className="roster-table-scroll">
      <table className="data-table roster-table">
        <thead>
          {table.getHeaderGroups().map((hg) => (
            <tr key={hg.id}>
              {hg.headers.map((h) => (
                <th key={h.id} className={h.column.id === 'member' ? 'sticky-col' : ''}>
                  {flexRender(h.column.columnDef.header, h.getContext())}
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr key={row.id}>
              {row.getVisibleCells().map((cell) => (
                <td key={cell.id} className={cell.column.id === 'member' ? 'sticky-col' : ''}>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
