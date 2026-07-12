import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getTeamId, setTeamId } from '../api/client'
import { createTeam as createTeamRequest, getMyTeams } from '../api/endpoints'
import { useAuth } from '../auth/AuthContext'
import type { TeamRole, TeamSummary } from '../types'

interface TeamContextValue {
  teams: TeamSummary[]
  isLoading: boolean
  currentTeamId: number | null
  currentTeam: TeamSummary | null
  currentRole: TeamRole | null
  switchTeam: (teamId: number) => void
  createTeam: (name: string) => Promise<TeamSummary>
}

const TeamContext = createContext<TeamContextValue | undefined>(undefined)

export function TeamProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const queryClient = useQueryClient()

  const { data: teams = [], isLoading } = useQuery({
    queryKey: ['teams', 'mine'],
    queryFn: getMyTeams,
    enabled: isAuthenticated,
  })

  const currentTeamId = getTeamId()
  const currentTeam = useMemo(() => teams.find((t) => t.id === currentTeamId) ?? null, [teams, currentTeamId])

  // If the currently-selected team disappears (removed, or none selected yet) and
  // exactly one team is available, just settle on it — no need to make a one-team
  // owner pick from a list of one.
  useEffect(() => {
    if (!isLoading && !currentTeam && teams.length === 1) {
      setTeamId(teams[0].id)
      queryClient.invalidateQueries()
    }
  }, [isLoading, currentTeam, teams, queryClient])

  // Both of these hard-navigate rather than using React Router — a soft
  // navigation races the query-cache clear (the redirect in Layout can fire
  // before the refetched team list lands, bouncing back to the picker). A full
  // reload also guarantees zero stale in-memory state carries across a team
  // boundary, which matters more here than it would for an ordinary route change.
  function switchTeam(teamId: number) {
    setTeamId(teamId)
    window.location.href = '/'
  }

  async function createTeam(name: string): Promise<TeamSummary> {
    const team = await createTeamRequest(name)
    setTeamId(team.id)
    window.location.href = '/'
    return team
  }

  return (
    <TeamContext.Provider
      value={{ teams, isLoading, currentTeamId, currentTeam, currentRole: currentTeam?.role ?? null, switchTeam, createTeam }}
    >
      {children}
    </TeamContext.Provider>
  )
}

export function useTeam(): TeamContextValue {
  const ctx = useContext(TeamContext)
  if (!ctx) throw new Error('useTeam must be used within TeamProvider')
  return ctx
}
