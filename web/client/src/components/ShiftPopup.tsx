import { useEffect, useRef, useState } from 'react'
import type { CompOffEntry, ShiftType } from '../types'
import './ShiftPopup.css'

interface Props {
  x: number
  y: number
  shiftTypes: ShiftType[]
  currentCode: string | null
  currentNote?: string | null
  onSelect: (code: string | null, note: string | null) => void
  onClose: () => void
  pendingCompOffs?: CompOffEntry[]
  onUseCompOff?: (id: number) => void
  leaveWarning?: string | null
}

export default function ShiftPopup({
  x,
  y,
  shiftTypes,
  currentCode,
  currentNote,
  onSelect,
  onClose,
  pendingCompOffs = [],
  onUseCompOff,
  leaveWarning,
}: Props) {
  const ref = useRef<HTMLDivElement>(null)
  const [note, setNote] = useState(currentNote ?? '')

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose()
    }
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('mousedown', handleClick)
    document.addEventListener('keydown', handleKey)
    return () => {
      document.removeEventListener('mousedown', handleClick)
      document.removeEventListener('keydown', handleKey)
    }
  }, [onClose])

  const clampedX = Math.min(x, window.innerWidth - 220)
  const clampedY = Math.min(y, window.innerHeight - 260)

  return (
    <div className="shift-popup" ref={ref} style={{ left: clampedX, top: clampedY }}>
      <div className="shift-popup-title">Assign shift</div>

      {leaveWarning && <div className="shift-popup-warning">{leaveWarning}</div>}

      {shiftTypes.map((st) => (
        <button
          key={st.code}
          className={`shift-popup-option${currentCode === st.code ? ' selected' : ''}`}
          style={{ '--chip-color': st.color } as React.CSSProperties}
          onClick={() => onSelect(st.code, note.trim() || null)}
        >
          <span className="shift-popup-code">{st.code}</span>
          <span className="shift-popup-name">{st.name}</span>
          {st.start && st.end && (
            <span className="shift-popup-time mono">
              {st.start.slice(0, 5)}–{st.end.slice(0, 5)}
            </span>
          )}
        </button>
      ))}
      <button className="shift-popup-option shift-popup-clear" onClick={() => onSelect(null, null)}>
        <span className="shift-popup-code">—</span>
        <span className="shift-popup-name">Clear</span>
      </button>

      <textarea
        className="shift-popup-note"
        placeholder="Note (optional) — e.g. cover the register at 2pm"
        value={note}
        onChange={(e) => setNote(e.target.value)}
        rows={2}
      />

      {pendingCompOffs.length > 0 && onUseCompOff && (
        <div className="shift-popup-compoffs">
          <div className="shift-popup-compoffs-title">Pending comp-offs owed</div>
          {pendingCompOffs.map((c) => (
            <button key={c.id} className="shift-popup-compoff-row" onClick={() => onUseCompOff(c.id)}>
              Earned {c.earnedDate} — settle here
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
