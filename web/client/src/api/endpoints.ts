import { api } from './client'
import type {
  CopyForwardResult,
  Employee,
  EmployeeInput,
  Holiday,
  ImportResult,
  Membership,
  RosterResponse,
  ShiftType,
  Subtrack,
  TeamRole,
  TeamSummary,
  Track,
} from '../types'

// --- Teams -------------------------------------------------------------------

export async function getMyTeams(): Promise<TeamSummary[]> {
  const res = await api.get('/api/teams/mine')
  return res.data
}

export async function createTeam(name: string): Promise<TeamSummary> {
  const res = await api.post('/api/teams', { name })
  return res.data
}

export async function getMembers(): Promise<Membership[]> {
  const res = await api.get('/api/teams/current/members')
  return res.data
}

export async function addMember(payload: { email: string; role: TeamRole }): Promise<Membership> {
  const res = await api.post('/api/teams/current/members', payload)
  return res.data
}

export async function updateMemberRole(membershipId: number, role: TeamRole): Promise<Membership> {
  const res = await api.patch(`/api/teams/current/members/${membershipId}`, { role })
  return res.data
}

export async function linkMemberEmployee(membershipId: number, employeeId: string | null): Promise<Membership> {
  const res = await api.patch(`/api/teams/current/members/${membershipId}/employee`, { employeeId })
  return res.data
}

export async function removeMember(membershipId: number): Promise<void> {
  await api.delete(`/api/teams/current/members/${membershipId}`)
}

// --- Roster ---------------------------------------------------------------

export async function getRoster(year: number, month: number): Promise<RosterResponse> {
  const res = await api.get('/api/roster', { params: { year, month } })
  return res.data
}

export async function upsertRosterEntry(payload: {
  employeeId: string
  date: string
  shiftCode: string | null
  note?: string | null
}) {
  const res = await api.put('/api/roster/entry', payload)
  return res.data
}

export interface CopyForwardRequest {
  sourceYear: number
  sourceMonth: number
  targetYear: number
  targetMonth: number
  pattern: 'weekday' | 'exact-date'
  skipInactive: boolean
}

export async function copyForward(payload: CopyForwardRequest): Promise<CopyForwardResult> {
  const res = await api.post('/api/roster/copy-forward', payload)
  return res.data
}

// --- Employees --------------------------------------------------------------

export async function getEmployees(): Promise<Employee[]> {
  const res = await api.get('/api/employees')
  return res.data
}

export async function getNextEmployeeCode(): Promise<string> {
  const res = await api.get('/api/employees/next-code')
  return res.data.code
}

export async function createEmployee(payload: EmployeeInput): Promise<Employee> {
  const res = await api.post('/api/employees', payload)
  return res.data
}

export async function updateEmployee(id: string, payload: EmployeeInput): Promise<Employee> {
  const res = await api.put(`/api/employees/${id}`, payload)
  return res.data
}

export async function deleteEmployee(id: string): Promise<void> {
  await api.delete(`/api/employees/${id}`)
}

// --- Tracks / Subtracks ------------------------------------------------------

export async function getTracks(): Promise<Track[]> {
  const res = await api.get('/api/tracks')
  return res.data
}

export async function createTrack(payload: { name: string; leadName: string | null; color: string }) {
  const res = await api.post('/api/tracks', { id: null, ...payload })
  return res.data
}

export async function updateTrack(id: number, payload: { name: string; leadName: string | null; color: string }) {
  const res = await api.put(`/api/tracks/${id}`, { id, ...payload })
  return res.data
}

export async function deleteTrack(id: number): Promise<void> {
  await api.delete(`/api/tracks/${id}`)
}

export async function createSubtrack(payload: { trackId: number; name: string }): Promise<Subtrack> {
  const res = await api.post('/api/subtracks', { id: null, ...payload })
  return res.data
}

export async function deleteSubtrack(id: number): Promise<void> {
  await api.delete(`/api/subtracks/${id}`)
}

// --- Shift types --------------------------------------------------------------

export async function getShiftTypes(): Promise<ShiftType[]> {
  const res = await api.get('/api/shift-types')
  return res.data
}

export async function createShiftType(payload: Omit<ShiftType, 'id'>): Promise<ShiftType> {
  const res = await api.post('/api/shift-types', payload)
  return res.data
}

export async function updateShiftType(id: number, payload: Omit<ShiftType, 'id'>): Promise<ShiftType> {
  const res = await api.put(`/api/shift-types/${id}`, payload)
  return res.data
}

export async function deleteShiftType(id: number): Promise<void> {
  await api.delete(`/api/shift-types/${id}`)
}

// --- Holidays --------------------------------------------------------------

export async function getHolidays(): Promise<Holiday[]> {
  const res = await api.get('/api/holidays')
  return res.data
}

export async function createHoliday(payload: { date: string; name: string }): Promise<Holiday> {
  const res = await api.post('/api/holidays', { id: null, ...payload })
  return res.data
}

export async function deleteHoliday(id: number): Promise<void> {
  await api.delete(`/api/holidays/${id}`)
}

// --- Import / Export --------------------------------------------------------------

export async function importEmployees(file: File): Promise<ImportResult> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await api.post('/api/import/employees', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return res.data
}

// Exports require the Authorization header, so a plain <a href> won't carry the token —
// fetch as a blob and trigger the browser's save dialog manually.
export async function downloadExport(kind: 'excel' | 'csv', year: number, month: number): Promise<void> {
  const res = await api.get(`/api/export/${kind}`, {
    params: { year, month },
    responseType: 'blob',
  })
  const ext = kind === 'excel' ? 'xlsx' : 'csv'
  const url = window.URL.createObjectURL(new Blob([res.data]))
  const link = document.createElement('a')
  link.href = url
  link.download = `roster-${year}-${String(month).padStart(2, '0')}.${ext}`
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(url)
}
