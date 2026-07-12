import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createEmployee, getNextEmployeeCode, updateEmployee } from '../api/endpoints'
import type { Employee, EmployeeInput, EmploymentType, EmployeeStatus, Track } from '../types'

interface Props {
  employee: Employee | null
  tracks: Track[]
  onClose: () => void
}

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

export default function EmployeeFormModal({ employee, tracks, onClose }: Props) {
  const queryClient = useQueryClient()
  const isEdit = !!employee

  const { data: suggestedCode } = useQuery({
    queryKey: ['employees', 'next-code'],
    queryFn: getNextEmployeeCode,
    enabled: !isEdit,
    staleTime: Infinity,
  })

  const [code, setCode] = useState(employee?.code ?? '')
  useEffect(() => {
    if (!isEdit && !code && suggestedCode) setCode(suggestedCode)
  }, [isEdit, code, suggestedCode])

  const [name, setName] = useState(employee?.name ?? '')
  const [phone, setPhone] = useState(employee?.phone ?? '')
  const [email, setEmail] = useState(employee?.email ?? '')
  const [trackId, setTrackId] = useState(employee?.trackId ?? tracks[0]?.id ?? 0)
  const [subtrackId, setSubtrackId] = useState<number | ''>(employee?.subtrackId ?? '')
  const [role, setRole] = useState(employee?.role ?? '')
  const [employmentType, setEmploymentType] = useState<EmploymentType>(employee?.employmentType ?? 'FullTime')
  const [joinDate, setJoinDate] = useState(employee?.joinDate ?? new Date().toISOString().slice(0, 10))
  const [weeklyOff, setWeeklyOff] = useState(employee?.weeklyOff ?? '')
  const [status, setStatus] = useState<EmployeeStatus>(employee?.status ?? 'Active')
  const [notes, setNotes] = useState(employee?.notes ?? '')

  const selectedTrack = tracks.find((t) => t.id === trackId)

  const mutation = useMutation({
    mutationFn: (input: EmployeeInput) =>
      isEdit ? updateEmployee(employee!.id, input) : createEmployee(input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['employees'] })
      queryClient.invalidateQueries({ queryKey: ['roster'] })
      onClose()
    },
  })

  function handleSubmit() {
    mutation.mutate({
      code,
      name,
      phone,
      email: email || null,
      trackId,
      subtrackId: subtrackId === '' ? null : subtrackId,
      role,
      employmentType,
      joinDate,
      weeklyOff: weeklyOff || null,
      status,
      notes: notes || null,
    })
  }

  return (
    <div className="modal-overlay" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal" style={{ width: 560 }}>
        <h2>{isEdit ? `Edit ${employee!.name}` : 'Add employee'}</h2>

        <div className="field-row">
          <div className="field">
            <label htmlFor="emp-name">Name *</label>
            <input id="emp-name" value={name} onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="field">
            <label htmlFor="emp-code">Employee code *</label>
            <input id="emp-code" value={code} onChange={(e) => setCode(e.target.value)} required />
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="emp-phone">Phone *</label>
            <input id="emp-phone" value={phone} onChange={(e) => setPhone(e.target.value)} required />
          </div>
          <div className="field">
            <label htmlFor="emp-email">Email</label>
            <input id="emp-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="emp-role">Role</label>
            <input id="emp-role" value={role} onChange={(e) => setRole(e.target.value)} placeholder="e.g. Cashier" />
          </div>
          <div className="field">
            <label htmlFor="emp-track">Track</label>
            <select
              id="emp-track"
              value={trackId}
              onChange={(e) => {
                setTrackId(Number(e.target.value))
                setSubtrackId('')
              }}
            >
              {tracks.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="emp-subtrack">Subtrack</label>
            <select id="emp-subtrack" value={subtrackId} onChange={(e) => setSubtrackId(e.target.value ? Number(e.target.value) : '')}>
              <option value="">None</option>
              {selectedTrack?.subtracks.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="emp-type">Employment type</label>
            <select id="emp-type" value={employmentType} onChange={(e) => setEmploymentType(e.target.value as EmploymentType)}>
              <option value="FullTime">Full-time</option>
              <option value="PartTime">Part-time</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="emp-status">Status</label>
            <select id="emp-status" value={status} onChange={(e) => setStatus(e.target.value as EmployeeStatus)}>
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
            </select>
          </div>
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="emp-join">Join date</label>
            <input id="emp-join" type="date" value={joinDate} onChange={(e) => setJoinDate(e.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="emp-off">Weekly off</label>
            <select id="emp-off" value={weeklyOff} onChange={(e) => setWeeklyOff(e.target.value)}>
              <option value="">None</option>
              {WEEKDAYS.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="field">
          <label htmlFor="emp-notes">Notes</label>
          <textarea id="emp-notes" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>

        {mutation.isError && <p className="error-text">Could not save employee. Check the fields and try again.</p>}

        <div className="modal-actions">
          <button className="btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button className="btn" onClick={handleSubmit} disabled={!name || !phone || mutation.isPending}>
            {mutation.isPending ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  )
}
