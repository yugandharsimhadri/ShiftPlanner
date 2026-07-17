import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  approveLeaveRequest,
  approveShiftSwap,
  cancelLeaveRequest,
  cancelShiftSwap,
  claimShiftSwap,
  getLeaveRequests,
  getShiftSwaps,
  rejectLeaveRequest,
  rejectShiftSwap,
} from '../api/endpoints'
import type { TeamMember } from '../types'
import LeaveRequestModal from './LeaveRequestModal'
import ShiftSwapModal from './ShiftSwapModal'
import './RequestsPanel.css'

interface UpcomingShift {
  date: string
  shiftCode: string
}

interface Props {
  canApprove: boolean
  myTeamMemberId: number | null
  upcomingShifts: UpcomingShift[]
  otherMembers: TeamMember[]
}

export default function RequestsPanel({ canApprove, myTeamMemberId, upcomingShifts, otherMembers }: Props) {
  const [expanded, setExpanded] = useState(false)
  const [leaveModalOpen, setLeaveModalOpen] = useState(false)
  const [swapModalOpen, setSwapModalOpen] = useState(false)
  const queryClient = useQueryClient()

  const { data: leaveRequests = [] } = useQuery({ queryKey: ['leave-requests'], queryFn: () => getLeaveRequests() })
  const { data: shiftSwaps = [] } = useQuery({ queryKey: ['shift-swaps'], queryFn: () => getShiftSwaps() })

  function refresh() {
    queryClient.invalidateQueries({ queryKey: ['leave-requests'] })
    queryClient.invalidateQueries({ queryKey: ['shift-swaps'] })
    queryClient.invalidateQueries({ queryKey: ['roster'] })
  }

  const approveLeave = useMutation({ mutationFn: approveLeaveRequest, onSuccess: refresh })
  const rejectLeave = useMutation({ mutationFn: (id: number) => rejectLeaveRequest(id), onSuccess: refresh })
  const cancelLeave = useMutation({ mutationFn: cancelLeaveRequest, onSuccess: refresh })
  const claimSwap = useMutation({ mutationFn: claimShiftSwap, onSuccess: refresh })
  const approveSwap = useMutation({ mutationFn: approveShiftSwap, onSuccess: refresh })
  const rejectSwap = useMutation({ mutationFn: rejectShiftSwap, onSuccess: refresh })
  const cancelSwap = useMutation({ mutationFn: cancelShiftSwap, onSuccess: refresh })

  const pendingLeave = leaveRequests.filter((l) => l.status === 'Pending')
  const openOrClaimedSwaps = shiftSwaps.filter((s) => s.status === 'Open' || s.status === 'Claimed')
  const pendingApprovalCount = (canApprove ? pendingLeave.length + openOrClaimedSwaps.filter((s) => s.status === 'Claimed').length : 0)

  const myLeave = leaveRequests.filter((l) => l.teamMemberId === myTeamMemberId)
  const myOfferedSwaps = shiftSwaps.filter((s) => s.offeredByTeamMemberId === myTeamMemberId)
  const claimableSwaps = shiftSwaps.filter(
    (s) => s.status === 'Open' && s.offeredByTeamMemberId !== myTeamMemberId &&
      (s.targetTeamMemberId === null || s.targetTeamMemberId === myTeamMemberId)
  )

  return (
    <div className="requests-panel">
      <button className="requests-panel-toggle" onClick={() => setExpanded((v) => !v)}>
        <span>Requests</span>
        {pendingApprovalCount > 0 && <span className="badge badge-warn">{pendingApprovalCount} awaiting you</span>}
        <span className="requests-panel-chevron">{expanded ? '▾' : '▸'}</span>
      </button>

      {expanded && (
        <div className="requests-panel-body">
          <div className="requests-panel-actions">
            <button className="btn-secondary" onClick={() => setLeaveModalOpen(true)}>
              Request leave
            </button>
            <button className="btn-secondary" onClick={() => setSwapModalOpen(true)} disabled={upcomingShifts.length === 0}>
              Offer a shift
            </button>
          </div>

          {canApprove && (pendingLeave.length > 0 || openOrClaimedSwaps.length > 0) && (
            <section className="requests-section">
              <h4>Awaiting your approval</h4>
              {pendingLeave.map((l) => (
                <div key={`leave-${l.id}`} className="request-row">
                  <div>
                    <strong>{l.memberName}</strong> ({l.memberCode}) — leave {l.startDate} to {l.endDate}
                    {l.reason && <span className="request-reason"> · {l.reason}</span>}
                  </div>
                  <div className="request-row-actions">
                    <button className="btn-secondary" onClick={() => approveLeave.mutate(l.id)}>Approve</button>
                    <button className="btn-secondary" onClick={() => rejectLeave.mutate(l.id)}>Reject</button>
                  </div>
                </div>
              ))}
              {openOrClaimedSwaps.map((s) => (
                <div key={`swap-${s.id}`} className="request-row">
                  <div>
                    <strong>{s.offeredByName}</strong>'s shift {s.date} ({s.shiftCode}){' '}
                    {s.status === 'Claimed' ? (
                      <>— claimed by <strong>{s.claimedByName}</strong></>
                    ) : (
                      <span className="request-reason">— not yet claimed</span>
                    )}
                  </div>
                  <div className="request-row-actions">
                    {s.status === 'Claimed' && (
                      <button className="btn-secondary" onClick={() => approveSwap.mutate(s.id)}>Approve</button>
                    )}
                    <button className="btn-secondary" onClick={() => rejectSwap.mutate(s.id)}>Reject</button>
                  </div>
                </div>
              ))}
            </section>
          )}

          {claimableSwaps.length > 0 && (
            <section className="requests-section">
              <h4>Open offers you can claim</h4>
              {claimableSwaps.map((s) => (
                <div key={s.id} className="request-row">
                  <div>
                    <strong>{s.offeredByName}</strong>'s shift {s.date} ({s.shiftCode})
                  </div>
                  <div className="request-row-actions">
                    <button className="btn-secondary" onClick={() => claimSwap.mutate(s.id)}>Claim</button>
                  </div>
                </div>
              ))}
            </section>
          )}

          <section className="requests-section">
            <h4>My requests</h4>
            {myLeave.length === 0 && myOfferedSwaps.length === 0 && (
              <p className="requests-empty">Nothing yet — request leave or offer a shift above.</p>
            )}
            {myLeave.map((l) => (
              <div key={`my-leave-${l.id}`} className="request-row">
                <div>
                  Leave {l.startDate} to {l.endDate} — <span className={`badge badge-${l.status.toLowerCase()}`}>{l.status}</span>
                </div>
                {l.status === 'Pending' && (
                  <div className="request-row-actions">
                    <button className="btn-secondary" onClick={() => cancelLeave.mutate(l.id)}>Cancel</button>
                  </div>
                )}
              </div>
            ))}
            {myOfferedSwaps.map((s) => (
              <div key={`my-swap-${s.id}`} className="request-row">
                <div>
                  Offered {s.date} ({s.shiftCode}) — <span className={`badge badge-${s.status.toLowerCase()}`}>{s.status}</span>
                  {s.claimedByName && s.status !== 'Approved' && <span className="request-reason"> · claimed by {s.claimedByName}</span>}
                </div>
                {(s.status === 'Open' || s.status === 'Claimed') && (
                  <div className="request-row-actions">
                    <button className="btn-secondary" onClick={() => cancelSwap.mutate(s.id)}>Cancel</button>
                  </div>
                )}
              </div>
            ))}
          </section>
        </div>
      )}

      {leaveModalOpen && <LeaveRequestModal onClose={() => setLeaveModalOpen(false)} onCreated={refresh} />}
      {swapModalOpen && (
        <ShiftSwapModal
          upcomingShifts={upcomingShifts}
          otherMembers={otherMembers}
          onClose={() => setSwapModalOpen(false)}
          onCreated={refresh}
        />
      )}
    </div>
  )
}
