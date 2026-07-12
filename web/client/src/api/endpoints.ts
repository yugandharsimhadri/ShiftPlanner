import { api } from './client'
import type {
  AssignPersonToTeamInput,
  CompOffEntry,
  CompOffStatus,
  CopyForwardResult,
  CreateTeamMemberInput,
  Holiday,
  ImportResult,
  Me,
  RosterResponse,
  ShiftType,
  Subtrack,
  TeamMember,
  TeamSettings,
  TeamSummary,
  Track,
  UnassignedPerson,
  UpdateTeamMemberInput,
  UpdateTeamSettingsInput,
  UtilizationRow,
} from '../types'

// --- Auth ----------------------------------------------------------------

// Either email or phone (or both) — whichever is given can log in with it later.
export async function registerAccount(payload: { email?: string | null; phone?: string | null; password: string }): Promise<void> {
  await api.post('/api/register-account', payload)
}

export async function loginWithPhone(phone: string, password: string): Promise<{ accessToken: string }> {
  const res = await api.post('/api/login-phone', { phone, password })
  return res.data
}

// --- Teams -------------------------------------------------------------------

export async function getMyTeams(): Promise<TeamSummary[]> {
  const res = await api.get('/api/teams/mine')
  return res.data
}

export async function createTeam(name: string): Promise<TeamSummary> {
  const res = await api.post('/api/teams', { name })
  return res.data
}

// --- Team members (merged Employees + Members) --------------------------------

export async function getTeamMembers(): Promise<TeamMember[]> {
  const res = await api.get('/api/teams/current/members')
  return res.data
}

export async function getMe(): Promise<Me> {
  const res = await api.get('/api/teams/current/members/me')
  return res.data
}

export async function getNextTeamMemberCode(): Promise<string> {
  const res = await api.get('/api/teams/current/members/next-code')
  return res.data.code
}

export async function getUnassignedCandidates(): Promise<UnassignedPerson[]> {
  const res = await api.get('/api/teams/current/members/unassigned-candidates')
  return res.data
}

export async function createTeamMember(payload: CreateTeamMemberInput): Promise<TeamMember> {
  const res = await api.post('/api/teams/current/members', payload)
  return res.data
}

export async function assignExistingPerson(payload: AssignPersonToTeamInput): Promise<TeamMember> {
  const res = await api.post('/api/teams/current/members/assign-existing', payload)
  return res.data
}

export async function updateTeamMember(id: number, payload: UpdateTeamMemberInput): Promise<TeamMember> {
  const res = await api.put(`/api/teams/current/members/${id}`, payload)
  return res.data
}

export async function updateMemberAccessRole(id: number, accessRole: string): Promise<TeamMember> {
  const res = await api.patch(`/api/teams/current/members/${id}/role`, { accessRole })
  return res.data
}

export async function removeMember(id: number): Promise<void> {
  await api.delete(`/api/teams/current/members/${id}`)
}

export async function transferLead(id: number): Promise<TeamMember> {
  const res = await api.patch(`/api/teams/current/members/${id}/lead`)
  return res.data
}

export async function setCoLead(id: number, isCoLead: boolean): Promise<TeamMember> {
  const res = await api.patch(`/api/teams/current/members/${id}/co-lead`, { isCoLead })
  return res.data
}

// --- Team settings -----------------------------------------------------------

export async function getTeamSettings(): Promise<TeamSettings> {
  const res = await api.get('/api/teams/current/settings')
  return res.data
}

export async function updateTeamSettings(payload: UpdateTeamSettingsInput): Promise<TeamSettings> {
  const res = await api.put('/api/teams/current/settings', payload)
  return res.data
}

// --- Comp-offs ---------------------------------------------------------------

export async function getCompOffs(status?: CompOffStatus, teamMemberId?: number): Promise<CompOffEntry[]> {
  const res = await api.get('/api/compoffs', { params: { status, teamMemberId } })
  return res.data
}

export async function useCompOff(id: number, usedDate: string): Promise<CompOffEntry> {
  const res = await api.post(`/api/compoffs/${id}/use`, { usedDate })
  return res.data
}

export async function unuseCompOff(id: number): Promise<CompOffEntry> {
  const res = await api.post(`/api/compoffs/${id}/unuse`)
  return res.data
}

// --- Reports -------------------------------------------------------------------

export async function getUtilizationReport(start: string, end: string): Promise<UtilizationRow[]> {
  const res = await api.get('/api/reports/utilization', { params: { start, end } })
  return res.data
}

// --- Roster ---------------------------------------------------------------

export async function getRoster(year: number, month: number): Promise<RosterResponse> {
  const res = await api.get('/api/roster', { params: { year, month } })
  return res.data
}

export async function upsertRosterEntry(payload: {
  teamMemberId: number
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
