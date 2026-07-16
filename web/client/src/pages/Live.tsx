import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getMe, getTeamAvailability, updateMyAvailability } from '../api/endpoints'
import AvailabilityList from '../components/AvailabilityList'
import { useTeam } from '../team/TeamContext'
import './Live.css'

const REFRESH_MS = 20000

export default function Live() {
  const { currentTeam } = useTeam()
  const queryClient = useQueryClient()

  const { data: members, isLoading } = useQuery({
    queryKey: ['availability', currentTeam?.id],
    queryFn: getTeamAvailability,
    enabled: !!currentTeam,
    refetchInterval: REFRESH_MS,
  })

  const { data: me } = useQuery({ queryKey: ['me', currentTeam?.id], queryFn: getMe, enabled: !!currentTeam })

  const myRow = members?.find((m) => m.personId === me?.personId)

  const toggleMutation = useMutation({
    mutationFn: updateMyAvailability,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['availability', currentTeam?.id] }),
  })

  return (
    <div className="live-page">
      <div className="page-heading">
        <h2>Live availability</h2>
      </div>
      <p className="page-sub">
        Who's actually free right now — separate from the planned roster. Toggle yourself on when you're available to take
        work; it clears itself automatically after your configured window.
      </p>

      <div className={`self-toggle-card${myRow?.isAvailable ? ' is-on' : ''}`}>
        <div>
          <div className="self-toggle-label">{myRow?.isAvailable ? "You're available" : "You're not available"}</div>
          <div className="field-hint">{myRow?.isAvailable ? 'Team members can see you as free right now.' : 'Flip this on when you can take work.'}</div>
        </div>
        <button
          className={myRow?.isAvailable ? 'btn-secondary' : 'btn'}
          disabled={!myRow || toggleMutation.isPending}
          onClick={() => myRow && toggleMutation.mutate(!myRow.isAvailable)}
        >
          {toggleMutation.isPending ? 'Updating…' : myRow?.isAvailable ? 'Go unavailable' : "I'm available"}
        </button>
      </div>

      {isLoading && <div className="empty-state">Loading…</div>}

      {members && (
        <div className="card availability-card">
          <AvailabilityList members={members} />
        </div>
      )}
    </div>
  )
}
