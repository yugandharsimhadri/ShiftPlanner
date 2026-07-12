import { useMemo } from 'react'
import { createColumnHelper, flexRender, getCoreRowModel, useReactTable } from '@tanstack/react-table'
import type { Employee, RosterEntry, ShiftType } from '../types'
import { isWeekend, toIsoDate, weekdayLetter } from '../lib/dates'
import './RosterTable.css'

interface Props {
  employees: Employee[]
  year: number
  month: number
  days: number[]
  entriesMap: Map<string, RosterEntry>
  shiftTypesByCode: Map<string, ShiftType>
  holidaySet: Set<string>
  onCellClick: (employeeId: string, date: string, el: HTMLElement) => void
  canEdit: boolean
}

const columnHelper = createColumnHelper<Employee>()

export default function RosterTable({
  employees,
  year,
  month,
  days,
  entriesMap,
  shiftTypesByCode,
  holidaySet,
  onCellClick,
  canEdit,
}: Props) {
  const columns = useMemo(() => {
    const cols = [
      columnHelper.accessor('id', {
        id: 'employee',
        header: 'Employee',
        cell: (info) => {
          const emp = info.row.original
          return (
            <div className="emp-cell">
              <div className="emp-name">{emp.name}</div>
              <div className="emp-meta mono">
                {emp.code} · {emp.role}
              </div>
            </div>
          )
        },
      }),
      ...days.map((day) =>
        columnHelper.display({
          id: `d${day}`,
          header: () => (
            <div className={`day-head${isWeekend(year, month, day) ? ' weekend' : ''}${holidaySet.has(toIsoDate(year, month, day)) ? ' holiday' : ''}`}>
              <span className="mono">{String(day).padStart(2, '0')}</span>
              <span className="day-head-dow">{weekdayLetter(year, month, day)}</span>
            </div>
          ),
          cell: ({ row }) => {
            const emp = row.original
            const date = toIsoDate(year, month, day)
            const entry = entriesMap.get(`${emp.id}|${date}`)
            const shift = entry?.shiftCode ? shiftTypesByCode.get(entry.shiftCode) : undefined
            return (
              <button
                className={`shift-cell${canEdit ? '' : ' shift-cell-readonly'}`}
                style={
                  shift
                    ? ({ '--chip-color': shift.color } as React.CSSProperties)
                    : undefined
                }
                data-filled={!!shift}
                onClick={(e) => canEdit && onCellClick(emp.id, date, e.currentTarget)}
                title={shift ? `${shift.name}${entry?.source === 'Copied' ? ' (copied)' : ''}` : canEdit ? 'Unassigned' : 'No shift'}
              >
                {shift ? shift.code : ''}
              </button>
            )
          },
        })
      ),
    ]
    return cols
  }, [days, entriesMap, shiftTypesByCode, holidaySet, year, month, onCellClick, canEdit])

  const table = useReactTable({
    data: employees,
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
                <th key={h.id} className={h.column.id === 'employee' ? 'sticky-col' : ''}>
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
                <td key={cell.id} className={cell.column.id === 'employee' ? 'sticky-col' : ''}>
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
