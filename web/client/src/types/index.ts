export type EmploymentType = 'FullTime' | 'PartTime'
export type EmployeeStatus = 'Active' | 'Inactive'
export type RosterEntrySource = 'Manual' | 'Copied'
export type TeamRole = 'Viewer' | 'Editor' | 'Admin'
export type MembershipStatus = 'Invited' | 'Active'

export interface Track {
  id: number
  name: string
  leadName: string | null
  color: string
  subtracks: Subtrack[]
}

export interface Subtrack {
  id: number
  trackId: number
  name: string
}

export interface ShiftType {
  id: number
  code: string
  name: string
  start: string | null
  end: string | null
  color: string
  isOvernight: boolean
}

export interface Holiday {
  id: number
  date: string
  name: string
}

export interface Employee {
  id: string
  code: string
  name: string
  phone: string
  email: string | null
  trackId: number
  track: Track | null
  subtrackId: number | null
  subtrack: Subtrack | null
  role: string
  employmentType: EmploymentType
  joinDate: string
  weeklyOff: string | null
  status: EmployeeStatus
  notes: string | null
}

export interface EmployeeInput {
  code: string
  name: string
  phone: string
  email?: string | null
  trackId: number
  subtrackId?: number | null
  role: string
  employmentType: EmploymentType
  joinDate: string
  weeklyOff?: string | null
  status: EmployeeStatus
  notes?: string | null
}

export interface RosterEntry {
  id: number
  employeeId: string
  date: string
  shiftCode: string | null
  source: RosterEntrySource
  note: string | null
}

export interface RosterResponse {
  year: number
  month: number
  entries: RosterEntry[]
  employees: Employee[]
  tracks: Track[]
  shiftTypes: ShiftType[]
  holidays: Holiday[]
}

export interface CopyForwardFlag {
  employeeId: string
  employeeName: string
  date: string
  reason: string
}

export interface CopyForwardResult {
  copiedCount: number
  flagged: CopyForwardFlag[]
}

export interface ImportRowError {
  row: number
  message: string
}

export interface ImportResult {
  imported: number
  errors: ImportRowError[]
}

// --- Teams -----------------------------------------------------------------

export interface TeamSummary {
  id: number
  name: string
  role: TeamRole
}

export interface Membership {
  id: number
  email: string
  role: TeamRole
  status: MembershipStatus
  employeeId: string | null
  createdAt: string
}
