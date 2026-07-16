import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getProfile, updateProfile } from '../api/endpoints'
import './Profile.css'

function detectedTimezone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null
  } catch {
    return null
  }
}

function timezoneOptions(): string[] {
  try {
    // @ts-expect-error -- not in all TS lib targets yet, but supported by modern browsers.
    const values = Intl.supportedValuesOf?.('timeZone') as string[] | undefined
    if (values && values.length > 0) return values
  } catch {
    // fall through to the manual fallback below
  }
  return ['Asia/Kolkata', 'Asia/Dubai', 'Europe/London', 'America/New_York', 'America/Los_Angeles', 'Asia/Singapore', 'Australia/Sydney']
}

export default function Profile() {
  const queryClient = useQueryClient()
  const { data: profile } = useQuery({ queryKey: ['profile'], queryFn: getProfile })

  const [expiryHours, setExpiryHours] = useState('')

  useEffect(() => {
    if (profile) setExpiryHours(String(profile.autoExpiryHours))
  }, [profile])

  const saveMutation = useMutation({
    mutationFn: updateProfile,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['profile'] }),
  })

  // Silently record the browser's timezone the first time we see a profile with none set —
  // no prompt needed, it's just a sensible default the user can still change below.
  useEffect(() => {
    if (profile && !profile.timezone) {
      const detected = detectedTimezone()
      if (detected) saveMutation.mutate({ timezone: detected })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile?.timezone])

  if (!profile) return null

  const hasOverride = profile.autoExpiryHoursOverride !== null
  const expiryChanged = expiryHours.trim() !== '' && Number(expiryHours) !== profile.autoExpiryHours

  return (
    <div className="profile-page">
      <div className="page-heading">
        <h2>My profile</h2>
      </div>
      <p className="page-sub">Your timezone and how long "available" stays on before it clears itself.</p>

      <section className="card settings-section">
        <div className="settings-field-grid">
          <div className="settings-field">
            <label htmlFor="profile-name">Name</label>
            <input id="profile-name" value={profile.name} readOnly />
          </div>
          <div className="settings-field">
            <label htmlFor="profile-phone">Phone</label>
            <input id="profile-phone" value={profile.phone} readOnly />
          </div>
        </div>

        <div className="field">
          <label htmlFor="profile-timezone">Timezone</label>
          <select
            id="profile-timezone"
            value={profile.timezone ?? ''}
            onChange={(e) => saveMutation.mutate({ timezone: e.target.value })}
          >
            {!profile.timezone && <option value="">Not set</option>}
            {timezoneOptions().map((tz) => (
              <option key={tz} value={tz}>{tz.replace(/_/g, ' ')}</option>
            ))}
          </select>
          <p className="field-hint">Used to show your local time to teammates and to pick your default auto-expiry window.</p>
        </div>

        <div className="field">
          <label htmlFor="profile-expiry">Availability auto-expiry (hours)</label>
          <div className="inline-picker">
            <input
              id="profile-expiry"
              type="number"
              min={1}
              max={24}
              value={expiryHours}
              onChange={(e) => setExpiryHours(e.target.value)}
              style={{ maxWidth: 100 }}
            />
            <button
              className="btn"
              disabled={!expiryChanged || saveMutation.isPending}
              onClick={() => saveMutation.mutate({ autoExpiryHoursOverride: Number(expiryHours) })}
            >
              Save
            </button>
            {hasOverride && (
              <button
                className="btn-ghost"
                disabled={saveMutation.isPending}
                onClick={() => saveMutation.mutate({ autoExpiryHoursOverride: null })}
              >
                Reset to default
              </button>
            )}
          </div>
          <p className="field-hint">
            {hasOverride
              ? "You've set a custom window."
              : `Default: 9 hours for India, 8 hours elsewhere — based on your timezone.`}
          </p>
        </div>
      </section>
    </div>
  )
}
