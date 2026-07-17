import { useState } from 'react'
import { Link } from 'react-router-dom'
import './OnboardingChecklist.css'

interface Props {
  hasShiftTypes: boolean
  hasOtherMembers: boolean
  hasRosterEntries: boolean
}

const DISMISS_KEY = 'shiftplanner.onboarding-dismissed'

export default function OnboardingChecklist({ hasShiftTypes, hasOtherMembers, hasRosterEntries }: Props) {
  const [dismissed, setDismissed] = useState(() => localStorage.getItem(DISMISS_KEY) === '1')

  const allDone = hasShiftTypes && hasOtherMembers && hasRosterEntries
  if (dismissed || allDone) return null

  function dismiss() {
    localStorage.setItem(DISMISS_KEY, '1')
    setDismissed(true)
  }

  return (
    <div className="onboarding-checklist">
      <div className="onboarding-checklist-header">
        <h4>Get your team set up</h4>
        <button className="onboarding-checklist-dismiss" onClick={dismiss} aria-label="Dismiss">✕</button>
      </div>
      <ul>
        <li className={hasShiftTypes ? 'done' : ''}>
          <span className="onboarding-checklist-check">{hasShiftTypes ? '✓' : '○'}</span>
          <span>Add your shift types</span>
          {!hasShiftTypes && <Link to="/settings" className="onboarding-checklist-link">Go to Settings</Link>}
        </li>
        <li className={hasOtherMembers ? 'done' : ''}>
          <span className="onboarding-checklist-check">{hasOtherMembers ? '✓' : '○'}</span>
          <span>Add your team members</span>
          {!hasOtherMembers && <Link to="/members" className="onboarding-checklist-link">Go to Team Members</Link>}
        </li>
        <li className={hasRosterEntries ? 'done' : ''}>
          <span className="onboarding-checklist-check">{hasRosterEntries ? '✓' : '○'}</span>
          <span>Assign your first shift on the roster below</span>
        </li>
      </ul>
    </div>
  )
}
