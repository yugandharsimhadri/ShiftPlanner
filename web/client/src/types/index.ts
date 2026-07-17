export type EmploymentType = 'FullTime' | 'PartTime'
export type EmployeeStatus = 'Active' | 'Inactive'
export type RosterEntrySource = 'Manual' | 'Copied'
export type TeamRole = 'Viewer' | 'Editor' | 'Admin'
export type CompOffStatus = 'Pending' | 'Used'
export type LeaveStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'
export type SwapStatus = 'Open' | 'Claimed' | 'Approved' | 'Rejected' | 'Cancelled'
export type DayOfWeekName =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export const ALL_DAYS: DayOfWeekName[] = [
  'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday',
]

export interface Track {
  id: number
  name: string
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
  isWorkShift: boolean
}

export interface Holiday {
  id: number
  date: string
  name: string
}

// --- Master lists (per-team, extendable) ------------------------------------

export interface Location {
  id: number
  name: string
}

export interface JobRole {
  id: number
  name: string
}

// --- Team members (one merged concept — access + roster, was Employee + Membership) ---

export interface TeamMember {
  id: number
  personId: string
  name: string
  phone: string
  email: string | null
  hasLogin: boolean
  code: string
  trackId: number | null
  trackName: string | null
  subtrackId: number | null
  subtrackName: string | null
  jobRoleId: number | null
  jobRoleName: string | null
  locationId: number | null
  locationName: string | null
  employmentType: EmploymentType
  joinDate: string
  status: EmployeeStatus
  notes: string | null
  accessRole: TeamRole
  isTeamLead: boolean
  isCoLead: boolean
  createdAt: string
}

export interface CreateTeamMemberInput {
  name: string
  phone: string
  email?: string | null
  notes?: string | null
  code: string
  trackId?: number | null
  subtrackId?: number | null
  jobRoleId?: number | null
  locationId?: number | null
  employmentType: EmploymentType
  joinDate: string
  status: EmployeeStatus
  accessRole: TeamRole
  teamIds: number[]
}

export interface UpdateTeamMemberInput {
  name: string
  phone: string
  email?: string | null
  notes?: string | null
  code: string
  trackId?: number | null
  subtrackId?: number | null
  jobRoleId?: number | null
  locationId?: number | null
  employmentType: EmploymentType
  joinDate: string
  status: EmployeeStatus
  accessRole: TeamRole
}

export interface UnassignedPerson {
  id: string
  name: string
  phone: string
  email: string | null
}

export interface AssignPersonToTeamInput {
  personId: string
  teamId: number
  code: string
  trackId?: number | null
  subtrackId?: number | null
  jobRoleId?: number | null
  locationId?: number | null
  employmentType: EmploymentType
  joinDate: string
  accessRole: TeamRole
}

export interface Me {
  personId: string
  name: string
  code: string
  role: TeamRole
  isTeamLead: boolean
  isCoLead: boolean
}

export interface RosterEntry {
  id: number
  teamMemberId: number
  date: string
  shiftCode: string | null
  source: RosterEntrySource
  note: string | null
  acknowledgedAt: string | null
}

export interface RosterResponse {
  year: number
  month: number
  entries: RosterEntry[]
  teamMembers: TeamMember[]
  tracks: Track[]
  shiftTypes: ShiftType[]
  holidays: Holiday[]
  defaultOffDays: DayOfWeekName[]
  leaveRequests: LeaveRequest[]
  isPublished: boolean
}

// --- Leave requests -----------------------------------------------------------

export interface LeaveRequest {
  id: number
  teamMemberId: number
  memberName: string
  memberCode: string
  startDate: string
  endDate: string
  reason: string | null
  status: LeaveStatus
  requestedAt: string
  decidedAt: string | null
  decisionNote: string | null
}

export interface CreateLeaveRequestInput {
  startDate: string
  endDate: string
  reason?: string | null
}

// --- Shift swaps ----------------------------------------------------------------
// A one-directional "someone else take my shift" offer, not a mutual trade.

export interface ShiftSwapRequest {
  id: number
  offeredByTeamMemberId: number
  offeredByName: string
  date: string
  shiftCode: string
  targetTeamMemberId: number | null
  targetName: string | null
  claimedByTeamMemberId: number | null
  claimedByName: string | null
  status: SwapStatus
  createdAt: string
}

export interface CreateShiftSwapInput {
  date: string
  shiftCode: string
  targetTeamMemberId?: number | null
}

// --- Roster: bulk / pattern / publish / history / acknowledge -------------------

export interface BulkEntryResult {
  updatedCount: number
  errors: string[]
}

export interface RosterPublishStatus {
  isPublished: boolean
  publishedAt: string | null
}

export interface RosterEntryHistoryRow {
  id: number
  teamMemberId: number
  memberName: string
  date: string
  oldShiftCode: string | null
  newShiftCode: string | null
  changedByUserId: string
  changedAt: string
  source: string
}

export interface CopyForwardFlag {
  teamMemberId: number
  memberName: string
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

export interface TeamSettings {
  name: string
  orgName: string | null
  teamStrength: number | null
  shiftsCovered: string | null
  defaultOffDays: DayOfWeekName[]
  compOffsEnabled: boolean
  activeMemberCount: number
  autoApproveLeaveRequests: boolean
  autoApproveShiftSwaps: boolean
}

export interface UpdateTeamSettingsInput {
  name: string
  orgName?: string | null
  teamStrength?: number | null
  shiftsCovered?: string | null
  defaultOffDays: DayOfWeekName[]
  compOffsEnabled: boolean
  autoApproveLeaveRequests: boolean
  autoApproveShiftSwaps: boolean
}

// --- Comp-offs ---------------------------------------------------------------

export interface CompOffEntry {
  id: number
  teamMemberId: number
  memberCode: string
  memberName: string
  earnedDate: string
  status: CompOffStatus
  usedDate: string | null
}

// --- Live availability -------------------------------------------------------------
// Independent of the planned roster — a member's self-reported "free right now"
// status, visible to every team member and auto-expiring per their own profile setting.

export interface TeamMemberAvailability {
  teamMemberId: number
  personId: string
  name: string
  code: string
  trackName: string | null
  isAvailable: boolean
  availableSince: string | null
  timezone: string | null
}

// --- Profile -------------------------------------------------------------------

export interface Profile {
  name: string
  email: string | null
  phone: string
  timezone: string | null
  autoExpiryHours: number
  autoExpiryHoursOverride: number | null
}

export interface UpdateProfileInput {
  timezone?: string | null
  autoExpiryHoursOverride?: number | null
}

// --- Managers ------------------------------------------------------------------
// A Manager has read-only oversight of the live-availability dashboard across every
// team they're assigned to — separate from AccessRole, so it doesn't grant roster
// edit rights.

export interface PersonSearchResult {
  id: string
  name: string
  phone: string
  email: string | null
}

export interface ManagerAssignment {
  id: number
  personId: string
  personName: string
  personPhone: string
  teamId: number
  teamName: string
}

export interface ManagerTeam {
  id: number
  name: string
}

export interface ManagerTeamAvailability {
  teamId: number
  teamName: string
  members: TeamMemberAvailability[]
}

// --- Reports -------------------------------------------------------------------

export interface UtilizationRow {
  teamMemberId: number
  memberCode: string
  memberName: string
  trackName: string | null
  totalShiftsWorked: number
  weekendShiftsWorked: number
  compOffsEarned: number
  compOffsUsed: number
  compOffsPending: number
}
