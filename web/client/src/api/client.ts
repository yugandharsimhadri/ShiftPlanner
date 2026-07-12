import axios from 'axios'

const TOKEN_KEY = 'shiftplanner.token'
const TEAM_KEY = 'shiftplanner.teamId'

let inMemoryToken: string | null = localStorage.getItem(TOKEN_KEY)
let inMemoryTeamId: number | null = (() => {
  const raw = localStorage.getItem(TEAM_KEY)
  return raw ? Number(raw) : null
})()

export function getToken(): string | null {
  return inMemoryToken
}

export function setToken(token: string | null): void {
  inMemoryToken = token
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

export function getTeamId(): number | null {
  return inMemoryTeamId
}

export function setTeamId(teamId: number | null): void {
  inMemoryTeamId = teamId
  if (teamId !== null) localStorage.setItem(TEAM_KEY, String(teamId))
  else localStorage.removeItem(TEAM_KEY)
}

// In dev, Vite runs on 5173 and the API on 5080 (CORS enabled by the API for that origin).
// In production the SPA is served by the API itself from the same origin, so a relative
// base URL works for both without extra config.
const baseURL = import.meta.env.DEV ? 'http://localhost:5080' : ''

export const api = axios.create({ baseURL })

api.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${token}`
  }
  const teamId = getTeamId()
  if (teamId !== null) {
    config.headers = config.headers ?? {}
    config.headers['X-Team-Id'] = String(teamId)
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      setToken(null)
      setTeamId(null)
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)
